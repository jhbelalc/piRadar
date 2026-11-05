using Foundation;
using PiRadar.App.Services;

namespace PiRadar.App;

/// <summary>
/// Recoge la imagen que dejó la extensión de compartir en el App Group. La carpeta queda vacía: de ahí en adelante la
/// imagen vive solo en memoria y, al guardar, en la BD cifrada.
/// </summary>
public static class ShareInbox
{
    private const string AppGroup = "group.com.jhbelalc.piradar";

    public static void Check() => _ = Task.Run(() =>
    {
        if (FolderPath() is not { } path || !Directory.Exists(path))
        {
            return;
        }

        var files = new DirectoryInfo(path).GetFiles().OrderByDescending(f => f.LastWriteTimeUtc).ToList();
        if (files.Count == 0)
        {
            return;
        }

        byte[] photo;
        try
        {
            photo = File.ReadAllBytes(files[0].FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            photo = [];
        }

        foreach (var file in files)
        {
            file.Delete();
        }

        MainThread.BeginInvokeOnMainThread(() => IncomingPhoto.Set(photo));
    });

    /// <summary>
    /// En el simulador la firma ad hoc no da acceso al App Group: en Debug se prueba con una carpeta <c>share-in</c> propia.
    /// </summary>
    private static string? FolderPath() =>
#if DEBUG
        Path.Combine(FileSystem.AppDataDirectory, "share-in");
#else
        NSFileManager.DefaultManager.GetContainerUrl(AppGroup)?.Append("inbox", true).Path;
#endif
}
