using System.Text.RegularExpressions;
using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;
using PiRadar.Data.Records;
using SQLite;

namespace PiRadar.Data.Repositories;

internal sealed partial class CategoryRepository(DatabaseHost host) : RepositoryBase(host), ICategoryRepository
{
    public async Task<IReadOnlyList<Category>> GetAllAsync(TransactionType? kind = null, bool includeArchived = false)
    {
        var query = Connection.Table<CategoryRecord>();
        if (kind is { } k)
        {
            query = query.Where(c => c.Kind == k);
        }

        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        var records = await query.OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync();
        return records.Select(r => r.ToDomain()).ToList();
    }

    public async Task<Category?> GetAsync(int id) =>
        (await Connection.FindAsync<CategoryRecord>(id))?.ToDomain();

    public Task<Category> AddAsync(Category category) => InTransactionAsync(db =>
    {
        var record = category.ToRecord();
        record.Id = 0;
        record.IsSystem = false;
        Validate(db, record);
        record.SortOrder = db.ExecuteScalar<int>("""SELECT coalesce(max("SortOrder"), -1) + 1 FROM "Category" """);
        db.Insert(record);
        return record.ToDomain();
    });

    public Task UpdateAsync(Category category) => InTransactionAsync(db =>
    {
        var stored = db.Find<CategoryRecord>(category.Id)
            ?? throw new KeyNotFoundException($"No existe la categoría {category.Id}.");

        var record = category.ToRecord();
        record.IsSystem = stored.IsSystem;
        Validate(db, record);

        if (record.Kind != stored.Kind && HasTransactions(db, record.Id))
        {
            throw new DomainValidationException(
                AppLocale.Pick("No se puede cambiar entre gasto e ingreso una categoría que ya tiene movimientos.", "A category that already has transactions cannot switch between expense and income."));
        }

        if (record.Kind != stored.Kind
            && Exists(db, """SELECT 1 FROM "Category" WHERE "ParentId" = ?""", record.Id))
        {
            throw new DomainValidationException(
                AppLocale.Pick("No se puede cambiar entre gasto e ingreso una categoría que tiene subcategorías.", "A category that has subcategories cannot switch between expense and income."));
        }

        db.Update(record);
        return true;
    });

    public Task<RemovalResult> DeleteOrArchiveAsync(int id) => InTransactionAsync(db =>
    {
        var stored = db.Find<CategoryRecord>(id) ?? throw new KeyNotFoundException($"No existe la categoría {id}.");

        if (HasTransactions(db, id) || Exists(db, """SELECT 1 FROM "Category" WHERE "ParentId" = ?""", id))
        {
            stored.IsArchived = true;
            db.Update(stored);
            return RemovalResult.Archived;
        }

        db.Delete<CategoryRecord>(id);
        return RemovalResult.Deleted;
    });

    private static bool HasTransactions(SQLiteConnection db, int id) =>
        Exists(db, """SELECT 1 FROM "Transactions" WHERE "CategoryId" = ?""", id);

    private static void Validate(SQLiteConnection db, CategoryRecord record)
    {
        record.Name = RequireName(record.Name, "la categoría", "category");
        if (!Enum.IsDefined(record.Kind))
        {
            throw new DomainValidationException(AppLocale.Pick("Tipo de categoría no válido.", "Invalid category type."));
        }

        if (string.IsNullOrWhiteSpace(record.Icon))
        {
            throw new DomainValidationException(AppLocale.Pick("La categoría necesita un ícono.", "The category needs an icon."));
        }

        if (!HexColor().IsMatch(record.Color))
        {
            throw new DomainValidationException(AppLocale.Pick("El color debe tener el formato #RRGGBB.", "The color must use the #RRGGBB format."));
        }

        record.Color = record.Color.ToUpperInvariant();

        if (record.ParentId is { } parentId)
        {
            var parent = db.Find<CategoryRecord>(parentId);
            if (parent is null || parentId == record.Id)
            {
                throw new DomainValidationException(AppLocale.Pick("La categoría padre no es válida.", "The parent category is not valid."));
            }

            if (parent.ParentId is not null)
            {
                throw new DomainValidationException(AppLocale.Pick("Solo se admite un nivel de subcategorías.", "Only one level of subcategories is allowed."));
            }

            if (parent.Kind != record.Kind)
            {
                throw new DomainValidationException(AppLocale.Pick("La subcategoría debe ser del mismo tipo que su categoría padre.", "A subcategory must be the same type as its parent category."));
            }

            if (record.Id != 0 && Exists(db, """SELECT 1 FROM "Category" WHERE "ParentId" = ?""", record.Id))
            {
                throw new DomainValidationException(AppLocale.Pick("Una categoría con subcategorías no puede ser subcategoría.", "A category with subcategories cannot be a subcategory."));
            }
        }

        if (!record.IsArchived)
        {
            var siblings = db.Table<CategoryRecord>()
                .Where(c => c.Kind == record.Kind && !c.IsArchived && c.Id != record.Id)
                .ToList();
            if (siblings.Any(c => c.ParentId == record.ParentId
                && string.Equals(c.Name, record.Name, StringComparison.CurrentCultureIgnoreCase)))
            {
                throw new DomainValidationException(AppLocale.Pick($"Ya existe una categoría llamada «{record.Name}».", $"A category named “{record.Name}” already exists."));
            }
        }
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
