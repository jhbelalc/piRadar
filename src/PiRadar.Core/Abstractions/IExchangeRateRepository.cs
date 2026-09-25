using PiRadar.Core.Domain;

namespace PiRadar.Core.Abstractions;

public interface IExchangeRateRepository
{
    /// <summary>Todas las tasas, por par y de la más reciente a la más antigua.</summary>
    Task<IReadOnlyList<ExchangeRate>> GetAllAsync();

    /// <summary>
    /// Guarda la tasa del par para su fecha (se ignora la hora). Si ya había una para ese par y fecha, la reemplaza.
    /// Devuelve la tasa guardada con su <c>Id</c>.
    /// </summary>
    /// <exception cref="DomainValidationException"/>
    Task<ExchangeRate> UpsertAsync(ExchangeRate rate);

    Task<bool> DeleteAsync(int id);
}
