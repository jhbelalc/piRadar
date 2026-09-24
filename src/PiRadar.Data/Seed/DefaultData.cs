using PiRadar.Core.Domain;

namespace PiRadar.Data.Seed;

/// <summary>Datos iniciales que inserta la migración 1. Iconos Material; colores #RRGGBB.</summary>
internal static class DefaultData
{
    public static readonly IReadOnlyList<(string Name, TransactionType Kind, string Icon, string Color)> Categories =
    [
        ("Mercado", TransactionType.Expense, "shopping_cart", "#43A047"),
        ("Restaurantes", TransactionType.Expense, "restaurant", "#FB8C00"),
        ("Transporte", TransactionType.Expense, "directions_bus", "#1E88E5"),
        ("Servicios públicos", TransactionType.Expense, "receipt_long", "#8E24AA"),
        ("Vivienda", TransactionType.Expense, "home", "#6D4C41"),
        ("Salud", TransactionType.Expense, "local_hospital", "#E53935"),
        ("Educación", TransactionType.Expense, "school", "#3949AB"),
        ("Entretenimiento", TransactionType.Expense, "movie", "#D81B60"),
        ("Ropa", TransactionType.Expense, "checkroom", "#00897B"),
        ("Tecnología", TransactionType.Expense, "devices", "#546E7A"),
        ("Mascotas", TransactionType.Expense, "pets", "#795548"),
        ("Otros gastos", TransactionType.Expense, "more_horiz", "#757575"),
        ("Salario", TransactionType.Income, "payments", "#2E7D32"),
        ("Honorarios", TransactionType.Income, "work", "#00796B"),
        ("Inversiones", TransactionType.Income, "trending_up", "#0277BD"),
        ("Otros ingresos", TransactionType.Income, "add_circle", "#616161"),
    ];

    public static readonly IReadOnlyList<(string Name, PaymentMethodKind Kind, string Icon)> PaymentMethods =
    [
        ("Efectivo", PaymentMethodKind.Cash, "payments"),
        ("Tarjeta débito", PaymentMethodKind.DebitCard, "credit_card"),
        ("Tarjeta crédito", PaymentMethodKind.CreditCard, "credit_card"),
        ("Cuenta bancaria", PaymentMethodKind.BankAccount, "account_balance"),
        ("Nequi", PaymentMethodKind.DigitalWallet, "smartphone"),
        ("Daviplata", PaymentMethodKind.DigitalWallet, "smartphone"),
    ];
}
