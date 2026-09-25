using PiRadar.Core.Domain;

namespace PiRadar.Data.Tests.Repositories;

internal static class TestData
{
    public static Transaction Expense(int categoryId, long amountMinor = 82150, DateTime? occurredAt = null,
        Currency currency = Currency.COP, int? paymentMethodId = null) => new()
    {
        Type = TransactionType.Expense,
        Amount = new Money(amountMinor, currency),
        OccurredAt = occurredAt ?? new DateTime(2026, 9, 14, 18, 42, 0, DateTimeKind.Local),
        CategoryId = categoryId,
        PaymentMethodId = paymentMethodId,
        Merchant = "Supermercado El Éxito",
    };
}
