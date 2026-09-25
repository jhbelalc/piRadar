using PiRadar.Core.Domain;

namespace PiRadar.Core.Abstractions;

public interface IPaymentMethodRepository
{
    /// <summary>Medios de pago ordenados por <c>SortOrder</c>.</summary>
    Task<IReadOnlyList<PaymentMethod>> GetAllAsync(bool includeArchived = false);

    Task<PaymentMethod?> GetAsync(int id);

    /// <summary>Crea el medio de pago y lo devuelve con su <c>Id</c>. Queda al final del orden.</summary>
    /// <exception cref="DomainValidationException"/>
    Task<PaymentMethod> AddAsync(PaymentMethod paymentMethod);

    /// <exception cref="DomainValidationException"/>
    /// <exception cref="KeyNotFoundException"/>
    Task UpdateAsync(PaymentMethod paymentMethod);

    /// <summary>Borra el medio de pago, o lo archiva si tiene movimientos.</summary>
    /// <exception cref="KeyNotFoundException"/>
    Task<RemovalResult> DeleteOrArchiveAsync(int id);
}
