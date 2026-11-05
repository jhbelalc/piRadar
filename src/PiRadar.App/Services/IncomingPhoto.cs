namespace PiRadar.App.Services;

/// <summary>
/// Imagen compartida desde otra app, a la espera de que <c>ScanReceipt</c> la lea. Solo en memoria. Es estática porque la
/// plataforma la entrega antes de que exista la app Blazor (arranque en frío). Sin bytes = no se pudo leer: la pantalla
/// muestra su error de siempre.
/// </summary>
public static class IncomingPhoto
{
    private static byte[]? _photo;

    /// <summary>Llegó una imagen. Se dispara en el hilo de quien la entrega.</summary>
    public static event Action? Arrived;

    public static bool IsWaiting => Volatile.Read(ref _photo) is not null;

    public static void Set(byte[] photo)
    {
        Volatile.Write(ref _photo, photo);
        Arrived?.Invoke();
    }

    /// <summary>Entrega la imagen una sola vez.</summary>
    public static byte[]? Take() => Interlocked.Exchange(ref _photo, null);
}
