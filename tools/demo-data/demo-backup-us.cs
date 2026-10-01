#:project ../../src/PiRadar.Data/PiRadar.Data.csproj
#:package Microsoft.Extensions.DependencyInjection@10.0.0
#:property PublishAot=false

// Igual que demo-backup.cs pero para la ficha en inglés (EE. UU.): nombres de categorías y medios de pago en inglés,
// montos en dólares y comercios inventados. Uso: dotnet run tools/demo-data/demo-backup-us.cs -- <archivo.piradar> [contraseña] [fecha-hoy yyyy-MM-dd]
// Restaurarlo en Debug copiándolo como backup-in.piradar (ver CLAUDE.md).
using Microsoft.Extensions.DependencyInjection;
using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;
using PiRadar.Data;

var output = args.Length > 0 ? args[0] : "demo.piradar";
var password = args.Length > 1 ? args[1] : "demo1234";
var today = args.Length > 2 ? DateTime.Parse(args[2]) : DateTime.Today;

// La semilla (categorías, medios de pago, moneda base) se crea según el idioma vigente.
AppLocale.Language = AppLanguage.English;
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
    string? description = null, Currency currency = Currency.USD)
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

DateTime At(DateTime day, int fromHour, int toHour) => day.AddHours(random.Next(fromHour, toHour)).AddMinutes(random.Next(60));
T Pick<T>(params T[] items) => items[random.Next(items.Length)];

// Montos en centavos de dólar; se redondea a múltiplos de 5 centavos como en un recibo normal.
long Usd(int minCents, int maxCents) => random.Next(minCents, maxCents) / 5 * 5;
var start = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
for (var month = start; month <= today; month = month.AddMonths(1))
{
    await Add(TransactionType.Income, "Salary", 480_000, month.AddDays(0).AddHours(9), "Bank account", "Northwind Labs", "Paycheck");
    await Add(TransactionType.Expense, "Housing", 165_000, month.AddDays(4).AddHours(10), "Bank account", "Oakwood Apartments", "Rent");
    await Add(TransactionType.Expense, "Utilities", Usd(9_000, 14_500), month.AddDays(9).AddHours(19), "Venmo", "Northwind Electric", "Electric");
    await Add(TransactionType.Expense, "Utilities", Usd(3_800, 6_200), month.AddDays(11).AddHours(19), "Venmo", "Clearwater Water", "Water");
    await Add(TransactionType.Expense, "Utilities", 6_999, month.AddDays(14).AddHours(8), "Credit card", "FiberLink Internet", "Internet");
    await Add(TransactionType.Expense, "Education", 2_900, month.AddDays(2).AddHours(11), "Bank account", "Skillshare Hub", "Online course");
    await Add(TransactionType.Expense, "Entertainment", 1_599, month.AddDays(6).AddHours(22), "Credit card", "StreamBox", "Subscription");
    await Add(TransactionType.Expense, "Health", 21_000, month.AddDays(1).AddHours(8), "Bank account", "Vital Health Plan", "Health plan");
    if (month.Month % 2 == 0)
    {
        await Add(TransactionType.Income, "Freelance", Usd(60_000, 120_000), month.AddDays(17).AddHours(15), "Bank account", "Creative Studio North", "Logo design");
    }

    for (var day = month; day < month.AddMonths(1); day = day.AddDays(1))
    {
        if (day.DayOfWeek == DayOfWeek.Saturday)
        {
            await Add(TransactionType.Expense, "Groceries", Usd(9_000, 21_000), At(day, 10, 13), Pick("Debit card", "Credit card"),
                Pick("Greenfield Grocers", "Maple Street Market", "Central Supermarket"));
        }

        if (random.NextDouble() < 0.25)
        {
            await Add(TransactionType.Expense, "Groceries", Usd(400, 2_300), At(day, 7, 20), Pick("Cash", "Venmo"),
                Pick("Daily Bread Bakery", "Corner Produce", "Sunny Fruit Stand"));
        }

        if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
        {
            await Add(TransactionType.Expense, "Transportation", Pick(275L, 550L), At(day, 6, 8), "Debit card", "Metro Transit");
            if (random.NextDouble() < 0.5)
            {
                await Add(TransactionType.Expense, "Restaurants", Usd(1_100, 2_200), At(day, 12, 14), Pick("Debit card", "Venmo"),
                    Pick("Riverside Diner", "Mountain Cafe", "Wok Express", "Home Kitchen Deli"), "Lunch");
            }
        }

        if (random.NextDouble() < 0.12)
        {
            await Add(TransactionType.Expense, "Transportation", Usd(1_200, 3_000), At(day, 18, 23), "Zelle", "City Rides");
        }

        if (day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Sunday && random.NextDouble() < 0.45)
        {
            await Add(TransactionType.Expense, "Restaurants", Usd(4_500, 11_000), At(day, 19, 22), "Credit card",
                Pick("Blue Fork Bistro", "Trattoria Bella", "Sushi Nami", "The Grill House"), "Dinner");
        }

        if (random.NextDouble() < 0.05)
        {
            await Add(TransactionType.Expense, "Entertainment", Usd(1_800, 5_500), At(day, 15, 21), "Credit card",
                Pick("Star Cinema", "Municipal Theater", "Galaxy Bowling"));
        }

        if (random.NextDouble() < 0.04)
        {
            await Add(TransactionType.Expense, "Pets", Usd(3_000, 9_500), At(day, 10, 18), "Debit card", "Paws Vet Clinic");
        }

        if (random.NextDouble() < 0.03)
        {
            await Add(TransactionType.Expense, "Clothing", Usd(4_000, 12_000), At(day, 11, 19), "Credit card", Pick("Urban Threads", "Trail Shoes Co."));
        }

        if (random.NextDouble() < 0.03)
        {
            await Add(TransactionType.Expense, "Health", Usd(1_200, 4_500), At(day, 9, 19), "Cash", "Sunrise Pharmacy");
        }
    }
}

await Add(TransactionType.Expense, "Technology", 8_999, today.AddDays(-38).AddHours(21), "Credit card", "Gadget Store", "Headphones");
await Add(TransactionType.Expense, "Technology", 2_450, today.AddDays(-9).AddHours(20), "Credit card", "App Market", "Photo app");

var file = await services.GetRequiredService<IBackupService>().ExportAsync(password);
await File.WriteAllBytesAsync(output, file);
await host.DisposeAsync();
dir.Delete(recursive: true);
Console.WriteLine($"{count} transactions → {output}");

sealed class MemoryKeys : IDatabaseKeyProvider
{
    private byte[]? _key;
    public Task<byte[]?> TryGetKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_key);
    public Task SaveKeyAsync(byte[] key, CancellationToken cancellationToken = default) { _key = key; return Task.CompletedTask; }
    public Task DeleteKeyAsync(CancellationToken cancellationToken = default) { _key = null; return Task.CompletedTask; }
}
