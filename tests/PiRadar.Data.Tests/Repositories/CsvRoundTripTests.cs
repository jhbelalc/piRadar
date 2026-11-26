using PiRadar.Core.Domain;
using PiRadar.Core.Export;
using PiRadar.Core.Import;
using PiRadar.Data.Repositories;

namespace PiRadar.Data.Tests.Repositories;

public class CsvRoundTripTests
{
    private sealed record Key(DateTime At, TransactionType Type, long Minor, Currency Currency, string? Category,
        string? Sub, string? Method, string? Merchant, string? Description, string? Notes);

    private static async Task<List<Key>> ReadAllAsync(TempHost t)
    {
        var cats = (await new CategoryRepository(t.Host).GetAllAsync(includeArchived: true)).ToDictionary(c => c.Id);
        var methods = (await new PaymentMethodRepository(t.Host).GetAllAsync(includeArchived: true)).ToDictionary(p => p.Id);
        var all = await new TransactionRepository(t.Host, t.Clock).QueryAsync(new TransactionFilter());
        return all.Select(x =>
        {
            var c = cats[x.CategoryId];
            var parent = c.ParentId is { } p ? cats[p] : null;
            return new Key(x.OccurredAt, x.Type, x.Amount.AmountMinor, x.Amount.Currency,
                parent?.Name ?? c.Name, parent is null ? null : c.Name,
                x.PaymentMethodId is { } m ? methods[m].Name : null, x.Merchant, x.Description, x.Notes);
        }).OrderBy(k => k.At).ThenBy(k => k.Minor).ThenBy(k => k.Description).ToList();
    }

    private static async Task SeedAsync(TempHost t, string? notes)
    {
        var cats = new CategoryRepository(t.Host);
        var food = await cats.AddAsync(new Category { Name = "Comida Zeta", Kind = TransactionType.Expense, Icon = "x", Color = "#112233" });
        var cafe = await cats.AddAsync(new Category { Name = "Cafetería Zeta", Kind = TransactionType.Expense, Icon = "x", Color = "#112233", ParentId = food.Id });
        var pay = await cats.AddAsync(new Category { Name = "Pago Zeta", Kind = TransactionType.Income, Icon = "x", Color = "#112233" });
        var card = await new PaymentMethodRepository(t.Host).AddAsync(new PaymentMethod { Name = "Tarjeta Zeta", Icon = "x", Kind = PaymentMethodKind.CreditCard });
        var repo = new TransactionRepository(t.Host, t.Clock);

        Transaction Make(TransactionType type, long minor, Currency cur, int cat, int? method, DateTime at, string? merchant,
            string? description, string? n) => new()
        {
            Type = type, Amount = new Money(minor, cur), CategoryId = cat, PaymentMethodId = method,
            OccurredAt = DateTime.SpecifyKind(at, DateTimeKind.Local), Merchant = merchant, Description = description, Notes = n,
        };

        await repo.AddAsync(Make(TransactionType.Expense, 1234567, Currency.COP, food.Id, card.Id, new(2026, 3, 5, 14, 7, 0), "Tienda; Uno", "Almuerzo", notes));
        await repo.AddAsync(Make(TransactionType.Expense, 1250, Currency.USD, cafe.Id, null, new(2026, 3, 6, 0, 5, 0), "Café \"Sol\"", "=SUMA(1)", null));
        await repo.AddAsync(Make(TransactionType.Income, 90000, Currency.COP, pay.Id, card.Id, new(2026, 3, 7, 23, 59, 0), null, "Pago", "a, b"));
        await repo.AddAsync(Make(TransactionType.Income, 100000, Currency.USD, pay.Id, null, new(2026, 4, 1, 9, 30, 0), null, "+nota", null));
        await repo.AddAsync(Make(TransactionType.Expense, 5, Currency.USD, food.Id, null, new(2026, 4, 2, 9, 30, 0), null, null, null));
    }

    private static async Task RunAsync(AppLanguage language, string? notes)
    {
        using var _ = AppLocale.Use(language);
        await using var source = await TempHost.OpenAsync();
        await SeedAsync(source, notes);
        var expected = await ReadAllAsync(source);

        using var ms = new MemoryStream();
        TransactionCsvExporter.Write(ms,
            await new TransactionRepository(source.Host, source.Clock).QueryAsync(new TransactionFilter()),
            await new CategoryRepository(source.Host).GetAllAsync(includeArchived: true),
            await new PaymentMethodRepository(source.Host).GetAllAsync(includeArchived: true));
        ms.Position = 0;

        var sheet = CsvReader.Read(ms);
        var header = HeaderDetector.Find(sheet);
        var mapping = ImportColumnGuesser.Guess(sheet, header);
        Assert.Equal(AmountMode.AmountWithType, mapping.AmountMode);
        foreach (var f in new[] { ImportField.Date, ImportField.Time, ImportField.Type, ImportField.Amount, ImportField.Currency,
            ImportField.Category, ImportField.Subcategory, ImportField.PaymentMethod, ImportField.Merchant,
            ImportField.Description, ImportField.Notes })
            Assert.NotNull(mapping.ColumnOf(f));

        var preview = ImportRowParser.Parse(sheet, new ImportOptions(mapping, ImportRowParser.SuggestDateOrder(sheet, mapping), Currency.COP));
        Assert.Empty(preview.Errors);

        await using var target = await TempHost.OpenAsync();
        var plan = new ImportPlan("x.csv", preview.Rows,
            preview.Categories.Select(c => new CategoryChoice(c.Name, c.Type, null)).ToList(),
            preview.PaymentMethods.Select(p => new PaymentMethodChoice(p.Name, null)).ToList(), null, null);
        var result = await new TransactionImporter(target.Host, target.Clock).ImportAsync(plan);
        Assert.Equal(expected.Count, result.Imported);

        Assert.Equal(expected, await ReadAllAsync(target));
    }

    [Fact]
    public Task Round_trip_in_Spanish() => RunAsync(AppLanguage.Spanish, "nota \"uno\"; dos");

    [Fact]
    public Task Round_trip_in_English() => RunAsync(AppLanguage.English, "note \"one\", two");

    [Fact]
    public async Task Notes_with_line_breaks_collapse_to_one_line_on_import()
    {
        await using var source = await TempHost.OpenAsync();
        await SeedAsync(source, "linea 1\r\nlinea 2");
        using var ms = new MemoryStream();
        TransactionCsvExporter.Write(ms,
            await new TransactionRepository(source.Host, source.Clock).QueryAsync(new TransactionFilter()),
            await new CategoryRepository(source.Host).GetAllAsync(includeArchived: true),
            await new PaymentMethodRepository(source.Host).GetAllAsync(includeArchived: true));
        ms.Position = 0;
        var sheet = CsvReader.Read(ms);
        var mapping = ImportColumnGuesser.Guess(sheet, HeaderDetector.Find(sheet));
        var preview = ImportRowParser.Parse(sheet, new ImportOptions(mapping, DateOrder.DayFirst, Currency.COP));
        Assert.Contains(preview.Rows, r => r.Notes == "linea 1 linea 2");
    }
}
