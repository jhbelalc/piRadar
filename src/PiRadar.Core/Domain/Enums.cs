namespace PiRadar.Core.Domain;

// Los valores numéricos se guardan en la BD: no reordenar ni reutilizar números.

public enum TransactionType
{
    Expense = 1,
    Income = 2,
}

public enum TransactionSource
{
    Manual = 1,
    Ocr = 2,
}

public enum PaymentMethodKind
{
    Cash = 1,
    DebitCard = 2,
    CreditCard = 3,
    BankAccount = 4,
    DigitalWallet = 5,
    Other = 99,
}
