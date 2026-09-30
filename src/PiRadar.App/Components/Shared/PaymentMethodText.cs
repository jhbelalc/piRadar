using PiRadar.Core.Domain;

namespace PiRadar.App.Components.Shared;

public static class PaymentMethodText
{
    public static string Kind(PaymentMethodKind kind) => kind switch
    {
        PaymentMethodKind.Cash => AppLocale.Pick("Efectivo", "Cash"),
        PaymentMethodKind.DebitCard => AppLocale.Pick("Tarjeta débito", "Debit card"),
        PaymentMethodKind.CreditCard => AppLocale.Pick("Tarjeta crédito", "Credit card"),
        PaymentMethodKind.BankAccount => AppLocale.Pick("Cuenta bancaria", "Bank account"),
        PaymentMethodKind.DigitalWallet => AppLocale.Pick("Billetera digital", "Digital wallet"),
        _ => AppLocale.Pick("Otro", "Other"),
    };

    /// <summary>"Tarjeta crédito ···4321" cuando tiene últimos dígitos.</summary>
    public static string Display(PaymentMethod method) =>
        string.IsNullOrEmpty(method.LastFourDigits) ? method.Name : $"{method.Name} ···{method.LastFourDigits}";
}
