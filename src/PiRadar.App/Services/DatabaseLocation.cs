using PiRadar.Data;

namespace PiRadar.App.Services;

public static class DatabaseLocation
{
    /// <summary>
    /// Ruta de la BD dentro de una carpeta propia. En iOS la carpeta se excluye del respaldo de iCloud/iTunes:
    /// la clave del Keychain puede no restaurarse y la BD quedaría ilegible.
    /// En Android lo excluyen <c>allowBackup=false</c> y <c>data_extraction_rules.xml</c>.
    /// </summary>
    public static string GetDatabasePath()
    {
        var directory = Path.Combine(FileSystem.AppDataDirectory, "db");
        Directory.CreateDirectory(directory);
        ExcludeFromBackup(directory);
        return Path.Combine(directory, PiRadarDatabase.FileName);
    }

    private static void ExcludeFromBackup(string directory)
    {
#if IOS
        var url = Foundation.NSUrl.FromFilename(directory);
        if (!url.SetResource(Foundation.NSUrl.IsExcludedFromBackupKey, Foundation.NSNumber.FromBoolean(true), out var error))
        {
            throw new IOException($"No se pudo excluir la base de datos del respaldo: {error?.LocalizedDescription}");
        }
#endif
    }
}
