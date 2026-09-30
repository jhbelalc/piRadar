using System.Text.RegularExpressions;
using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;
using PiRadar.Data.Records;
using SQLite;

namespace PiRadar.Data.Repositories;

internal sealed partial class PaymentMethodRepository(DatabaseHost host)
    : RepositoryBase(host), IPaymentMethodRepository
{
    public async Task<IReadOnlyList<PaymentMethod>> GetAllAsync(bool includeArchived = false)
    {
        var query = Connection.Table<PaymentMethodRecord>();
        if (!includeArchived)
        {
            query = query.Where(p => !p.IsArchived);
        }

        var records = await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync();
        return records.Select(r => r.ToDomain()).ToList();
    }

    public async Task<PaymentMethod?> GetAsync(int id) =>
        (await Connection.FindAsync<PaymentMethodRecord>(id))?.ToDomain();

    public Task<PaymentMethod> AddAsync(PaymentMethod paymentMethod) => InTransactionAsync(db =>
    {
        var record = paymentMethod.ToRecord();
        record.Id = 0;
        record.IsSystem = false;
        Validate(db, record);
        record.SortOrder = db.ExecuteScalar<int>("""SELECT coalesce(max("SortOrder"), -1) + 1 FROM "PaymentMethod" """);
        db.Insert(record);
        return record.ToDomain();
    });

    public Task UpdateAsync(PaymentMethod paymentMethod) => InTransactionAsync(db =>
    {
        var stored = db.Find<PaymentMethodRecord>(paymentMethod.Id)
            ?? throw new KeyNotFoundException($"No existe el medio de pago {paymentMethod.Id}.");

        var record = paymentMethod.ToRecord();
        record.IsSystem = stored.IsSystem;
        Validate(db, record);
        db.Update(record);
        return true;
    });

    public Task<RemovalResult> DeleteOrArchiveAsync(int id) => InTransactionAsync(db =>
    {
        var stored = db.Find<PaymentMethodRecord>(id)
            ?? throw new KeyNotFoundException($"No existe el medio de pago {id}.");

        if (Exists(db, """SELECT 1 FROM "Transactions" WHERE "PaymentMethodId" = ?""", id))
        {
            stored.IsArchived = true;
            db.Update(stored);
            return RemovalResult.Archived;
        }

        db.Delete<PaymentMethodRecord>(id);
        return RemovalResult.Deleted;
    });

    private static void Validate(SQLiteConnection db, PaymentMethodRecord record)
    {
        record.Name = RequireName(record.Name, "el medio de pago", "payment method");
        if (!Enum.IsDefined(record.Kind))
        {
            throw new DomainValidationException(AppLocale.Pick("Tipo de medio de pago no válido.", "Invalid payment method type."));
        }

        if (string.IsNullOrWhiteSpace(record.Icon))
        {
            throw new DomainValidationException(AppLocale.Pick("El medio de pago necesita un ícono.", "The payment method needs an icon."));
        }

        record.LastFourDigits = string.IsNullOrWhiteSpace(record.LastFourDigits) ? null : record.LastFourDigits.Trim();
        if (record.LastFourDigits is not null && !FourDigits().IsMatch(record.LastFourDigits))
        {
            throw new DomainValidationException(AppLocale.Pick("Los últimos dígitos deben ser exactamente 4 números.", "The last digits must be exactly 4 numbers."));
        }

        if (!record.IsArchived)
        {
            var others = db.Table<PaymentMethodRecord>().Where(p => !p.IsArchived && p.Id != record.Id).ToList();
            if (others.Any(p => string.Equals(p.Name, record.Name, StringComparison.CurrentCultureIgnoreCase)))
            {
                throw new DomainValidationException(AppLocale.Pick($"Ya existe un medio de pago llamado «{record.Name}».", $"A payment method named “{record.Name}” already exists."));
            }
        }
    }

    [GeneratedRegex("^[0-9]{4}$")]
    private static partial Regex FourDigits();
}
