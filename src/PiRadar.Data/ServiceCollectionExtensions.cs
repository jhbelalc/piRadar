using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PiRadar.Core.Abstractions;
using PiRadar.Data.Backup;
using PiRadar.Data.Repositories;

namespace PiRadar.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra los repositorios. <see cref="DatabaseHost"/> se registra aparte porque la ruta de la BD depende de
    /// la plataforma. Los repositorios solo se pueden usar con la BD abierta (dentro de <c>DatabaseGate</c>).
    /// </summary>
    public static IServiceCollection AddPiRadarData(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<IPaymentMethodRepository, PaymentMethodRepository>();
        services.AddSingleton<ITransactionRepository, TransactionRepository>();
        services.AddSingleton<IReceiptRepository, ReceiptRepository>();
        services.AddSingleton<IExchangeRateRepository, ExchangeRateRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<ISystemNameLocalizer, SystemNameLocalizer>();
        services.AddSingleton<IBackupService, BackupService>();
        return services;
    }
}
