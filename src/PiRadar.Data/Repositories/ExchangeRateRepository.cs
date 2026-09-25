using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;
using PiRadar.Data.Records;

namespace PiRadar.Data.Repositories;

internal sealed class ExchangeRateRepository(DatabaseHost host) : RepositoryBase(host), IExchangeRateRepository
{
    public async Task<IReadOnlyList<ExchangeRate>> GetAllAsync()
    {
        var records = await Connection.Table<ExchangeRateRecord>()
            .OrderBy(r => r.FromCurrency)
            .ThenBy(r => r.ToCurrency)
            .ThenByDescending(r => r.EffectiveDate)
            .ToListAsync();
        return records.Select(r => r.ToDomain()).ToList();
    }

    public Task<ExchangeRate> UpsertAsync(ExchangeRate rate)
    {
        ArgumentNullException.ThrowIfNull(rate);
        if (rate.From == rate.To)
        {
            throw new DomainValidationException("La tasa debe ser entre dos monedas distintas.");
        }

        if (rate.RateMicros <= 0)
        {
            throw new DomainValidationException("La tasa debe ser mayor que cero.");
        }

        var record = rate.ToRecord();
        record.EffectiveDate = record.EffectiveDate.Date;

        return InTransactionAsync(db =>
        {
            var existing = db.Table<ExchangeRateRecord>()
                .Where(r => r.FromCurrency == record.FromCurrency
                    && r.ToCurrency == record.ToCurrency
                    && r.EffectiveDate == record.EffectiveDate)
                .FirstOrDefault();

            if (existing is null)
            {
                record.Id = 0;
                db.Insert(record);
            }
            else
            {
                record.Id = existing.Id;
                db.Update(record);
            }

            return record.ToDomain();
        });
    }

    public async Task<bool> DeleteAsync(int id) => await Connection.DeleteAsync<ExchangeRateRecord>(id) > 0;
}
