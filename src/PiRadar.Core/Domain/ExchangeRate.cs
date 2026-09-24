namespace PiRadar.Core.Domain;

/// <summary>Tasa ingresada manualmente: 1 <see cref="From"/> = <see cref="Rate"/> <see cref="To"/>.</summary>
public sealed class ExchangeRate
{
    public const long MicrosPerUnit = 1_000_000;

    public int Id { get; set; }
    public Currency From { get; set; }
    public Currency To { get; set; }

    /// <summary>Tasa × 10⁶, para no guardar decimales en SQLite.</summary>
    public long RateMicros { get; set; }

    /// <summary>Fecha local desde la que aplica la tasa.</summary>
    public DateTime EffectiveDate { get; set; }

    public decimal Rate
    {
        get => (decimal)RateMicros / MicrosPerUnit;
        set => RateMicros = checked((long)decimal.Round(value * MicrosPerUnit, MidpointRounding.AwayFromZero));
    }
}
