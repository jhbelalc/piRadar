using PiRadar.Core.Domain;

namespace PiRadar.Core.Abstractions;

/// <summary>
/// Traduce los nombres de las categorías y medios de pago que trae la app cuando el usuario cambia de idioma.
/// Solo toca filas <c>IsSystem</c> que todavía tienen el nombre original en el otro idioma: lo que el usuario
/// renombró o creó no se modifica.
/// </summary>
public interface ISystemNameLocalizer
{
    /// <summary>Renombra al idioma <paramref name="target"/>. Devuelve cuántas filas cambiaron.</summary>
    Task<int> ApplyAsync(AppLanguage target);
}
