using System.Security.Cryptography;
using System.Text;
using PiRadar.Core.Domain;
using PiRadar.Data.Migrations;
using PiRadar.Data.Records;
using PiRadar.Data.Seed;
using SQLite;

namespace PiRadar.Data.Tests;

public class PiRadarDatabaseTests
{
    [Fact]
    public async Task File_IsEncrypted_AndHasNoPlaintextData()
    {
        using var temp = new TempDatabase();
        await using (await temp.OpenAsync())
        {
        }

        var bytes = await File.ReadAllBytesAsync(temp.Path, TestContext.Current.CancellationToken);
        Assert.NotEqual("SQLite format 3\0", Encoding.ASCII.GetString(bytes, 0, 16));
        Assert.DoesNotContain("Mercado", Encoding.UTF8.GetString(bytes));
        Assert.DoesNotContain("Transactions", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Open_WithWrongKey_Throws()
    {
        using var temp = new TempDatabase();
        await using (await temp.OpenAsync())
        {
        }

        await Assert.ThrowsAsync<DatabaseUnreadableException>(
            () => PiRadarDatabase.OpenAsync(temp.Path, RandomNumberGenerator.GetBytes(32)));
    }

    [Fact]
    public async Task Open_WithoutEncryption_CannotReadFile()
    {
        using var temp = new TempDatabase();
        await using (await temp.OpenAsync())
        {
        }

        var plain = new SQLiteConnection(temp.Path);
        try
        {
            Assert.Throws<SQLiteException>(() => plain.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master"));
        }
        finally
        {
            plain.Close();
        }
    }

    [Fact]
    public async Task Open_RejectsKeysThatAreNot32Bytes()
    {
        using var temp = new TempDatabase();
        await Assert.ThrowsAsync<ArgumentException>(() => PiRadarDatabase.OpenAsync(temp.Path, new byte[16]));
    }

    [Fact]
    public async Task Open_AppliesMigrations_AndSeedsDefaults()
    {
        using var temp = new TempDatabase();
        await using var db = await temp.OpenAsync();

        Assert.Equal(Migrator.LatestVersion, await db.Connection.ExecuteScalarAsync<int>("PRAGMA user_version"));

        var categories = await db.Connection.Table<CategoryRecord>().OrderBy(c => c.SortOrder).ToListAsync();
        Assert.Equal(DefaultData.Categories.Select(c => c.Name), categories.Select(c => c.Name));
        Assert.All(categories, c => Assert.True(c.IsSystem));

        var methods = await db.Connection.Table<PaymentMethodRecord>().OrderBy(p => p.SortOrder).ToListAsync();
        Assert.Equal(DefaultData.PaymentMethods.Select(p => p.Name), methods.Select(p => p.Name));

        var baseCurrency = await db.Connection.FindAsync<AppSettingRecord>(AppSettingKeys.BaseCurrency);
        Assert.Equal("COP", baseCurrency.Value);
    }

    [Fact]
    public async Task Reopen_DoesNotReapplyMigrations()
    {
        using var temp = new TempDatabase();
        await using (await temp.OpenAsync())
        {
        }

        await using var db = await temp.OpenAsync();
        Assert.Equal(DefaultData.Categories.Count, await db.Connection.Table<CategoryRecord>().CountAsync());
    }

    [Fact]
    public async Task ForeignKeys_AreEnforced()
    {
        using var temp = new TempDatabase();
        await using var db = await temp.OpenAsync();

        var orphan = NewTransaction(categoryId: 9999).ToRecord();
        await Assert.ThrowsAsync<SQLiteException>(() => db.Connection.InsertAsync(orphan));
    }

    [Fact]
    public async Task Transaction_RoundTrips_WithUtcAuditDates_AndLocalOccurredAt()
    {
        using var temp = new TempDatabase();
        await using var db = await temp.OpenAsync();
        var category = await db.Connection.Table<CategoryRecord>().FirstAsync();
        var transaction = NewTransaction(category.Id);

        await db.Connection.InsertAsync(transaction.ToRecord());
        var loaded = (await db.Connection.GetAsync<TransactionRecord>(transaction.Id)).ToDomain();

        Assert.Equal(transaction.Amount, loaded.Amount);
        Assert.Equal(transaction.OccurredAt, loaded.OccurredAt);
        Assert.Equal(DateTimeKind.Local, loaded.OccurredAt.Kind);
        Assert.Equal(transaction.CreatedAt, loaded.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, loaded.UpdatedAt.Kind);
        Assert.Equal(transaction.Merchant, loaded.Merchant);
        Assert.Null(loaded.PaymentMethodId);
    }

    [Fact]
    public async Task Transactions_RejectNegativeAmounts()
    {
        using var temp = new TempDatabase();
        await using var db = await temp.OpenAsync();
        var category = await db.Connection.Table<CategoryRecord>().FirstAsync();
        var record = NewTransaction(category.Id).ToRecord();
        record.AmountMinor = -1;

        await Assert.ThrowsAsync<SQLiteException>(() => db.Connection.InsertAsync(record));
    }

    private static Transaction NewTransaction(int categoryId)
    {
        var now = DateTime.UtcNow;
        return new Transaction
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Expense,
            Amount = new Money(82150, Currency.COP),
            OccurredAt = new DateTime(2026, 9, 14, 18, 42, 0, DateTimeKind.Local),
            CategoryId = categoryId,
            Merchant = "Supermercado El Éxito",
            Source = TransactionSource.Manual,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
