using Foundation;
using UIKit;

namespace PiRadar.ShareExtension;

/// <summary>
/// Copia la imagen compartida a <c>inbox/</c> del App Group y avisa que se abra piRadar: una extensión no puede abrir su
/// app. No decodifica la imagen (límite de memoria de las extensiones) ni la lee: eso lo hace la app.
/// </summary>
[Register("ShareViewController")]
public class ShareViewController : UIViewController
{
    private const string AppGroup = "group.com.jhbelalc.piradar";
    private const string ImageType = "public.image";

    private bool _started;

    /// <summary>iOS crea esta pantalla por su cuenta: sin este constructor .NET no puede enlazarla y la extensión se cierra.</summary>
    protected ShareViewController(ObjCRuntime.NativeHandle handle)
        : base(handle)
    {
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        if (_started)
        {
            return;
        }

        _started = true;
        var provider = ExtensionContext?.InputItems
            .SelectMany(item => item.Attachments ?? [])
            .FirstOrDefault(attachment => attachment.HasItemConformingTo(ImageType));
        if (provider is null)
        {
            Finish(saved: false);
            return;
        }

        provider.LoadItem(ImageType, null, (item, error) =>
        {
            var saved = error is null && Save(item);
            InvokeOnMainThread(() => Finish(saved));
        });
    }

    /// <summary>La app que comparte entrega un archivo (Fotos), bytes o una imagen en memoria (algunas apps).</summary>
    private static bool Save(NSObject? item)
    {
        var folder = NSFileManager.DefaultManager.GetContainerUrl(AppGroup)?.Append("inbox", true);
        if (folder?.Path is not { } path)
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(path);
            var target = Path.Combine(path, $"{Guid.NewGuid():N}.image");
            switch (item)
            {
                case NSUrl { Path: { } source }:
                    File.Copy(source, target);
                    break;
                case NSData data:
                    File.WriteAllBytes(target, data.ToArray());
                    break;
                case UIImage image when image.AsPNG() is { } png:
                    File.WriteAllBytes(target, png.ToArray());
                    break;
                default:
                    return false;
            }

            // Fuera de la BD cifrada hasta que se abra piRadar: ilegible con el teléfono bloqueado y fuera de los respaldos.
            NSFileManager.DefaultManager.SetAttributes(new NSFileAttributes { ProtectionKey = NSFileProtection.Complete }, target);
            NSUrl.FromFilename(target).SetResource(NSUrl.IsExcludedFromBackupKey, NSNumber.FromBoolean(true));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void Finish(bool saved)
    {
        var english = NSLocale.PreferredLanguages.FirstOrDefault()?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;
        var (title, message) = (saved, english) switch
        {
            (true, false) => ("Listo", "Abre piRadar para continuar."),
            (true, true) => ("Done", "Open piRadar to continue."),
            (false, false) => ("No se pudo enviar", "Prueba desde piRadar con Escanear."),
            (false, true) => ("Could not send", "Try from piRadar with Scan."),
        };

        var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ => ExtensionContext?.CompleteRequest([], null)));
        PresentViewController(alert, true, null);
    }
}
