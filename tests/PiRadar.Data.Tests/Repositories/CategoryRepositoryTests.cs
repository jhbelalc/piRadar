using PiRadar.Core.Domain;
using PiRadar.Data.Repositories;
using PiRadar.Data.Seed;

namespace PiRadar.Data.Tests.Repositories;

public class CategoryRepositoryTests
{
    [Fact]
    public async Task GetAll_FiltersByKind_AndOrdersBySortOrder()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);

        var all = await repo.GetAllAsync();
        var income = await repo.GetAllAsync(TransactionType.Income);

        Assert.Equal(DefaultData.Categories.Select(c => c.Name), all.Select(c => c.Name));
        Assert.All(income, c => Assert.Equal(TransactionType.Income, c.Kind));
        Assert.Equal(DefaultData.Categories.Count(c => c.Kind == TransactionType.Income), income.Count);
    }

    [Fact]
    public async Task Add_AssignsId_PutsLast_AndIsNotSystem()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);

        var added = await repo.AddAsync(NewCategory("  Gimnasio "));

        Assert.True(added.Id > 0);
        Assert.Equal("Gimnasio", added.Name);
        Assert.False(added.IsSystem);
        Assert.Equal("#ABCDEF", added.Color);
        Assert.Equal(added.Id, (await repo.GetAllAsync())[^1].Id);
    }

    [Theory]
    [InlineData("", "#123456")]
    [InlineData("  ", "#123456")]
    [InlineData("Gimnasio", "123456")]
    [InlineData("Gimnasio", "#12345G")]
    [InlineData("mercado", "#123456")]
    public async Task Add_RejectsInvalidData(string name, string color)
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);

        var category = NewCategory(name);
        category.Color = color;

        await Assert.ThrowsAsync<DomainValidationException>(() => repo.AddAsync(category));
    }

    [Fact]
    public async Task Add_AllowsSameNameAsArchivedOrOtherKind()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);

        var incomeNamedLikeExpense = NewCategory("Mercado");
        incomeNamedLikeExpense.Kind = TransactionType.Income;
        await repo.AddAsync(incomeNamedLikeExpense);

        var gym = await repo.AddAsync(NewCategory("Gimnasio"));
        await repo.DeleteOrArchiveAsync(gym.Id);
        await repo.AddAsync(NewCategory("Gimnasio"));
    }

    [Fact]
    public async Task Subcategory_MustMatchParentKind_AndOnlyOneLevel()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);
        var market = await FindAsync(repo, "Mercado");
        var salary = await FindAsync(repo, "Salario");

        var fruit = NewCategory("Frutas");
        fruit.ParentId = market.Id;
        fruit = await repo.AddAsync(fruit);

        var wrongKind = NewCategory("Bonos");
        wrongKind.ParentId = salary.Id;
        await Assert.ThrowsAsync<DomainValidationException>(() => repo.AddAsync(wrongKind));

        var grandChild = NewCategory("Manzanas");
        grandChild.ParentId = fruit.Id;
        await Assert.ThrowsAsync<DomainValidationException>(() => repo.AddAsync(grandChild));

        market.ParentId = market.Id;
        await Assert.ThrowsAsync<DomainValidationException>(() => repo.UpdateAsync(market));
    }

    [Fact]
    public async Task Update_KeepsIsSystem_AndPersistsChanges()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);
        var market = await FindAsync(repo, "Mercado");

        market.Name = "Supermercado";
        market.IsSystem = false;
        await repo.UpdateAsync(market);

        var loaded = await repo.GetAsync(market.Id);
        Assert.Equal("Supermercado", loaded!.Name);
        Assert.True(loaded.IsSystem);
    }

    [Fact]
    public async Task Update_UnknownId_Throws()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);
        var ghost = NewCategory("Fantasma");
        ghost.Id = 9999;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.UpdateAsync(ghost));
    }

    [Fact]
    public async Task Update_CannotChangeKind_WhenItHasTransactions()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);
        var market = await FindAsync(repo, "Mercado");
        await new TransactionRepository(temp.Host, temp.Clock).AddAsync(TestData.Expense(market.Id));

        market.Kind = TransactionType.Income;

        await Assert.ThrowsAsync<DomainValidationException>(() => repo.UpdateAsync(market));
    }

    [Fact]
    public async Task Delete_WithoutTransactions_Deletes()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);
        var gym = await repo.AddAsync(NewCategory("Gimnasio"));

        Assert.Equal(RemovalResult.Deleted, await repo.DeleteOrArchiveAsync(gym.Id));
        Assert.Null(await repo.GetAsync(gym.Id));
    }

    [Fact]
    public async Task Delete_WithTransactions_Archives_AndHidesFromDefaultList()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);
        var market = await FindAsync(repo, "Mercado");
        await new TransactionRepository(temp.Host, temp.Clock).AddAsync(TestData.Expense(market.Id));

        Assert.Equal(RemovalResult.Archived, await repo.DeleteOrArchiveAsync(market.Id));
        Assert.True((await repo.GetAsync(market.Id))!.IsArchived);
        Assert.DoesNotContain(await repo.GetAllAsync(), c => c.Id == market.Id);
        Assert.Contains(await repo.GetAllAsync(includeArchived: true), c => c.Id == market.Id);
    }

    [Fact]
    public async Task Delete_WithSubcategories_Archives()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);
        var market = await FindAsync(repo, "Mercado");
        var fruit = NewCategory("Frutas");
        fruit.ParentId = market.Id;
        await repo.AddAsync(fruit);

        Assert.Equal(RemovalResult.Archived, await repo.DeleteOrArchiveAsync(market.Id));
    }

    [Fact]
    public async Task Delete_UnknownId_Throws()
    {
        await using var temp = await TempHost.OpenAsync();
        var repo = new CategoryRepository(temp.Host);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.DeleteOrArchiveAsync(9999));
    }

    private static async Task<Category> FindAsync(CategoryRepository repo, string name) =>
        (await repo.GetAllAsync(includeArchived: true)).Single(c => c.Name == name);

    private static Category NewCategory(string name) => new()
    {
        Name = name, Kind = TransactionType.Expense, Icon = "fitness_center", Color = "#abcdef",
    };
}
