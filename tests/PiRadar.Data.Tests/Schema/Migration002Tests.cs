using PiRadar.Data.Migrations;
using SQLite;

namespace PiRadar.Data.Tests.Schema;

public class Migration002Tests
{
    private static SQLiteConnection Memory()
    {
        SQLitePCL.Batteries_V2.Init();
        var db = new SQLiteConnection(new SQLiteConnectionString(":memory:", storeDateTimeAsTicks: true));
        db.Execute("PRAGMA foreign_keys = ON");
        return db;
    }

    [Fact]
    public void Migrating_a_version1_database_keeps_data_and_adds_the_batch_structure()
    {
        using var db = Memory();
        Migrator.MigrateTo(db, 1);
        var id = Guid.NewGuid().ToString();
        db.Execute(
            """
            INSERT INTO "Transactions" ("Id","Type","AmountMinor","Currency","OccurredAt","CategoryId","Source","CreatedAt","UpdatedAt")
            VALUES (?, 1, 5000, 'COP', 1, 1, 1, 1, 1)
            """, id);
        var categories = db.ExecuteScalar<int>("""SELECT count(*) FROM "Category" """);

        Migrator.Migrate(db);

        Assert.Equal(2, Migrator.GetVersion(db));
        Assert.Equal(categories, db.ExecuteScalar<int>("""SELECT count(*) FROM "Category" """));
        Assert.Equal(5000, db.ExecuteScalar<long>("""SELECT "AmountMinor" FROM "Transactions" WHERE "Id" = ?""", id));
        Assert.Null(db.ExecuteScalar<string?>("""SELECT "ImportBatchId" FROM "Transactions" WHERE "Id" = ?""", id));
        Assert.Equal(0, db.ExecuteScalar<int>("""SELECT count(*) FROM "ImportBatch" """));
        Assert.Equal(1, db.ExecuteScalar<int>("""SELECT count(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Transactions_ImportBatchId'"""));
        Assert.Equal(0, db.ExecuteScalar<int>("SELECT count(*) FROM pragma_foreign_key_check"));
    }

    [Fact]
    public void Deleting_a_batch_leaves_its_transactions_without_batch()
    {
        using var db = Memory();
        Migrator.Migrate(db);
        var batch = Guid.NewGuid().ToString();
        var id = Guid.NewGuid().ToString();
        db.Execute("""INSERT INTO "ImportBatch" ("Id","ImportedAt","FileName","RowCount") VALUES (?, 1, NULL, 1)""", batch);
        db.Execute(
            """
            INSERT INTO "Transactions" ("Id","Type","AmountMinor","Currency","OccurredAt","CategoryId","Source","CreatedAt","UpdatedAt","ImportBatchId")
            VALUES (?, 1, 5000, 'COP', 1, 1, 3, 1, 1, ?)
            """, id, batch);

        db.Execute("""DELETE FROM "ImportBatch" WHERE "Id" = ?""", batch);

        Assert.Null(db.ExecuteScalar<string?>("""SELECT "ImportBatchId" FROM "Transactions" WHERE "Id" = ?""", id));
    }

    [Fact]
    public async Task New_database_is_at_version2()
    {
        using var temp = new TempDatabase();
        await using var db = await temp.OpenAsync();
        Assert.Equal(2, await db.Connection.ExecuteScalarAsync<int>("PRAGMA user_version"));
        Assert.Equal(2, Migrator.LatestVersion);
    }
}
