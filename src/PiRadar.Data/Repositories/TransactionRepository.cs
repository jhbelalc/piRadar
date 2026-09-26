using System.Globalization;
using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;
using PiRadar.Data.Records;
using SQLite;

namespace PiRadar.Data.Repositories;

internal sealed class TransactionRepository(DatabaseHost host, IClock clock)
    : RepositoryBase(host), ITransactionRepository
{
    public async Task<Transaction?> GetAsync(Guid id) =>
        (await Connection.FindAsync<TransactionRecord>(id))?.ToDomain();

    public async Task<IReadOnlyList<Transaction>> QueryAsync(TransactionFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfNegative(filter.Skip);

        var query = Connection.Table<TransactionRecord>();

        // OccurredAt se guarda como ticks locales: los límites deben estar en la misma hora local.
        if (filter.From is { } from)
        {
            var fromLocal = RecordMapping.ToLocalForStorage(from);
            query = query.Where(t => t.OccurredAt >= fromLocal);
        }

        if (filter.To is { } to)
        {
            var toLocal = RecordMapping.ToLocalForStorage(to);
            query = query.Where(t => t.OccurredAt < toLocal);
        }

        if (filter.Type is { } type)
        {
            query = query.Where(t => t.Type == type);
        }

        if (filter.Currency is { } currency)
        {
            var code = currency.ToString();
            query = query.Where(t => t.Currency == code);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(t => t.CategoryId == categoryId);
        }

        if (filter.PaymentMethodId is { } paymentMethodId)
        {
            query = query.Where(t => t.PaymentMethodId == paymentMethodId);
        }

        // CreatedAt desempata movimientos con la misma hora para que la paginación sea estable.
        query = query.OrderByDescending(t => t.OccurredAt).ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id);
        if (filter.Skip > 0)
        {
            query = query.Skip(filter.Skip);
        }

        if (filter.Take is { } take)
        {
            query = query.Take(take);
        }

        var records = await query.ToListAsync();
        return records.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<string>> GetRecentMerchantsAsync(int take = 200)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        // COLLATE NOCASE de SQLite solo pliega ASCII ("Éxito" ≠ "éxito"): se agrupa exacto y se deduplica aquí.
        var merchants = await Connection.QueryScalarsAsync<string>(
            """
            SELECT "Merchant" FROM "Transactions"
            WHERE "Merchant" IS NOT NULL
            GROUP BY "Merchant"
            ORDER BY max("OccurredAt") DESC
            """);
        var seen = new HashSet<string>(MerchantComparer);
        return merchants.Where(seen.Add).Take(take).ToList();
    }

    /// <summary>Sin distinguir mayúsculas ni tildes: "Éxito", "éxito" y "EXITO" son el mismo comercio.</summary>
    private static readonly StringComparer MerchantComparer = StringComparer.Create(
        CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);

    public Task<Transaction> AddAsync(Transaction transaction) => InTransactionAsync(db =>
    {
        var record = NewRecord(transaction);
        record.Source = TransactionSource.Manual;
        record.ReceiptId = null;
        Validate(db, record, stored: null);
        db.Insert(record);
        return record.ToDomain();
    });

    public Task<Transaction> AddWithReceiptAsync(Transaction transaction, Receipt receipt, ReceiptImage image)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(image);

        return InTransactionAsync(db =>
        {
            var receiptRecord = receipt.ToRecord();
            if (receiptRecord.Id == Guid.Empty)
            {
                receiptRecord.Id = Guid.NewGuid();
            }

            if (receiptRecord.CapturedAt == default)
            {
                receiptRecord.CapturedAt = clock.UtcNow;
            }

            if (image.Jpeg is not { Length: > 0 } || image.Width <= 0 || image.Height <= 0)
            {
                throw new DomainValidationException("La imagen del recibo no es válida.");
            }

            var imageRecord = image.ToRecord();
            imageRecord.ReceiptId = receiptRecord.Id;

            var record = NewRecord(transaction);
            record.Source = TransactionSource.Ocr;
            record.ReceiptId = receiptRecord.Id;
            Validate(db, record, stored: null);

            db.Insert(receiptRecord);
            db.Insert(imageRecord);
            db.Insert(record);
            return record.ToDomain();
        });
    }

    public Task<Transaction> UpdateAsync(Transaction transaction) => InTransactionAsync(db =>
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var stored = db.Find<TransactionRecord>(transaction.Id)
            ?? throw new KeyNotFoundException($"No existe el movimiento {transaction.Id}.");

        var record = transaction.ToRecord();
        record.Source = stored.Source;
        record.ReceiptId = stored.ReceiptId;
        record.CreatedAt = stored.CreatedAt;
        record.UpdatedAt = clock.UtcNow;
        Validate(db, record, stored);
        db.Update(record);
        return record.ToDomain();
    });

    public Task<bool> DeleteAsync(Guid id) => InTransactionAsync(db =>
    {
        var stored = db.Find<TransactionRecord>(id);
        if (stored is null)
        {
            return false;
        }

        db.Delete<TransactionRecord>(id);
        if (stored.ReceiptId is { } receiptId)
        {
            // La imagen se borra en cascada (FK ON DELETE CASCADE).
            db.Delete<ReceiptRecord>(receiptId);
        }

        return true;
    });

    private TransactionRecord NewRecord(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var record = transaction.ToRecord();
        if (record.Id == Guid.Empty)
        {
            record.Id = Guid.NewGuid();
        }

        var now = clock.UtcNow;
        record.CreatedAt = now;
        record.UpdatedAt = now;
        return record;
    }

    /// <param name="stored">La versión guardada al editar; <c>null</c> al crear.</param>
    private static void Validate(SQLiteConnection db, TransactionRecord record, TransactionRecord? stored)
    {
        if (!Enum.IsDefined(record.Type))
        {
            throw new DomainValidationException("Tipo de movimiento no válido.");
        }

        if (record.AmountMinor <= 0)
        {
            throw new DomainValidationException("El monto debe ser mayor que cero.");
        }

        record.Merchant = TrimToNull(record.Merchant);
        record.Description = TrimToNull(record.Description);
        record.Notes = TrimToNull(record.Notes);

        var category = db.Find<CategoryRecord>(record.CategoryId)
            ?? throw new DomainValidationException("La categoría no existe.");
        if (category.Kind != record.Type)
        {
            throw new DomainValidationException(record.Type == TransactionType.Expense
                ? "La categoría elegida es de ingresos; el movimiento es un gasto."
                : "La categoría elegida es de gastos; el movimiento es un ingreso.");
        }

        // Una categoría o medio archivado no se puede elegir de nuevo, pero un movimiento viejo lo conserva al editarse.
        if (category.IsArchived && stored?.CategoryId != record.CategoryId)
        {
            throw new DomainValidationException($"La categoría «{category.Name}» está archivada.");
        }

        if (record.PaymentMethodId is { } paymentMethodId)
        {
            var method = db.Find<PaymentMethodRecord>(paymentMethodId)
                ?? throw new DomainValidationException("El medio de pago no existe.");
            if (method.IsArchived && stored?.PaymentMethodId != paymentMethodId)
            {
                throw new DomainValidationException($"El medio de pago «{method.Name}» está archivado.");
            }
        }
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
