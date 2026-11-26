using PiRadar.Core.Domain;

namespace PiRadar.Core.Import;

/// <summary>Con qué categoría se guarda un nombre del archivo.</summary>
/// <param name="ExistingId">null ⇒ crear una categoría nueva con <paramref name="Name"/>.</param>
public sealed record CategoryChoice(string Name, TransactionType Type, int? ExistingId);

/// <summary>Con qué medio de pago se guarda un nombre del archivo.</summary>
/// <param name="ExistingId">null ⇒ crear uno nuevo con <paramref name="Name"/>.</param>
public sealed record PaymentMethodChoice(string Name, int? ExistingId);

/// <summary>Todo lo necesario para guardar una importación; se escribe en una sola transacción.</summary>
public sealed record ImportPlan(
    string? FileName,
    IReadOnlyList<ImportRow> Rows,
    IReadOnlyList<CategoryChoice> Categories,
    IReadOnlyList<PaymentMethodChoice> PaymentMethods,
    int? FallbackExpenseCategoryId,
    int? FallbackIncomeCategoryId);

/// <summary>Un lote de importación guardado. <paramref name="RemainingCount"/> = movimientos que siguen en él.</summary>
public sealed record ImportBatchInfo(Guid Id, DateTime ImportedAt, string? FileName, int RowCount, int RemainingCount);

public sealed record ImportResult(Guid BatchId, int Imported, int CategoriesCreated, int SubcategoriesCreated, int PaymentMethodsCreated);
