using System.Diagnostics;
using PiRadar.Core.Domain;
using PiRadar.Core.Import;
using PiRadar.Data.Repositories;
using PiRadar.Data.Seed;

namespace PiRadar.Data.Tests.Repositories;

public class TransactionImporterTests
{
    private static readonly DateTime Day = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Local);

    private static ImportRow Row(int n, long amount = 5000, string? category = null, string? sub = null, string? method = null,
        TransactionType type = TransactionType.Expense, string? description = "Compra", DateTime? at = null,
        Currency currency = Currency.COP) =>
        new(n, type, new Money(amount, currency), at ?? Day, category, sub, method, description, "Tienda", "nota", false, false);

    private static ImportPlan Plan(IReadOnlyList<ImportRow> rows, IReadOnlyList<CategoryChoice>? categories = null,
        IReadOnlyList<PaymentMethodChoice>? methods = null, int? fallbackExpense = null, int? fallbackIncome = null,
        string? fileName = "gastos.xlsx") =>
        new(fileName, rows, categories ?? [], methods ?? [], fallbackExpense, fallbackIncome);

    private static async Task<int> CountAsync(TempHost t, string table) =>
        await t.Host.Database.Connection.ExecuteScalarAsync<int>($"SELECT count(*) FROM \"{table}\"");

    private static TransactionImporter Importer(TempHost t) => new(t.Host, t.Clock);

    private static async Task<int> OtherExpensesAsync(TempHost t) =>
        (await new CategoryRepository(t.Host).GetAllAsync(TransactionType.Expense)).Single(c => c.Name == "Otros gastos").Id;

    private static async Task<(int Cats, int Methods, int Txs, int Batches)> SnapshotAsync(TempHost t) =>
        (await CountAsync(t, "Category"), await CountAsync(t, "PaymentMethod"), await CountAsync(t, "Transactions"), await CountAsync(t, "ImportBatch"));

    [Fact]
    public async Task Import_creates_a_new_category_once_even_with_different_spellings()
    {
        await using var t = await TempHost.OpenAsync();
        var rows = new[] { Row(2, category: "Gimnasio"), Row(3, category: "GIMNASIO "), Row(4, category: "gímnasio") };
        var choices = new[] { new CategoryChoice("Gimnasio", TransactionType.Expense, null) };

        var result = await Importer(t).ImportAsync(Plan(rows, choices, fallbackExpense: await OtherExpensesAsync(t)));

        Assert.Equal(3, result.Imported);
        Assert.Equal(1, result.CategoriesCreated);
        var categories = await new CategoryRepository(t.Host).GetAllAsync();
        var gym = Assert.Single(categories, c => c.Name == "Gimnasio");
        Assert.False(gym.IsSystem);
        Assert.Equal("more_horiz", gym.Icon);
        var all = await new TransactionRepository(t.Host, t.Clock).QueryAsync(new TransactionFilter());
        Assert.All(all, tx => Assert.Equal(gym.Id, tx.CategoryId));
    }

    [Fact]
    public async Task Import_joins_existing_category_and_uses_fallback_for_rows_without_one()
    {
        await using var t = await TempHost.OpenAsync();
        var categories = await new CategoryRepository(t.Host).GetAllAsync();
        var market = categories.Single(c => c.Name == "Mercado");
        var otherExpenses = await OtherExpensesAsync(t);
        var otherIncome = categories.Single(c => c.Name == "Otros ingresos").Id;
        var rows = new[]
        {
            Row(2, category: "mercado"), Row(3, category: null), Row(4, category: "Desconocida"),
            Row(5, type: TransactionType.Income, category: null, amount: 900),
        };
        var plan = Plan(rows, [new CategoryChoice("Mercado", TransactionType.Expense, market.Id)],
            fallbackExpense: otherExpenses, fallbackIncome: otherIncome);

        var before = await CountAsync(t, "Category");
        var result = await Importer(t).ImportAsync(plan);

        Assert.Equal(0, result.CategoriesCreated);
        Assert.Equal(before, await CountAsync(t, "Category"));
        var byDescription = (await new TransactionRepository(t.Host, t.Clock).QueryAsync(new TransactionFilter()))
            .OrderBy(x => x.Amount.AmountMinor).ToList();
        Assert.Equal(otherIncome, byDescription[0].CategoryId);
        Assert.Equal(1, byDescription.Count(x => x.CategoryId == market.Id));
        Assert.Equal(2, byDescription.Count(x => x.CategoryId == otherExpenses));
    }

    [Fact]
    public async Task Import_creates_subcategories_once_under_the_parent()
    {
        await using var t = await TempHost.OpenAsync();
        var restaurants = (await new CategoryRepository(t.Host).GetAllAsync()).Single(c => c.Name == "Restaurantes");
        var rows = new[]
        {
            Row(2, category: "Restaurantes", sub: "Café"), Row(3, category: "Restaurantes", sub: "CAFE"),
            Row(4, category: "Restaurantes", sub: "Domicilios"), Row(5, category: "Restaurantes"),
        };

        var result = await Importer(t).ImportAsync(Plan(rows, [new CategoryChoice("Restaurantes", TransactionType.Expense, restaurants.Id)]));

        Assert.Equal(2, result.SubcategoriesCreated);
        var all = await new CategoryRepository(t.Host).GetAllAsync();
        var coffee = Assert.Single(all, c => c.Name == "Café");
        Assert.Equal(restaurants.Id, coffee.ParentId);
        Assert.Equal(restaurants.Color, coffee.Color);
        var txs = await new TransactionRepository(t.Host, t.Clock).QueryAsync(new TransactionFilter { CategoryId = coffee.Id });
        Assert.Equal(2, txs.Count);
    }

    [Fact]
    public async Task Import_reuses_an_existing_subcategory_and_ignores_sub_of_sub()
    {
        await using var t = await TempHost.OpenAsync();
        var categories = new CategoryRepository(t.Host);
        var restaurants = (await categories.GetAllAsync()).Single(c => c.Name == "Restaurantes");
        var coffee = await categories.AddAsync(new Category { Name = "Café", Kind = TransactionType.Expense, Icon = "local_cafe", Color = "#6D4C41", ParentId = restaurants.Id });
        var rows = new[] { Row(2, category: "Restaurantes", sub: "café"), Row(3, category: "Cafe", sub: "Otra") };
        var plan = Plan(rows, [
            new CategoryChoice("Restaurantes", TransactionType.Expense, restaurants.Id),
            new CategoryChoice("Cafe", TransactionType.Expense, coffee.Id)]);

        var result = await Importer(t).ImportAsync(plan);

        Assert.Equal(0, result.SubcategoriesCreated);
        var txs = await new TransactionRepository(t.Host, t.Clock).QueryAsync(new TransactionFilter { CategoryId = coffee.Id });
        Assert.Equal(2, txs.Count);
    }

    [Fact]
    public async Task Import_creates_and_reuses_payment_methods()
    {
        await using var t = await TempHost.OpenAsync();
        var cash = (await new PaymentMethodRepository(t.Host).GetAllAsync()).Single(p => p.Name == "Efectivo");
        var other = await OtherExpensesAsync(t);
        var rows = new[]
        {
            Row(2, method: "Efectivo"), Row(3, method: "Tarjeta Éxito"), Row(4, method: "TARJETA EXITO"), Row(5, method: null),
            Row(6, method: "Sin choice"),
        };
        var plan = Plan(rows, methods: [new PaymentMethodChoice("Efectivo", cash.Id), new PaymentMethodChoice("Tarjeta Éxito", null)], fallbackExpense: other);

        var result = await Importer(t).ImportAsync(plan);

        Assert.Equal(2, result.PaymentMethodsCreated);
        var methods = await new PaymentMethodRepository(t.Host).GetAllAsync();
        var created = Assert.Single(methods, m => m.Name == "Tarjeta Éxito");
        Assert.Equal(PaymentMethodKind.Other, created.Kind);
        Assert.False(created.IsSystem);
        var txs = await new TransactionRepository(t.Host, t.Clock).QueryAsync(new TransactionFilter());
        Assert.Equal(1, txs.Count(x => x.PaymentMethodId == cash.Id));
        Assert.Equal(2, txs.Count(x => x.PaymentMethodId == created.Id));
        Assert.Equal(1, txs.Count(x => x.PaymentMethodId is null));
    }

    [Fact]
    public async Task Import_marks_source_batch_and_keeps_totals_and_dates()
    {
        await using var t = await TempHost.OpenAsync();
        var other = await OtherExpensesAsync(t);
        var rows = new[] { Row(2, 1000), Row(3, 2550, currency: Currency.USD), Row(4, 70) };
        var plan = Plan(rows, fallbackExpense: other, fileName: "/ruta/larga/gastos agosto.xlsx");

        var result = await Importer(t).ImportAsync(plan);

        Assert.Equal(3, result.Imported);
        var txs = await new TransactionRepository(t.Host, t.Clock).QueryAsync(new TransactionFilter());
        Assert.All(txs, x =>
        {
            Assert.Equal(TransactionSource.Import, x.Source);
            Assert.Equal(Day, x.OccurredAt);
            Assert.Equal(DateTimeKind.Local, x.OccurredAt.Kind);
            Assert.Equal(t.Clock.UtcNow, x.CreatedAt);
            Assert.Equal("Tienda", x.Merchant);
            Assert.Equal("nota", x.Notes);
        });
        Assert.Equal(1070, txs.Where(x => x.Amount.Currency == Currency.COP).Sum(x => x.Amount.AmountMinor));
        Assert.Equal(2550, txs.Single(x => x.Amount.Currency == Currency.USD).Amount.AmountMinor);
        var batch = await Importer(t).GetLastImportAsync();
        Assert.NotNull(batch);
        Assert.Equal(result.BatchId, batch.Id);
        Assert.Equal("gastos agosto.xlsx", batch.FileName);
        Assert.Equal(3, batch.RowCount);
        Assert.Equal(3, batch.RemainingCount);
        Assert.Equal(t.Clock.UtcNow, batch.ImportedAt);
    }

    [Fact]
    public async Task Imported_rows_are_found_as_duplicates_afterwards()
    {
        await using var t = await TempHost.OpenAsync();
        var rows = new[] { Row(2, 1000, description: "Uno"), Row(3, 2000, description: "Dos", at: Day.AddDays(1)) };
        await Importer(t).ImportAsync(Plan(rows, fallbackExpense: await OtherExpensesAsync(t)));

        var stored = await new TransactionRepository(t.Host, t.Clock).QueryAsync(new TransactionFilter());

        Assert.Equal(new HashSet<int> { 2, 3 }, ImportDuplicates.Find(rows, stored));
    }

    [Fact]
    public async Task Invalid_plans_fail_and_leave_the_database_untouched()
    {
        await using var t = await TempHost.OpenAsync();
        var categories = new CategoryRepository(t.Host);
        var archived = await categories.AddAsync(new Category { Name = "Vieja", Kind = TransactionType.Expense, Icon = "home", Color = "#112233" });
        await new TransactionRepository(t.Host, t.Clock).AddAsync(TestData.Expense(archived.Id));
        await categories.DeleteOrArchiveAsync(archived.Id);
        var income = (await categories.GetAllAsync(TransactionType.Income)).First();
        var other = await OtherExpensesAsync(t);
        var archivedMethod = await new PaymentMethodRepository(t.Host).AddAsync(new PaymentMethod { Name = "Vieja", Kind = PaymentMethodKind.Other, Icon = "payments" });
        await new TransactionRepository(t.Host, t.Clock).AddAsync(TestData.Expense(other, paymentMethodId: archivedMethod.Id));
        await new PaymentMethodRepository(t.Host).DeleteOrArchiveAsync(archivedMethod.Id);
        var before = await SnapshotAsync(t);
        var rows = new[] { Row(2, category: "Nueva") };
        var newChoice = new CategoryChoice("Nueva", TransactionType.Expense, null);

        var plans = new[]
        {
            Plan([], fallbackExpense: other),
            Plan(rows, [newChoice, new CategoryChoice("X", TransactionType.Expense, 99999)], fallbackExpense: other),
            Plan(rows, [newChoice, new CategoryChoice("X", TransactionType.Expense, archived.Id)], fallbackExpense: other),
            Plan(rows, [newChoice, new CategoryChoice("X", TransactionType.Expense, income.Id)], fallbackExpense: other),
            Plan(rows, [newChoice], methods: [new PaymentMethodChoice("X", 99999)], fallbackExpense: other),
            Plan(rows, [newChoice], methods: [new PaymentMethodChoice("X", archivedMethod.Id)], fallbackExpense: other),
            Plan(rows, [newChoice], fallbackExpense: income.Id),
            Plan(rows, [newChoice], fallbackExpense: archived.Id),
            Plan([Row(2, category: "Nueva"), Row(3)], [newChoice], fallbackExpense: null),
            // Falla a mitad de la escritura: la categoría nueva ya se había creado cuando la fila 3 tiene monto 0.
            Plan([Row(2, category: "Nueva"), Row(3, amount: 0)], [newChoice], fallbackExpense: other),
        };

        foreach (var plan in plans)
        {
            await Assert.ThrowsAsync<DomainValidationException>(() => Importer(t).ImportAsync(plan));
            Assert.Equal(before, await SnapshotAsync(t));
        }

        Assert.Null(await Importer(t).GetLastImportAsync());
    }

    [Fact]
    public async Task Error_messages_follow_the_language()
    {
        await using var t = await TempHost.OpenAsync();
        using var _ = AppLocale.Use(AppLanguage.English);

        var empty = await Assert.ThrowsAsync<DomainValidationException>(() => Importer(t).ImportAsync(Plan([])));
        var missing = await Assert.ThrowsAsync<DomainValidationException>(() => Importer(t).ImportAsync(Plan([Row(2)])));

        Assert.Equal("There are no transactions to import.", empty.Message);
        Assert.Equal("Choose a category for the transactions without a category.", missing.Message);
    }

    [Fact]
    public async Task Undo_removes_batches_from_newest_to_oldest_without_touching_manual_transactions()
    {
        await using var t = await TempHost.OpenAsync();
        var other = await OtherExpensesAsync(t);
        var repo = new TransactionRepository(t.Host, t.Clock);
        var importer = Importer(t);
        var manual = await repo.AddAsync(TestData.Expense(other, 777));
        var first = await importer.ImportAsync(Plan([Row(2), Row(3)], fallbackExpense: other, fileName: "uno.csv"));
        t.Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await importer.ImportAsync(Plan([Row(2), Row(3), Row(4)], fallbackExpense: other, fileName: "dos.csv"));

        Assert.Equal(second.BatchId, (await importer.GetLastImportAsync())!.Id);
        Assert.Equal(3, await importer.UndoLastImportAsync(second.BatchId));
        var info = await importer.GetLastImportAsync();
        Assert.Equal(first.BatchId, info!.Id);
        Assert.Equal(2, info.RemainingCount);
        Assert.Equal(3, await CountAsync(t, "Transactions"));

        Assert.Equal(2, await importer.UndoLastImportAsync(first.BatchId));
        Assert.Null(await importer.GetLastImportAsync());
        await Assert.ThrowsAsync<DomainValidationException>(() => importer.UndoLastImportAsync(first.BatchId));
        Assert.NotNull(await repo.GetAsync(manual.Id));
        Assert.Equal(1, await CountAsync(t, "Transactions"));
        Assert.Equal(0, await CountAsync(t, "ImportBatch"));
    }

    [Fact]
    public async Task Editing_or_deleting_imported_transactions_keeps_the_batch_consistent()
    {
        await using var t = await TempHost.OpenAsync();
        var other = await OtherExpensesAsync(t);
        var repo = new TransactionRepository(t.Host, t.Clock);
        var importer = Importer(t);
        var imported = await importer.ImportAsync(Plan([Row(2, 1000), Row(3, 2000), Row(4, 3000)], fallbackExpense: other));
        var txs = await repo.QueryAsync(new TransactionFilter());

        var edited = txs[0];
        edited.Notes = "cambiada";
        edited.Amount = new Money(9999, Currency.COP);
        var saved = await repo.UpdateAsync(edited);
        Assert.Equal(TransactionSource.Import, saved.Source);
        Assert.Equal(1, await t.Host.Database.Connection.ExecuteScalarAsync<int>(
            """SELECT count(*) FROM "Transactions" WHERE "Id" = ? AND "ImportBatchId" IS NOT NULL""", edited.Id));
        Assert.Equal(3, (await importer.GetLastImportAsync())!.RemainingCount);

        Assert.True(await repo.DeleteAsync(txs[1].Id));
        var info = await importer.GetLastImportAsync();
        Assert.Equal(3, info!.RowCount);
        Assert.Equal(2, info.RemainingCount);

        Assert.Equal(2, await importer.UndoLastImportAsync(imported.BatchId));
        Assert.Equal(0, await CountAsync(t, "Transactions"));
    }

    [Fact]
    public async Task Twenty_thousand_rows_import_quickly()
    {
        await using var t = await TempHost.OpenAsync();
        var other = await OtherExpensesAsync(t);
        var rows = Enumerable.Range(2, 20_000)
            .Select(i => Row(i, 100 + i, category: $"Cat {i % 30}", sub: i % 3 == 0 ? $"Sub {i % 7}" : null,
                method: $"Medio {i % 5}", at: Day.AddMinutes(i)))
            .ToList();
        var choices = Enumerable.Range(0, 30).Select(i => new CategoryChoice($"Cat {i}", TransactionType.Expense, null)).ToList();

        var watch = Stopwatch.StartNew();
        var result = await Importer(t).ImportAsync(Plan(rows, choices, fallbackExpense: other));
        watch.Stop();

        Assert.Equal(20_000, result.Imported);
        Assert.Equal(30, result.CategoriesCreated);
        Assert.Equal(5, result.PaymentMethodsCreated);
        Assert.Equal(20_000, await CountAsync(t, "Transactions"));
        Console.WriteLine($"20 000 filas importadas en {watch.ElapsedMilliseconds} ms");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), $"Tardó {watch.Elapsed}");
    }

    [Fact]
    public async Task Undo_refuses_when_the_latest_batch_is_not_the_confirmed_one()
    {
        await using var t = await TempHost.OpenAsync();
        var other = await OtherExpensesAsync(t);
        var importer = Importer(t);
        var first = await importer.ImportAsync(Plan([Row(2), Row(3)], fallbackExpense: other, fileName: "uno.csv"));
        t.Clock.Advance(TimeSpan.FromMinutes(5));
        await importer.ImportAsync(Plan([Row(2)], fallbackExpense: other, fileName: "dos.csv"));
        var before = await SnapshotAsync(t);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => importer.UndoLastImportAsync(first.BatchId));
        Assert.Contains("cambió", ex.Message);
        await Assert.ThrowsAsync<DomainValidationException>(() => importer.UndoLastImportAsync(Guid.NewGuid()));
        Assert.Equal(before, await SnapshotAsync(t));
    }

    [Fact]
    public async Task Rows_without_a_real_category_name_import_with_the_fallback()
    {
        await using var t = await TempHost.OpenAsync();
        var other = await OtherExpensesAsync(t);
        var sheet = ImportRowParser.Parse(
            new SheetData("S", [[CellValue.FromDate(Day), CellValue.FromNumber(-5000), CellValue.FromText("-"), CellValue.FromText("\U0001F355")]], 4),
            new ImportOptions(new ImportMapping(null, [ImportField.Date, ImportField.Amount, ImportField.Category, ImportField.PaymentMethod]),
                DateOrder.DayFirst, Currency.COP, true));
        Assert.Empty(sheet.Errors);
        var (needExpense, _) = ImportWizard.FallbacksNeeded(sheet.Rows);
        Assert.True(needExpense);

        var plan = ImportWizard.BuildPlan("a.csv", sheet.Rows, [], [], other, null);
        var result = await Importer(t).ImportAsync(plan);

        Assert.Equal(1, result.Imported);
        var tx = Assert.Single(await new TransactionRepository(t.Host, t.Clock).QueryAsync(new TransactionFilter()));
        Assert.Equal(other, tx.CategoryId);
        Assert.Null(tx.PaymentMethodId);
    }
}
