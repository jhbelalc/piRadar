using System.IO.Compression;
using System.Text.Json;
using PiRadar.Core.Backup;
using PiRadar.Core.Domain;
using PiRadar.Core.Import;
using PiRadar.Data.Backup;
using PiRadar.Data.Migrations;
using PiRadar.Data.Repositories;
using SQLite;

namespace PiRadar.Data.Tests.Backup;

public class BackupImportBatchTests
{
    private const string Password = "respaldo seguro";
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static BackupService Service(TempHost temp) => new(temp.Host, temp.Clock) { Iterations = 1000 };

    [Fact]
    public async Task Backup_of_current_schema_carries_import_batches_and_restores_them()
    {
        await using var source = await TempHost.OpenAsync();
        var other = (await new CategoryRepository(source.Host).GetAllAsync()).Single(c => c.Name == "Otros gastos").Id;
        var row = new ImportRow(2, TransactionType.Expense, new Money(4200, Currency.COP),
            new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Local), null, null, null, "Compra", null, null, false, false);
        var result = await new TransactionImporter(source.Host, source.Clock)
            .ImportAsync(new ImportPlan("a.csv", [row], [], [], other, null));
        await using var target = await TempHost.OpenAsync();

        var file = await Service(source).ExportAsync(Password, Ct);
        await Service(target).RestoreAsync(await Service(target).OpenAsync(file, Password, Ct), Ct);

        var importer = new TransactionImporter(target.Host, target.Clock);
        var info = await importer.GetLastImportAsync();
        Assert.Equal(result.BatchId, info!.Id);
        Assert.Equal("a.csv", info.FileName);
        Assert.Equal(1, info.RemainingCount);
        Assert.Equal(source.Clock.UtcNow, info.ImportedAt);
        var tx = Assert.Single(await new TransactionRepository(target.Host, target.Clock).QueryAsync(new TransactionFilter()));
        Assert.Equal(TransactionSource.Import, tx.Source);
        Assert.Equal(1, await importer.UndoLastImportAsync(result.BatchId));
    }

    [Fact]
    public async Task Backup_of_schema_1_restores_and_ends_at_schema_2_without_batches()
    {
        SQLitePCL.Batteries_V2.Init();
        BackupDocument document;
        using (var old = new SQLiteConnection(new SQLiteConnectionString(":memory:", storeDateTimeAsTicks: true)))
        {
            old.Execute("PRAGMA foreign_keys = ON");
            Migrator.MigrateTo(old, 1);
            old.Execute(
                """
                INSERT INTO "Transactions" ("Id","Type","AmountMinor","Currency","OccurredAt","CategoryId","Source","CreatedAt","UpdatedAt")
                VALUES ('11111111-1111-1111-1111-111111111111', 1, 5000, 'COP', 637000000000000000, 1, 1, 637000000000000000, 637000000000000000)
                """);
            document = new BackupDocument
            {
                SchemaVersion = 1,
                CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                Tables = TableDump.DumpAll(old).ToDictionary(p => p.Key, p => p.Value),
            };
        }

        Assert.DoesNotContain("ImportBatch", document.Tables.Keys);
        Assert.DoesNotContain("ImportBatchId", document.Tables["Transactions"].Columns);

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
        {
            gzip.Write(System.Text.Encoding.UTF8.GetBytes(json));
        }

        var file = BackupCrypto.Encrypt(output.ToArray(), Password, 1, iterations: 1000);
        await using var target = await TempHost.OpenAsync();

        await Service(target).RestoreAsync(await Service(target).OpenAsync(file, Password, Ct), Ct);

        var db = target.Host.Database.Connection;
        Assert.Equal(2, await db.ExecuteScalarAsync<int>("PRAGMA user_version"));
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("""SELECT count(*) FROM "Transactions" """));
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("""SELECT count(*) FROM "Transactions" WHERE "ImportBatchId" IS NULL"""));
        Assert.Equal(0, await db.ExecuteScalarAsync<int>("""SELECT count(*) FROM "ImportBatch" """));
        Assert.Null(await new TransactionImporter(target.Host, target.Clock).GetLastImportAsync());
    }
}
