using PiRadar.Core.Domain;

namespace PiRadar.App.Components.Shared;

public static class PaymentMethodText
{
    public static string Kind(PaymentMethodKind kind) => kind switch
    {
        PaymentMethodKind.Cash => "Efectivo",
        PaymentMethodKind.DebitCard => "Tarjeta débito",
        PaymentMethodKind.CreditCard => "Tarjeta crédito",
        PaymentMethodKind.BankAccount => "Cuenta bancaria",
        PaymentMethodKind.DigitalWallet => "Billetera digital",
        _ => "Otro",
    };

    /// <summary>"Tarjeta crédito ···4321" cuando tiene últimos dígitos.</summary>
    public static string Display(PaymentMethod method) =>
        string.IsNullOrEmpty(method.LastFourDigits) ? method.Name : $"{method.Name} ···{method.LastFourDigits}";
}
