using SQLite;

namespace PiRadar.Data.Repositories;

/// <summary>Toma la conexión en cada llamada para seguir funcionando después de <see cref="DatabaseHost.ResetAsync"/>.</summary>
internal abstract class RepositoryBase(DatabaseHost host)
{
    protected SQLiteAsyncConnection Connection => host.Database.Connection;

    /// <summary>Corre <paramref name="work"/> en una transacción de BD: validaciones y escrituras son atómicas.</summary>
    protected async Task<T> InTransactionAsync<T>(Func<SQLiteConnection, T> work)
    {
        T result = default!;
        await Connection.RunInTransactionAsync(db => result = work(db));
        return result;
    }

    protected static bool Exists(SQLiteConnection db, string sql, params object[] args) =>
        db.ExecuteScalar<int>($"SELECT EXISTS({sql})", args) == 1;

    protected static string RequireName(string? name, string what, string whatEn)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new Core.Domain.DomainValidationException(
                Core.Domain.AppLocale.Pick($"El nombre de {what} es obligatorio.", $"The {whatEn} name is required."));
        }

        return trimmed;
    }
}
