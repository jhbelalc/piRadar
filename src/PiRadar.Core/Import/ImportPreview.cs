using PiRadar.Core.Domain;

namespace PiRadar.Core.Import;

/// <summary>Opciones para convertir las filas de una hoja en movimientos.</summary>
/// <param name="NegativeIsExpense">Con una sola columna de monto con signo: true ⇒ negativo = gasto; false ⇒ negativo = ingreso.</param>
public sealed record ImportOptions(
    ImportMapping Mapping,
    DateOrder DateOrder,
    Currency DefaultCurrency,
    bool NegativeIsExpense = true);

/// <summary>Movimiento propuesto a partir de una fila.</summary>
/// <param name="RowNumber">Número de fila de Excel (índice + 1).</param>
public sealed record ImportRow(
    int RowNumber,
    TransactionType Type,
    Money Amount,
    DateTime OccurredAt,
    string? Category,
    string? Subcategory,
    string? PaymentMethod,
    string? Description,
    string? Merchant,
    string? Notes,
    bool Rounded,
    bool LooksLikeOpeningBalance);

/// <summary>Problema de una fila; el mensaje ya trae el número de fila.</summary>
public sealed record ImportIssue(int RowNumber, string Message);

public sealed record CurrencySummary(Currency Currency, int Count, Money Income, Money Expense, DateTime From, DateTime To);

public sealed record ImportName(string Name, TransactionType Type, int Count);

public sealed record ImportSubName(string Parent, string Name, TransactionType Type, int Count);

/// <summary>Resultado de convertir una hoja: filas válidas, errores y resúmenes para la vista previa.</summary>
public sealed class ImportPreview
{
    public IReadOnlyList<ImportRow> Rows { get; init; } = [];
    public IReadOnlyList<ImportIssue> Errors { get; init; } = [];

    /// <summary>Números de fila cuyo monto se redondeó.</summary>
    public IReadOnlyList<int> RoundedRows { get; init; } = [];

    /// <summary>Números de fila que parecen un saldo anterior (siguen en <see cref="Rows"/>).</summary>
    public IReadOnlyList<int> OpeningBalanceRows { get; init; } = [];

    public int SkippedEmpty { get; init; }
    public int SkippedTotals { get; init; }
    public int SkippedZero { get; init; }

    /// <summary>Por moneda, COP primero.</summary>
    public IReadOnlyList<CurrencySummary> Totals { get; init; } = [];

    public IReadOnlyList<ImportName> Categories { get; init; } = [];
    public IReadOnlyList<ImportSubName> Subcategories { get; init; } = [];
    public IReadOnlyList<ImportName> PaymentMethods { get; init; } = [];
}
