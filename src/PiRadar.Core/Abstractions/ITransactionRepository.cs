using PiRadar.Core.Domain;

namespace PiRadar.Core.Abstractions;

public interface ITransactionRepository
{
    Task<Transaction?> GetAsync(Guid id);

    Task<IReadOnlyList<Transaction>> QueryAsync(TransactionFilter filter);

    /// <summary>
    /// Guarda un movimiento manual. Genera el <c>Id</c> si viene vacío y fija <c>CreatedAt</c>/<c>UpdatedAt</c>.
    /// Devuelve el movimiento tal como quedó guardado.
    /// </summary>
    /// <exception cref="DomainValidationException"/>
    Task<Transaction> AddAsync(Transaction transaction);

    /// <summary>
    /// Guarda en una sola transacción de BD el recibo, su imagen y el movimiento (con <c>Source = Ocr</c>).
    /// </summary>
    /// <exception cref="DomainValidationException"/>
    Task<Transaction> AddWithReceiptAsync(Transaction transaction, Receipt receipt, ReceiptImage image);

    /// <summary>Actualiza los datos editables. Conserva <c>CreatedAt</c>, <c>Source</c> y <c>ReceiptId</c>.</summary>
    /// <exception cref="DomainValidationException"/>
    /// <exception cref="KeyNotFoundException"/>
    Task<Transaction> UpdateAsync(Transaction transaction);

    /// <summary>Borra el movimiento y, si tiene, su recibo e imagen. Devuelve <c>false</c> si no existía.</summary>
    Task<bool> DeleteAsync(Guid id);
}
