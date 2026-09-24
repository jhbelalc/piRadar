namespace PiRadar.Core.Domain;

/// <summary>Gasto o ingreso. Se guarda en la tabla <c>Transactions</c>.</summary>
public sealed class Transaction
{
    public Guid Id { get; set; }
    public TransactionType Type { get; set; }

    /// <summary>Siempre positivo; el signo lo da <see cref="Type"/>.</summary>
    public Money Amount { get; set; }

    /// <summary>Fecha y hora local del movimiento; base de las agregaciones.</summary>
    public DateTime OccurredAt { get; set; }

    public int CategoryId { get; set; }
    public int? PaymentMethodId { get; set; }
    public string? Merchant { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public Guid? ReceiptId { get; set; }
    public TransactionSource Source { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Monto con signo: negativo para gastos.</summary>
    public Money SignedAmount => Type == TransactionType.Expense ? -Amount : Amount;
}
