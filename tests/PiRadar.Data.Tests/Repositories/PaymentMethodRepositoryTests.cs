using PiRadar.Core.Domain;
using PiRadar.Data.Repositories;
using PiRadar.Data.Seed;

namespace PiRadar.Data.Tests.Repositories;

public class PaymentMethodRepositoryTests
{
    [Fact]
    public async Task GetAll_ReturnsSeedInOrder()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new PaymentMethodRepository(temp.Host);

        var all = await repo.GetAllAsync();

        Assert.Equal(DefaultData.PaymentMethods.Select(p => p.Name), all.Select(p => p.Name));
    }

    [Fact]
    public async Task Add_NormalizesLastFourDigits_AndPutsLast()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new PaymentMethodRepository(temp.Host);

        var visa = await repo.AddAsync(NewMethod("Visa Bancolombia", " 1234 "));
        var noDigits = await repo.AddAsync(NewMethod("Bolsillo", "  "));

        Assert.Equal("1234", visa.LastFourDigits);
        Assert.Null(noDigits.LastFourDigits);
        Assert.Equal(noDigits.Id, (await repo.GetAllAsync())[^1].Id);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("Visa", "123")]
    [InlineData("Visa", "12a4")]
    [InlineData("Visa", "12345")]
    [InlineData("efectivo", null)]
    public async Task Add_RejectsInvalidData(string name, string? lastFour)
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new PaymentMethodRepository(temp.Host);

        await Assert.ThrowsAsync<DomainValidationException>(() => repo.AddAsync(NewMethod(name, lastFour)));
    }

    [Fact]
    public async Task Update_KeepsIsSystem()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new PaymentMethodRepository(temp.Host);
        var cash = (await repo.GetAllAsync())[0];

        cash.Name = "Billetes";
        cash.IsSystem = false;
        await repo.UpdateAsync(cash);

        var loaded = await repo.GetAsync(cash.Id);
        Assert.Equal("Billetes", loaded!.Name);
        Assert.True(loaded.IsSystem);
    }

    [Fact]
    public async Task Delete_ArchivesWhenUsed_DeletesOtherwise()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new PaymentMethodRepository(temp.Host);
        var methods = await repo.GetAllAsync();
        var category = (await new CategoryRepository(temp.Host).GetAllAsync(TransactionType.Expense))[0];
        await new TransactionRepository(temp.Host, temp.Clock)
            .AddAsync(TestData.Expense(category.Id, paymentMethodId: methods[0].Id));

        Assert.Equal(RemovalResult.Archived, await repo.DeleteOrArchiveAsync(methods[0].Id));
        Assert.Equal(RemovalResult.Deleted, await repo.DeleteOrArchiveAsync(methods[1].Id));
        Assert.True((await repo.GetAsync(methods[0].Id))!.IsArchived);
        Assert.Null(await repo.GetAsync(methods[1].Id));
    }

    private static PaymentMethod NewMethod(string name, string? lastFour) => new()
    {
        Name = name, Kind = PaymentMethodKind.CreditCard, Icon = "credit_card", LastFourDigits = lastFour,
    };
}
