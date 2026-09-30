using PiRadar.Core.Domain;
using PiRadar.Data.Repositories;
using PiRadar.Data.Seed;

namespace PiRadar.Data.Tests.Repositories;

public class EnglishSeedTests
{
    private static async Task<TempHost> OpenEnglishAsync()
    {
        using var english = AppLocale.Use(AppLanguage.English);
        return await TempHost.OpenAsync();
    }

    [Fact]
    public void Seed_lists_are_parallel()
    {
        Assert.Equal(DefaultData.SpanishCategories.Count, DefaultData.EnglishCategoryNames.Count);
        Assert.Equal(DefaultData.SpanishPaymentMethods.Count, DefaultData.EnglishPaymentMethodNames.Count);
        Assert.Equal(DefaultData.EnglishCategoryNames.Count, DefaultData.EnglishCategoryNames.Distinct().Count());
    }

    [Fact]
    public async Task New_database_on_english_device_is_seeded_in_english_with_usd()
    {
        await using var temp = await OpenEnglishAsync();
        var categories = await new CategoryRepository(temp.Host).GetAllAsync();
        var methods = await new PaymentMethodRepository(temp.Host).GetAllAsync();

        Assert.Contains(categories, c => c.Name == "Groceries" && c.Kind == TransactionType.Expense);
        Assert.Contains(categories, c => c.Name == "Salary" && c.Kind == TransactionType.Income);
        Assert.DoesNotContain(categories, c => c.Name == "Mercado");
        Assert.Contains(methods, m => m.Name == "Cash");
        Assert.Contains(methods, m => m.Name == "Venmo");
        Assert.Equal(Currency.USD, await new SettingsRepository(temp.Host).GetBaseCurrencyAsync());
    }

    [Fact]
    public async Task New_database_on_spanish_device_keeps_spanish_seed_and_cop()
    {
        await using var temp = await TempHost.OpenAsync();
        var categories = await new CategoryRepository(temp.Host).GetAllAsync();

        Assert.Contains(categories, c => c.Name == "Mercado");
        Assert.Equal(Currency.COP, await new SettingsRepository(temp.Host).GetBaseCurrencyAsync());
    }

    [Fact]
    public async Task Translate_to_english_and_back_renames_untouched_system_rows()
    {
        await using var temp = await TempHost.OpenAsync();
        var localizer = new SystemNameLocalizer(temp.Host);
        var categories = new CategoryRepository(temp.Host);
        var methods = new PaymentMethodRepository(temp.Host);

        var changed = await localizer.ApplyAsync(AppLanguage.English);
        Assert.Equal(DefaultData.SpanishCategories.Count + DefaultData.TranslatablePaymentMethods, changed);
        var english = await categories.GetAllAsync();
        Assert.Contains(english, c => c.Name == "Groceries");
        Assert.DoesNotContain(english, c => c.Name == "Mercado");
        var englishMethods = await methods.GetAllAsync();
        Assert.Contains(englishMethods, m => m.Name == "Debit card");
        // Wallet brands are not translated.
        Assert.Contains(englishMethods, m => m.Name == "Nequi");
        Assert.DoesNotContain(englishMethods, m => m.Name == "Venmo");

        Assert.Equal(changed, await localizer.ApplyAsync(AppLanguage.Spanish));
        Assert.Contains(await categories.GetAllAsync(), c => c.Name == "Mercado");
        Assert.Contains(await methods.GetAllAsync(), m => m.Name == "Tarjeta débito");
    }

    [Fact]
    public async Task Translating_twice_changes_nothing_the_second_time()
    {
        await using var temp = await TempHost.OpenAsync();
        var localizer = new SystemNameLocalizer(temp.Host);

        await localizer.ApplyAsync(AppLanguage.English);

        Assert.Equal(0, await localizer.ApplyAsync(AppLanguage.English));
    }

    [Fact]
    public async Task Renamed_or_user_created_rows_are_left_alone()
    {
        await using var temp = await TempHost.OpenAsync();
        var categories = new CategoryRepository(temp.Host);
        var groceries = (await categories.GetAllAsync()).Single(c => c.Name == "Mercado");
        groceries.Name = "Supermercado";
        await categories.UpdateAsync(groceries);
        await categories.AddAsync(new Category { Name = "Salud", Kind = TransactionType.Income, Icon = "home", Color = "#112233" });

        await new SystemNameLocalizer(temp.Host).ApplyAsync(AppLanguage.English);

        var all = await categories.GetAllAsync();
        Assert.Contains(all, c => c.Name == "Supermercado");
        Assert.DoesNotContain(all, c => c.Name == "Groceries");
        // The user-created income "Salud" is not a system row: it stays; the system expense "Salud" becomes "Health".
        Assert.Contains(all, c => c.Name == "Salud" && c.Kind == TransactionType.Income && !c.IsSystem);
        Assert.Contains(all, c => c.Name == "Health" && c.Kind == TransactionType.Expense && c.IsSystem);
    }

    [Fact]
    public async Task A_name_already_used_by_a_user_row_is_not_duplicated()
    {
        await using var temp = await TempHost.OpenAsync();
        var categories = new CategoryRepository(temp.Host);
        await categories.AddAsync(new Category { Name = "Groceries", Kind = TransactionType.Expense, Icon = "home", Color = "#112233" });

        var changed = await new SystemNameLocalizer(temp.Host).ApplyAsync(AppLanguage.English);

        Assert.Equal(DefaultData.SpanishCategories.Count + DefaultData.TranslatablePaymentMethods - 1, changed);
        var all = await categories.GetAllAsync(TransactionType.Expense);
        Assert.Single(all, c => c.Name == "Groceries");
        Assert.Contains(all, c => c.Name == "Mercado" && c.IsSystem);
    }

    [Fact]
    public async Task Language_setting_round_trips()
    {
        await using var temp = await TempHost.OpenAsync();
        var settings = new SettingsRepository(temp.Host);

        Assert.Null(await settings.GetLanguageAsync());
        await settings.SetLanguageAsync(AppLanguage.English);
        Assert.Equal(AppLanguage.English, await settings.GetLanguageAsync());
        await settings.SetLanguageAsync(AppLanguage.Spanish);
        Assert.Equal(AppLanguage.Spanish, await settings.GetLanguageAsync());
        await settings.SetLanguageAsync(null);
        Assert.Null(await settings.GetLanguageAsync());
    }

    [Fact]
    public async Task Validation_errors_follow_the_language()
    {
        await using var temp = await TempHost.OpenAsync();
        var categories = new CategoryRepository(temp.Host);
        var draft = new Category { Name = " ", Kind = TransactionType.Expense, Icon = "home", Color = "#112233" };

        var spanish = await Assert.ThrowsAsync<DomainValidationException>(() => categories.AddAsync(draft));
        Assert.Equal("El nombre de la categoría es obligatorio.", spanish.Message);

        using var english = AppLocale.Use(AppLanguage.English);
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => categories.AddAsync(draft));
        Assert.Equal("The category name is required.", ex.Message);
    }
}
