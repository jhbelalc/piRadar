using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;
using PiRadar.Core.Import;
using PiRadar.Data.Records;
using PiRadar.Data.Seed;
using SQLite;

namespace PiRadar.Data.Repositories;

/// <summary>
/// Guarda una importación completa (categorías, medios de pago, lote y movimientos) en una sola transacción de BD:
/// si algo falla no queda nada. Las filas se escriben en bloque y sin consultas por fila.
/// </summary>
internal sealed class TransactionImporter(DatabaseHost host, IClock clock) : RepositoryBase(host), ITransactionImporter
{
    private const int MaxFileNameLength = 200;
    private const string NewCategoryIcon = "more_horiz"; // "label" no está en el catálogo de íconos de la app.
    private const string NewPaymentMethodIcon = "payments";

    public Task<ImportResult> ImportAsync(ImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return InTransactionAsync(db => new Writer(db, plan, clock.UtcNow).Run());
    }

    public async Task<ImportBatchInfo?> GetLastImportAsync()
    {
        var batches = await Connection.QueryAsync<ImportBatchRecord>(
            """SELECT * FROM "ImportBatch" ORDER BY "ImportedAt" DESC, rowid DESC LIMIT 1""");
        if (batches.Count == 0)
        {
            return null;
        }

        var batch = batches[0];
        var remaining = await Connection.ExecuteScalarAsync<int>(
            """SELECT count(*) FROM "Transactions" WHERE "ImportBatchId" = ?""", batch.Id);
        return new ImportBatchInfo(
            batch.Id, DateTime.SpecifyKind(batch.ImportedAt, DateTimeKind.Utc), batch.FileName, batch.RowCount, remaining);
    }

    public Task<int> UndoLastImportAsync(Guid batchId) => InTransactionAsync(db =>
    {
        var batch = db.Query<ImportBatchRecord>(
            """SELECT * FROM "ImportBatch" ORDER BY "ImportedAt" DESC, rowid DESC LIMIT 1""").FirstOrDefault();
        if (batch is null || batch.Id != batchId)
        {
            throw new DomainValidationException(AppLocale.Pick(
                "La última importación cambió. Vuelve a abrir Respaldo.",
                "The last import changed. Open Backup again."));
        }

        // Igual que TransactionRepository.DeleteAsync: el recibo de un movimiento borrado se borra también
        // (su imagen cae en cascada). Un movimiento importado no debería tener recibo; por si acaso.
        db.Execute(
            """
            DELETE FROM "Receipt" WHERE "Id" IN
                (SELECT "ReceiptId" FROM "Transactions" WHERE "ImportBatchId" = ? AND "ReceiptId" IS NOT NULL)
            """, batch.Id);
        var deleted = db.Execute("""DELETE FROM "Transactions" WHERE "ImportBatchId" = ?""", batch.Id);
        db.Execute("""DELETE FROM "ImportBatch" WHERE "Id" = ?""", batch.Id);
        return deleted;
    });

    private sealed class Writer(SQLiteConnection db, ImportPlan plan, DateTime now)
    {
        private readonly List<CategoryRecord> _categories = db.Table<CategoryRecord>().ToList();
        private readonly List<PaymentMethodRecord> _methods = db.Table<PaymentMethodRecord>().ToList();
        private readonly Dictionary<(string Key, TransactionType Type), CategoryChoice> _categoryChoices = [];
        private readonly Dictionary<string, PaymentMethodChoice> _methodChoices = [];
        private readonly Dictionary<(string Key, TransactionType Type), int> _resolvedCategories = [];
        private readonly Dictionary<(int Parent, string Key), int> _resolvedSubcategories = [];
        private readonly Dictionary<string, int> _resolvedMethods = [];
        private int _nextCategoryOrder;
        private int _nextMethodOrder;
        private int _categoriesCreated;
        private int _subcategoriesCreated;
        private int _methodsCreated;

        public ImportResult Run()
        {
            if (plan.Rows is not { Count: > 0 })
            {
                throw Error("No hay movimientos para importar.", "There are no transactions to import.");
            }

            _nextCategoryOrder = _categories.Count == 0 ? 0 : _categories.Max(c => c.SortOrder) + 1;
            _nextMethodOrder = _methods.Count == 0 ? 0 : _methods.Max(m => m.SortOrder) + 1;

            RegisterChoices();
            var fallbackExpense = ValidateFallback(plan.FallbackExpenseCategoryId, TransactionType.Expense);
            var fallbackIncome = ValidateFallback(plan.FallbackIncomeCategoryId, TransactionType.Income);

            var batch = new ImportBatchRecord
            {
                Id = Guid.NewGuid(),
                ImportedAt = now,
                FileName = CleanFileName(plan.FileName),
                RowCount = plan.Rows.Count,
            };
            // El lote va antes que los movimientos (clave foránea).
            db.Insert(batch);

            var records = new List<TransactionRecord>(plan.Rows.Count);
            foreach (var row in plan.Rows)
            {
                if (row.Amount.AmountMinor <= 0)
                {
                    throw Error($"Fila {row.RowNumber}: el monto debe ser mayor que cero.",
                        $"Row {row.RowNumber}: the amount must be greater than zero.");
                }

                var categoryId = ResolveCategory(row, fallbackExpense, fallbackIncome);
                records.Add(new TransactionRecord
                {
                    Id = Guid.NewGuid(),
                    Type = row.Type,
                    AmountMinor = row.Amount.AmountMinor,
                    Currency = row.Amount.Currency.ToString(),
                    OccurredAt = RecordMapping.ToLocalForStorage(row.OccurredAt),
                    CategoryId = categoryId,
                    PaymentMethodId = ResolvePaymentMethod(row.PaymentMethod),
                    Merchant = TrimToNull(row.Merchant),
                    Description = TrimToNull(row.Description),
                    Notes = TrimToNull(row.Notes),
                    Source = TransactionSource.Import,
                    ImportBatchId = batch.Id,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            db.InsertAll(records, runInTransaction: false);
            return new ImportResult(batch.Id, records.Count, _categoriesCreated, _subcategoriesCreated, _methodsCreated);
        }

        private void RegisterChoices()
        {
            foreach (var choice in plan.Categories)
            {
                if (choice.ExistingId is { } id)
                {
                    var category = _categories.FirstOrDefault(c => c.Id == id)
                        ?? throw Error("La categoría elegida no existe.", "The chosen category does not exist.");
                    if (category.IsArchived)
                    {
                        throw Error($"La categoría «{category.Name}» está archivada.", $"The category “{category.Name}” is archived.");
                    }

                    if (category.Kind != choice.Type)
                    {
                        throw Error($"La categoría «{category.Name}» no es del tipo de los movimientos.",
                            $"The category “{category.Name}” does not match the transaction type.");
                    }
                }

                _categoryChoices[(ImportText.Key(choice.Name), choice.Type)] = choice;
            }

            foreach (var choice in plan.PaymentMethods)
            {
                if (choice.ExistingId is { } id)
                {
                    var method = _methods.FirstOrDefault(m => m.Id == id)
                        ?? throw Error("El medio de pago elegido no existe.", "The chosen payment method does not exist.");
                    if (method.IsArchived)
                    {
                        throw Error($"El medio de pago «{method.Name}» está archivado.", $"The payment method “{method.Name}” is archived.");
                    }
                }

                _methodChoices[ImportText.Key(choice.Name)] = choice;
            }
        }

        private int? ValidateFallback(int? id, TransactionType type)
        {
            if (id is not { } value)
            {
                return null;
            }

            var category = _categories.FirstOrDefault(c => c.Id == value)
                ?? throw Error("La categoría para movimientos sin categoría no existe.", "The category for uncategorized transactions does not exist.");
            if (category.IsArchived || category.Kind != type)
            {
                throw Error($"La categoría «{category.Name}» no sirve para movimientos sin categoría.",
                    $"The category “{category.Name}” cannot be used for uncategorized transactions.");
            }

            return value;
        }

        private int ResolveCategory(ImportRow row, int? fallbackExpense, int? fallbackIncome)
        {
            var key = ImportText.Key(row.Category);
            if (key.Length > 0 && _categoryChoices.TryGetValue((key, row.Type), out var choice))
            {
                var parentId = _resolvedCategories.TryGetValue((key, row.Type), out var cached)
                    ? cached
                    : _resolvedCategories[(key, row.Type)] = choice.ExistingId ?? CreateOrReuseCategory(choice, row.Type);
                return ResolveSubcategory(parentId, row);
            }

            return (row.Type == TransactionType.Expense ? fallbackExpense : fallbackIncome)
                ?? throw Error("Elige una categoría para los movimientos sin categoría.",
                    "Choose a category for the transactions without a category.");
        }

        private int CreateOrReuseCategory(CategoryChoice choice, TransactionType type)
        {
            var name = ImportText.Clean(choice.Name)
                ?? throw Error("El nombre de la categoría es obligatorio.", "The category name is required.");
            var key = ImportText.Key(name);
            var existing = _categories.FirstOrDefault(c =>
                !c.IsArchived && c.ParentId is null && c.Kind == type && ImportText.Key(c.Name) == key);
            if (existing is not null)
            {
                return existing.Id;
            }

            var colors = DefaultData.SpanishCategories;
            var record = new CategoryRecord
            {
                Name = name,
                Kind = type,
                Icon = NewCategoryIcon,
                Color = colors[_categoriesCreated % colors.Count].Color,
                SortOrder = _nextCategoryOrder++,
            };
            db.Insert(record);
            _categories.Add(record);
            _categoriesCreated++;
            return record.Id;
        }

        private int ResolveSubcategory(int parentId, ImportRow row)
        {
            var key = ImportText.Key(row.Subcategory);
            if (key.Length == 0)
            {
                return parentId;
            }

            if (_resolvedSubcategories.TryGetValue((parentId, key), out var cached))
            {
                return cached;
            }

            var parent = _categories.First(c => c.Id == parentId);
            if (parent.ParentId is not null)
            {
                // Solo un nivel: si la elegida ya es subcategoría, se ignora la subcategoría del archivo.
                return _resolvedSubcategories[(parentId, key)] = parentId;
            }

            var name = ImportText.Clean(row.Subcategory)!;
            var domain = _categories.Where(c => c.ParentId == parentId).Select(c => c.ToDomain());
            var id = ImportCategoryMatcher.FindSubcategory(name, parentId, row.Type, domain);
            if (id is null)
            {
                var record = new CategoryRecord
                {
                    Name = name,
                    Kind = parent.Kind,
                    Icon = parent.Icon,
                    Color = parent.Color,
                    ParentId = parentId,
                    SortOrder = _nextCategoryOrder++,
                };
                db.Insert(record);
                _categories.Add(record);
                _subcategoriesCreated++;
                id = record.Id;
            }

            return _resolvedSubcategories[(parentId, key)] = id.Value;
        }

        private int? ResolvePaymentMethod(string? name)
        {
            var key = ImportText.Key(name);
            if (key.Length == 0)
            {
                return null;
            }

            if (_resolvedMethods.TryGetValue(key, out var cached))
            {
                return cached;
            }

            _methodChoices.TryGetValue(key, out var choice);
            if (choice?.ExistingId is { } existingId)
            {
                return _resolvedMethods[key] = existingId;
            }

            var reuse = _methods.FirstOrDefault(m => !m.IsArchived && ImportText.Key(m.Name) == key);
            if (reuse is not null)
            {
                return _resolvedMethods[key] = reuse.Id;
            }

            var record = new PaymentMethodRecord
            {
                Name = ImportText.Clean(choice?.Name) ?? ImportText.Clean(name)!,
                Kind = PaymentMethodKind.Other,
                Icon = NewPaymentMethodIcon,
                SortOrder = _nextMethodOrder++,
            };
            db.Insert(record);
            _methods.Add(record);
            _methodsCreated++;
            return _resolvedMethods[key] = record.Id;
        }

        private static string? CleanFileName(string? fileName)
        {
            var name = ImportText.Clean(Path.GetFileName(fileName ?? ""));
            return name is null ? null : name.Length > MaxFileNameLength ? name[..MaxFileNameLength] : name;
        }

        private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static DomainValidationException Error(string es, string en) => new(AppLocale.Pick(es, en));
    }
}
