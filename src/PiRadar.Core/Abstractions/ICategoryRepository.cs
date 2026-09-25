using PiRadar.Core.Domain;

namespace PiRadar.Core.Abstractions;

public interface ICategoryRepository
{
    /// <summary>Categorías ordenadas por <c>SortOrder</c>; opcionalmente solo de un tipo.</summary>
    Task<IReadOnlyList<Category>> GetAllAsync(TransactionType? kind = null, bool includeArchived = false);

    Task<Category?> GetAsync(int id);

    /// <summary>Crea la categoría y la devuelve con su <c>Id</c>. Queda al final del orden.</summary>
    /// <exception cref="DomainValidationException"/>
    Task<Category> AddAsync(Category category);

    /// <exception cref="DomainValidationException"/>
    /// <exception cref="KeyNotFoundException"/>
    Task UpdateAsync(Category category);

    /// <summary>Borra la categoría, o la archiva si tiene movimientos o subcategorías.</summary>
    /// <exception cref="KeyNotFoundException"/>
    Task<RemovalResult> DeleteOrArchiveAsync(int id);
}
