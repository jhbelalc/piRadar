using PiRadar.Core.Domain;
using PiRadar.Data.Records;
using PiRadar.Data.Repositories;

namespace PiRadar.Data.Tests.Repositories;

public class TransactionRepositoryTests
{
    [Fact]
    public async Task Add_GeneratesId_SetsUtcAuditDates_AndRoundTrips()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        var input = TestData.Expense(market.Id);
        input.Notes = "  ";

        var saved = await repo.AddAsync(input);
        var loaded = await repo.GetAsync(saved.Id);

        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.NotNull(loaded);
        Assert.Equal(temp.Clock.UtcNow, loaded.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.Equal(temp.Clock.UtcNow, loaded.UpdatedAt);
        Assert.Equal(input.OccurredAt, loaded.OccurredAt);
        Assert.Equal(DateTimeKind.Local, loaded.OccurredAt.Kind);
        Assert.Equal(new Money(82150, Currency.COP), loaded.Amount);
        Assert.Equal(TransactionSource.Manual, loaded.Source);
        Assert.Null(loaded.Notes);
    }

    [Fact]
    public async Task Add_KeepsGivenId()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        var input = TestData.Expense(market.Id);
        input.Id = Guid.NewGuid();

        Assert.Equal(input.Id, (await repo.AddAsync(input)).Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Add_RejectsNonPositiveAmounts(long amountMinor)
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => repo.AddAsync(TestData.Expense(market.Id, amountMinor)));
    }

    [Fact]
    public async Task Add_RejectsCategoryOfOtherKind()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, _, salary) = await SetupAsync(temp);

        await Assert.ThrowsAsync<DomainValidationException>(() => repo.AddAsync(TestData.Expense(salary.Id)));
    }

    [Fact]
    public async Task Add_RejectsUnknownCategoryOrPaymentMethod()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);

        await Assert.ThrowsAsync<DomainValidationException>(() => repo.AddAsync(TestData.Expense(9999)));
        await Assert.ThrowsAsync<DomainValidationException>(
            () => repo.AddAsync(TestData.Expense(market.Id, paymentMethodId: 9999)));
    }

    [Fact]
    public async Task Archived_CategoryAndMethod_CannotBeChosen_ButOldTransactionsCanBeEdited()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        var methods = new PaymentMethodRepository(temp.Host);
        var cash = (await methods.GetAllAsync())[0];
        var existing = await repo.AddAsync(TestData.Expense(market.Id, paymentMethodId: cash.Id));

        await new CategoryRepository(temp.Host).DeleteOrArchiveAsync(market.Id);
        await methods.DeleteOrArchiveAsync(cash.Id);

        await Assert.ThrowsAsync<DomainValidationException>(() => repo.AddAsync(TestData.Expense(market.Id)));
        var other = (await new CategoryRepository(temp.Host).GetAllAsync(TransactionType.Expense))[0];
        await Assert.ThrowsAsync<DomainValidationException>(
            () => repo.AddAsync(TestData.Expense(other.Id, paymentMethodId: cash.Id)));

        existing.Amount = new Money(90000, Currency.COP);
        var updated = await repo.UpdateAsync(existing);
        Assert.Equal(90000, updated.Amount.AmountMinor);
    }

    [Fact]
    public async Task Update_KeepsCreatedAtSourceAndReceipt_AndBumpsUpdatedAt()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        var saved = await repo.AddWithReceiptAsync(TestData.Expense(market.Id), NewReceipt(), NewImage());
        var createdAt = saved.CreatedAt;
        temp.Clock.Advance(TimeSpan.FromHours(1));

        saved.Merchant = "Carulla";
        saved.Source = TransactionSource.Manual;
        saved.ReceiptId = null;
        saved.CreatedAt = DateTime.UtcNow.AddYears(-1);
        await repo.UpdateAsync(saved);

        var loaded = await repo.GetAsync(saved.Id);
        Assert.Equal("Carulla", loaded!.Merchant);
        Assert.Equal(createdAt, loaded.CreatedAt);
        Assert.Equal(temp.Clock.UtcNow, loaded.UpdatedAt);
        Assert.Equal(TransactionSource.Ocr, loaded.Source);
        Assert.NotNull(loaded.ReceiptId);
    }

    [Fact]
    public async Task Update_UnknownId_Throws()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        var ghost = TestData.Expense(market.Id);
        ghost.Id = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.UpdateAsync(ghost));
    }

    [Fact]
    public async Task AddWithReceipt_SavesReceiptImageAndTransaction()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        var receipts = new ReceiptRepository(temp.Host);

        var saved = await repo.AddWithReceiptAsync(TestData.Expense(market.Id), NewReceipt(), NewImage());

        Assert.Equal(TransactionSource.Ocr, saved.Source);
        var receipt = await receipts.GetAsync(saved.ReceiptId!.Value);
        Assert.NotNull(receipt);
        Assert.Equal(temp.Clock.UtcNow, receipt.CapturedAt);
        Assert.Equal(DateTimeKind.Utc, receipt.CapturedAt.Kind);
        Assert.Equal(new Money(82150, Currency.COP), receipt.ParsedTotal);
        Assert.Equal("900123456-7", receipt.ParsedTaxId);
        var image = await receipts.GetImageAsync(receipt.Id);
        Assert.Equal([0xFF, 0xD8, 0xFF], image!.Jpeg);
    }

    [Fact]
    public async Task AddWithReceipt_WhenTransactionIsInvalid_SavesNothing()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => repo.AddWithReceiptAsync(TestData.Expense(market.Id, amountMinor: 0), NewReceipt(), NewImage()));

        await AssertCountsAsync(temp, receipts: 0, images: 0, transactions: 0);
    }

    [Fact]
    public async Task AddWithReceipt_WhenInsertFails_RollsBackReceiptAndImage()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        var first = await repo.AddAsync(TestData.Expense(market.Id));

        // Mismo Id que un movimiento existente: falla el último INSERT, después del recibo y la imagen.
        var duplicate = TestData.Expense(market.Id);
        duplicate.Id = first.Id;
        await Assert.ThrowsAnyAsync<Exception>(() => repo.AddWithReceiptAsync(duplicate, NewReceipt(), NewImage()));

        await AssertCountsAsync(temp, receipts: 0, images: 0, transactions: 1);
    }

    [Fact]
    public async Task Delete_RemovesTransactionReceiptAndImage()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        var saved = await repo.AddWithReceiptAsync(TestData.Expense(market.Id), NewReceipt(), NewImage());

        Assert.True(await repo.DeleteAsync(saved.Id));
        Assert.False(await repo.DeleteAsync(saved.Id));

        await AssertCountsAsync(temp, receipts: 0, images: 0, transactions: 0);
    }

    [Fact]
    public async Task Query_FiltersByHalfOpenDateRange_AndOrdersNewestFirst()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Local);
        var end = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Local);
        await repo.AddAsync(TestData.Expense(market.Id, 1, start.AddTicks(-1)));
        await repo.AddAsync(TestData.Expense(market.Id, 2, start));
        await repo.AddAsync(TestData.Expense(market.Id, 3, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Local)));
        await repo.AddAsync(TestData.Expense(market.Id, 4, end.AddTicks(-1)));
        await repo.AddAsync(TestData.Expense(market.Id, 5, end));

        var september = await repo.QueryAsync(TransactionFilter.Between(start, end));

        Assert.Equal([4L, 3L, 2L], september.Select(t => t.Amount.AmountMinor));
    }

    [Fact]
    public async Task Query_AcceptsUtcBounds()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        var occurredAt = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Local);
        await repo.AddAsync(TestData.Expense(market.Id, occurredAt: occurredAt));

        var result = await repo.QueryAsync(TransactionFilter.Between(
            occurredAt.ToUniversalTime(), occurredAt.AddMinutes(1).ToUniversalTime()));

        Assert.Single(result);
    }

    [Fact]
    public async Task Query_FiltersByTypeCurrencyCategoryAndPaymentMethod()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, salary) = await SetupAsync(temp);
        var restaurants = (await new CategoryRepository(temp.Host).GetAllAsync()).Single(c => c.Name == "Restaurantes");
        var cash = (await new PaymentMethodRepository(temp.Host).GetAllAsync())[0];

        await repo.AddAsync(TestData.Expense(market.Id, 100, paymentMethodId: cash.Id));
        await repo.AddAsync(TestData.Expense(market.Id, 1250, currency: Currency.USD));
        await repo.AddAsync(TestData.Expense(restaurants.Id, 300));
        var income = TestData.Expense(salary.Id, 5_000_000);
        income.Type = TransactionType.Income;
        await repo.AddAsync(income);

        Assert.Equal(3, (await repo.QueryAsync(new() { Type = TransactionType.Expense })).Count);
        Assert.Equal([1250L], (await repo.QueryAsync(new() { Currency = Currency.USD })).Select(t => t.Amount.AmountMinor));
        Assert.Equal(2, (await repo.QueryAsync(new() { CategoryId = market.Id })).Count);
        Assert.Equal([100L], (await repo.QueryAsync(new() { PaymentMethodId = cash.Id })).Select(t => t.Amount.AmountMinor));
        Assert.Empty(await repo.QueryAsync(new() { Currency = Currency.USD, CategoryId = restaurants.Id }));
    }

    [Fact]
    public async Task Query_PagesWithSkipAndTake()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        var day = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Local);
        for (var i = 1; i <= 5; i++)
        {
            await repo.AddAsync(TestData.Expense(market.Id, i, day.AddDays(i)));
        }

        Assert.Equal([5L, 4L], (await repo.QueryAsync(new() { Take = 2 })).Select(t => t.Amount.AmountMinor));
        Assert.Equal([3L, 2L], (await repo.QueryAsync(new() { Skip = 2, Take = 2 })).Select(t => t.Amount.AmountMinor));
        Assert.Equal([2L, 1L], (await repo.QueryAsync(new() { Skip = 3 })).Select(t => t.Amount.AmountMinor));
    }

    [Fact]
    public async Task Repositories_KeepWorking_AfterReset()
    {
        await using var temp = await TempHost.OpenAsync();
        var (repo, market, _) = await SetupAsync(temp);
        await repo.AddAsync(TestData.Expense(market.Id));

        await temp.Host.ResetAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await repo.QueryAsync(TransactionFilter.All));
        await repo.AddAsync(TestData.Expense(market.Id));
        Assert.Single(await repo.QueryAsync(TransactionFilter.All));
    }

    private static async Task<(TransactionRepository Repo, Category Market, Category Salary)> SetupAsync(TempHost temp)
    {
        var categories = await new CategoryRepository(temp.Host).GetAllAsync();
        return (new TransactionRepository(temp.Host, temp.Clock),
            categories.Single(c => c.Name == "Mercado"),
            categories.Single(c => c.Name == "Salario"));
    }

    private static async Task AssertCountsAsync(TempHost temp, int receipts, int images, int transactions)
    {
        var db = temp.Host.Database.Connection;
        Assert.Equal(receipts, await db.Table<ReceiptRecord>().CountAsync());
        Assert.Equal(images, await db.Table<ReceiptImageRecord>().CountAsync());
        Assert.Equal(transactions, await db.Table<TransactionRecord>().CountAsync());
    }

    private static Receipt NewReceipt() => new()
    {
        OcrText = "SUPERMERCADO\nTOTAL A PAGAR $82.150",
        ParsedTotal = new Money(82150, Currency.COP),
        ParsedDate = new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Local),
        ParsedMerchant = "Supermercado",
        ParsedTaxId = "900123456-7",
        OcrEngine = "Test",
    };

    private static ReceiptImage NewImage() => new() { Jpeg = [0xFF, 0xD8, 0xFF], Width = 1200, Height = 1600 };
}
