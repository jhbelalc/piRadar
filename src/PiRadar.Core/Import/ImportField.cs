namespace PiRadar.Core.Import;

/// <summary>Campo de un movimiento al que se puede asignar una columna del archivo. Valores explícitos: no reordenar.</summary>
public enum ImportField
{
    Ignore = 0,
    Date = 1,
    Time = 2,
    Amount = 3,
    Income = 4,
    Expense = 5,
    Type = 6,
    Category = 7,
    Subcategory = 8,
    PaymentMethod = 9,
    Currency = 10,
    Description = 11,
    Merchant = 12,
    Notes = 13,
}

/// <summary>Cómo se obtienen tipo y monto de una fila.</summary>
public enum AmountMode
{
    None,
    /// <summary>Una columna de monto con signo.</summary>
    Signed,
    /// <summary>Columnas separadas de ingreso y/o gasto.</summary>
    IncomeExpense,
    /// <summary>Monto más una columna de tipo.</summary>
    AmountWithType,
}

/// <summary>Orden de día y mes en fechas escritas como texto.</summary>
public enum DateOrder
{
    DayFirst,
    MonthFirst,
}
