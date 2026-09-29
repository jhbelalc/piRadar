using System.IO.Compression;
using System.Text;
using PiRadar.Core.Backup;
using PiRadar.Core.Domain;
using PiRadar.Data.Backup;
using PiRadar.Data.Migrations;
using PiRadar.Data.Repositories;
using PiRadar.Data.Tests.Repositories;

namespace PiRadar.Data.Tests.Backup;

public class BackupServiceTests
{
    private const string Password = "respaldo seguro";

    [Fact]
    public async Task ExportThenRestore_OnAnotherDatabase_CopiesEverything()
    {
        await using var source = await TempHost.OpenAsync();
        var original = await SeedAsync(source);
        await using var target = await TempHost.OpenAsync();
        // Datos propios del destino: la restauración los reemplaza.
        var targetMarket = (await new CategoryRepository(target.Host).GetAllAsync()).Single(c => c.Name == "Mercado");
        var stale = await new TransactionRepository(target.Host, target.Clock).AddAsync(TestData.Expense(targetMarket.Id, 999));

        var file = await Service(source).ExportAsync(Password);
        var contents = await Service(target).OpenAsync(file, Password);
        await Service(target).RestoreAsync(contents);

        Assert.Equal(new DateTime(2026, 9, 20, 15, 0, 0, DateTimeKind.Utc), contents.Summary.CreatedAt);
        Assert.Equal(2, contents.Summary.TransactionCount);
        Assert.Equal(1, contents.Summary.ReceiptCount);
        Assert.Equal(new DateTime(2026, 9, 1, 8, 0, 0), contents.Summary.FirstTransaction);
        Assert.Equal(new DateTime(2026, 9, 14, 18, 42, 0), contents.Summary.LastTransaction);

        var transactions = new TransactionRepository(target.Host, target.Clock);
        Assert.Null(await transactions.GetAsync(stale.Id));
        var restored = await transactions.GetAsync(original.Id);
        Assert.NotNull(restored);
        Assert.Equal(original.Amount, restored.Amount);
        Assert.Equal(original.OccurredAt, restored.OccurredAt);
        Assert.Equal(DateTimeKind.Local, restored.OccurredAt.Kind);
        Assert.Equal(original.CreatedAt, restored.CreatedAt);
        Assert.Equal("Supermercado El Éxito", restored.Merchant);

        var receipts = new ReceiptRepository(target.Host);
        Assert.Equal("900123456-7", (await receipts.GetAsync(restored.ReceiptId!.Value))!.ParsedTaxId);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x7F }, (await receipts.GetImageAsync(restored.ReceiptId.Value))!.Jpeg);

        var categories = await new CategoryRepository(target.Host).GetAllAsync(includeArchived: true);
        var coffee = Assert.Single(categories, c => c.Name == "Café");
        Assert.Equal(categories.Single(c => c.Name == "Restaurantes").Id, coffee.ParentId);
        Assert.Equal(3900m, Assert.Single(await new ExchangeRateRepository(target.Host).GetAllAsync()).Rate);
        Assert.Equal("Dark", await new SettingsRepository(target.Host).GetAsync(AppSettingKeys.Theme));
        Assert.Equal("Mis finanzas", await new SettingsRepository(target.Host).GetHomeTitleAsync());
        // La fecha del último respaldo pasa a ser la del respaldo restaurado.
        Assert.Equal(contents.Summary.CreatedAt, await new SettingsRepository(target.Host).GetLastBackupAtAsync());
    }

    [Fact]
    public async Task Open_WrongPassword_Fails_WithoutTouchingData()
    {
        await using var temp = await TempHost.OpenAsync();
        await SeedAsync(temp);
        var file = await Service(temp).ExportAsync(Password);

        var ex = await Assert.ThrowsAsync<BackupException>(() => Service(temp).OpenAsync(file, "otra contraseña"));

        Assert.Equal("Contraseña incorrecta o archivo dañado.", ex.Message);
        Assert.Equal(2, await CountAsync(temp, "Transactions"));
    }

    [Fact]
    public async Task Open_NewerSchema_IsRejected()
    {
        await using var temp = await TempHost.OpenAsync();
        var file = BackupCrypto.Encrypt(Gzip("{}"), Password, Migrator.LatestVersion + 1, iterations: 1000);

        var ex = await Assert.ThrowsAsync<BackupException>(() => Service(temp).OpenAsync(file, Password));

        Assert.Contains("versión más nueva", ex.Message);
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("""{"app":"otra","schemaVersion":1,"tables":{}}""")]
    [InlineData("""{"app":"piRadar","schemaVersion":1,"tables":{"Borrar; DROP TABLE Category":{"columns":["Id"],"rows":[[1]]}}}""")]
    [InlineData("""{"app":"piRadar","schemaVersion":1,"tables":{"Category":{"columns":["Id","Clave"],"rows":[[1,"x"]]}}}""")]
    public async Task Open_InvalidContent_IsReportedAsDamaged(string json)
    {
        await using var temp = await TempHost.OpenAsync();
        var file = BackupCrypto.Encrypt(Gzip(json), Password, 1, iterations: 1000);

        var ex = await Assert.ThrowsAsync<BackupException>(() => Service(temp).OpenAsync(file, Password));

        Assert.Equal("El archivo de respaldo está dañado.", ex.Message);
    }

    [Fact]
    public async Task Open_BrokenReference_IsReportedAsDamaged()
    {
        await using var temp = await TempHost.OpenAsync();
        await SeedAsync(temp);
        var service = Service(temp);
        // Un movimiento que apunta a una categoría que no está en el respaldo.
        await temp.Host.Database.Connection.ExecuteAsync("PRAGMA foreign_keys = OFF");
        await temp.Host.Database.Connection.ExecuteAsync("UPDATE \"Transactions\" SET \"CategoryId\" = 999");
        var file = await service.ExportAsync(Password);

        await Assert.ThrowsAsync<BackupException>(() => service.OpenAsync(file, Password));
    }

    private static BackupService Service(TempHost temp) => new(temp.Host, temp.Clock) { Iterations = 1000 };

    /// <summary>Una subcategoría, un gasto manual, otro con recibo e imagen, una tasa y el tema.</summary>
    private static async Task<Transaction> SeedAsync(TempHost temp)
    {
        var categories = new CategoryRepository(temp.Host);
        var restaurants = (await categories.GetAllAsync()).Single(c => c.Name == "Restaurantes");
        var coffee = await categories.AddAsync(new Category
        {
            Name = "Café", Kind = TransactionType.Expense, Icon = "local_cafe", Color = "#6D4C41", ParentId = restaurants.Id,
        });

        var transactions = new TransactionRepository(temp.Host, temp.Clock);
        await transactions.AddAsync(TestData.Expense(coffee.Id, 8500, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Local)));
        var withReceipt = await transactions.AddWithReceiptAsync(
            TestData.Expense(coffee.Id),
            new Receipt { OcrText = "TOTAL A PAGAR $82.150", ParsedTaxId = "900123456-7", OcrEngine = "Test" },
            new ReceiptImage { Jpeg = [0xFF, 0xD8, 0xFF, 0x00, 0x7F], Width = 1200, Height = 1600 });

        await new ExchangeRateRepository(temp.Host).UpsertAsync(new ExchangeRate
        {
            From = Currency.USD, To = Currency.COP, Rate = 3900m, EffectiveDate = new DateTime(2026, 9, 1),
        });
        await new SettingsRepository(temp.Host).SetAsync(AppSettingKeys.Theme, "Dark");
        await new SettingsRepository(temp.Host).SetHomeTitleAsync("Mis finanzas");
        return withReceipt;
    }

    private static Task<int> CountAsync(TempHost temp, string table) =>
        temp.Host.Database.Connection.ExecuteScalarAsync<int>($"SELECT count(*) FROM \"{table}\"");

    private static byte[] Gzip(string json)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
        {
            gzip.Write(Encoding.UTF8.GetBytes(json));
        }

        return output.ToArray();
    }
}
