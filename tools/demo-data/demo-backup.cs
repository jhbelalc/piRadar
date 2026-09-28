#:project ../../src/PiRadar.Data/PiRadar.Data.csproj
#:package Microsoft.Extensions.DependencyInjection@10.0.0
#:property PublishAot=false

// Genera un respaldo .piradar con datos ficticios (comercios inventados, sin datos reales) para las capturas de
// la tienda. Uso: dotnet run tools/demo-data/demo-backup.cs -- <archivo.piradar> [contraseña] [fecha-hoy yyyy-MM-dd]
// Restaurarlo en Debug copiándolo como backup-in.piradar (ver CLAUDE.md).
using Microsoft.Extensions.DependencyInjection;
using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;
using PiRadar.Data;

var output = args.Length > 0 ? args[0] : "demo.piradar";
var password = args.Length > 1 ? args[1] : "demo1234";
var today = args.Length > 2 ? DateTime.Parse(args[2]) : DateTime.Today;

SQLitePCL.Batteries_V2.Init();
var dir = Directory.CreateTempSubdirectory("piradar-demo");
var host = new DatabaseHost(new MemoryKeys(), Path.Combine(dir.FullName, "demo.db3"));
await host.OpenAsync();

var services = new ServiceCollection().AddSingleton(host).AddPiRadarData().BuildServiceProvider();
var categories = await services.GetRequiredService<ICategoryRepository>().GetAllAsync();
var methods = await services.GetRequiredService<IPaymentMethodRepository>().GetAllAsync();
var transactions = services.GetRequiredService<ITransactionRepository>();
int Cat(string name) => categories.First(c => c.Name == name).Id;
int Pay(string name) => methods.First(m => m.Name == name).Id;

var random = new Random(20260927);
var count = 0;

async Task Add(TransactionType type, string category, long amount, DateTime at, string? payment, string? merchant,
    string? description = null, Currency currency = Currency.COP)
{
    if (at > today.AddHours(20))
    {
        return;
    }

    await transactions.AddAsync(new Transaction
    {
        Type = type,
        Amount = new Money(amount, currency),
        OccurredAt = DateTime.SpecifyKind(at, DateTimeKind.Local),
        CategoryId = Cat(category),
        PaymentMethodId = payment is null ? null : Pay(payment),
        Merchant = merchant,
        Description = description,
    });
    count++;
}

// Redondea a centenas, como los precios en pesos.
long Cop(int min, int max) => random.Next(min, max) / 100 * 100;
DateTime At(DateTime day, int fromHour, int toHour) => day.AddHours(random.Next(fromHour, toHour)).AddMinutes(random.Next(60));
T Pick<T>(params T[] items) => items[random.Next(items.Length)];

var start = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
for (var month = start; month <= today; month = month.AddMonths(1))
{
    await Add(TransactionType.Income, "Salario", 6_800_000, month.AddDays(0).AddHours(9), "Cuenta bancaria", "Empresa Andina", "Salario");
    await Add(TransactionType.Expense, "Vivienda", 1_850_000, month.AddDays(4).AddHours(10), "Cuenta bancaria", "Inmobiliaria del Parque", "Arriendo");
    await Add(TransactionType.Expense, "Servicios públicos", Cop(160_000, 210_000), month.AddDays(9).AddHours(19), "Nequi", "Energía de la Ciudad", "Luz");
    await Add(TransactionType.Expense, "Servicios públicos", Cop(70_000, 95_000), month.AddDays(11).AddHours(19), "Nequi", "Aguas del Valle", "Agua");
    await Add(TransactionType.Expense, "Servicios públicos", 89_900, month.AddDays(14).AddHours(8), "Tarjeta crédito", "Fibra Hogar", "Internet");
    await Add(TransactionType.Expense, "Educación", 320_000, month.AddDays(2).AddHours(11), "Cuenta bancaria", "Instituto de Idiomas", "Curso de inglés");
    await Add(TransactionType.Expense, "Entretenimiento", 1_299, month.AddDays(6).AddHours(22), "Tarjeta crédito", "StreamBox", "Suscripción", Currency.USD);
    await Add(TransactionType.Expense, "Salud", 245_000, month.AddDays(1).AddHours(8), "Cuenta bancaria", "Medicina Prepagada Vital", "Plan de salud");
    if (month.Month % 2 == 0)
    {
        await Add(TransactionType.Income, "Honorarios", Cop(1_200_000, 2_400_000), month.AddDays(17).AddHours(15), "Cuenta bancaria", "Estudio Creativo Norte", "Diseño de logo");
    }

    for (var day = month; day < month.AddMonths(1); day = day.AddDays(1))
    {
        if (day.DayOfWeek == DayOfWeek.Saturday)
        {
            await Add(TransactionType.Expense, "Mercado", Cop(180_000, 420_000), At(day, 10, 13), Pick("Tarjeta débito", "Tarjeta crédito"),
                Pick("Supermercado La Canasta", "Mercados Frescos", "Hipermercado Central"));
        }

        if (random.NextDouble() < 0.25)
        {
            await Add(TransactionType.Expense, "Mercado", Cop(8_000, 45_000), At(day, 7, 20), Pick("Efectivo", "Nequi"),
                Pick("Panadería San José", "Tienda Don Pedro", "Frutería El Mango"));
        }

        if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
        {
            await Add(TransactionType.Expense, "Transporte", Pick(3_200L, 6_400L), At(day, 6, 8), "Tarjeta débito", "Metro Urbano");
            if (random.NextDouble() < 0.5)
            {
                await Add(TransactionType.Expense, "Restaurantes", Cop(16_000, 32_000), At(day, 12, 14), Pick("Tarjeta débito", "Nequi"),
                    Pick("Corrientazo La Esquina", "Café Montaña", "Wok Express", "Sabor Casero"), "Almuerzo");
            }
        }

        if (random.NextDouble() < 0.12)
        {
            await Add(TransactionType.Expense, "Transporte", Cop(14_000, 38_000), At(day, 18, 23), "Daviplata", "Taxi");
        }

        if (day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Sunday && random.NextDouble() < 0.45)
        {
            await Add(TransactionType.Expense, "Restaurantes", Cop(60_000, 180_000), At(day, 19, 22), "Tarjeta crédito",
                Pick("Parrilla del Río", "Trattoria Bella", "Sushi Nami", "Arepas & Co."), "Cena");
        }

        if (random.NextDouble() < 0.05)
        {
            await Add(TransactionType.Expense, "Entretenimiento", Cop(30_000, 90_000), At(day, 15, 21), "Tarjeta crédito",
                Pick("Cine Estrella", "Teatro Municipal", "Bolera Galaxia"));
        }

        if (random.NextDouble() < 0.04)
        {
            await Add(TransactionType.Expense, "Mascotas", Cop(45_000, 160_000), At(day, 10, 18), "Tarjeta débito", "Veterinaria Patitas");
        }

        if (random.NextDouble() < 0.03)
        {
            await Add(TransactionType.Expense, "Ropa", Cop(90_000, 280_000), At(day, 11, 19), "Tarjeta crédito", Pick("Almacén Moda Viva", "Calzado Andes"));
        }

        if (random.NextDouble() < 0.03)
        {
            await Add(TransactionType.Expense, "Salud", Cop(18_000, 75_000), At(day, 9, 19), "Efectivo", "Droguería La Salud");
        }
    }
}

// Algunas compras en dólares y las tasas para el consolidado.
await Add(TransactionType.Expense, "Tecnología", 8_999, today.AddDays(-38).AddHours(21), "Tarjeta crédito", "Gadget Store", "Audífonos", Currency.USD);
await Add(TransactionType.Expense, "Tecnología", 2_450, today.AddDays(-9).AddHours(20), "Tarjeta crédito", "App Market", "Aplicación de fotos", Currency.USD);
var rates = services.GetRequiredService<IExchangeRateRepository>();
for (var month = start; month <= today; month = month.AddMonths(1))
{
    await rates.UpsertAsync(new ExchangeRate { From = Currency.USD, To = Currency.COP, Rate = 3_850m + random.Next(0, 250), EffectiveDate = month });
}

var file = await services.GetRequiredService<IBackupService>().ExportAsync(password);
await File.WriteAllBytesAsync(output, file);
await host.DisposeAsync();
dir.Delete(recursive: true);
Console.WriteLine($"{count} movimientos → {output}");

sealed class MemoryKeys : IDatabaseKeyProvider
{
    private byte[]? _key;
    public Task<byte[]?> TryGetKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_key);
    public Task SaveKeyAsync(byte[] key, CancellationToken cancellationToken = default) { _key = key; return Task.CompletedTask; }
    public Task DeleteKeyAsync(CancellationToken cancellationToken = default) { _key = null; return Task.CompletedTask; }
}
