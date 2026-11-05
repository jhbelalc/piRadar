using Android.App;
using Android.Content;
using Android.OS;
using PiRadar.App.Services;

namespace PiRadar.App;

/// <summary>
/// Recibe la imagen que otra app comparte con piRadar y abre la app. Es aparte de <see cref="MainActivity"/> para que la
/// app no se abra como una segunda copia dentro de la app que comparte. Transparente y no NoDisplay, porque la lectura
/// termina después de onResume.
/// </summary>
[Activity(Label = "piRadar", Exported = true, NoHistory = true, ExcludeFromRecents = true,
    Theme = "@android:style/Theme.Translucent.NoTitleBar")]
[IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault], DataMimeType = "image/*")]
public class ShareReceiverActivity : Activity
{
    /// <summary>Muy por encima de cualquier foto: evita agotar la memoria con lo que mande otra app.</summary>
    private const int MaxBytes = 64 * 1024 * 1024;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var uri = SharedUri();

        // Fuera del hilo de la interfaz: una foto grande leída ahí congela la app. El permiso sobre la URI dura mientras
        // viva esta actividad, por eso se lee antes de cerrarla.
        _ = Task.Run(() =>
        {
            var photo = Read(uri);
            RunOnUiThread(() =>
            {
                IncomingPhoto.Set(photo);
                var open = new Intent(this, typeof(MainActivity));
                open.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
                StartActivity(open);
                Finish();
            });
        });
    }

    private Android.Net.Uri? SharedUri()
    {
#pragma warning disable CA1422, CS0618 // La versión con clase solo existe desde Android 13.
        var uri = OperatingSystem.IsAndroidVersionAtLeast(33)
            ? Intent?.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(Android.Net.Uri))) as Android.Net.Uri
            : Intent?.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri;
#pragma warning restore CA1422, CS0618
        return uri ?? (Intent?.ClipData is { ItemCount: > 0 } clip ? clip.GetItemAt(0)?.Uri : null);
    }

    /// <summary>Sin bytes si no se pudo: la app abre y muestra el error de imagen ilegible.</summary>
    private byte[] Read(Android.Net.Uri? uri)
    {
        // Solo content://: una ruta file:// de otra app podría apuntar a los archivos privados de piRadar.
        if (uri?.Scheme != ContentResolver.SchemeContent)
        {
            return [];
        }

        try
        {
            using var stream = ContentResolver?.OpenInputStream(uri);
            if (stream is null)
            {
                return [];
            }

            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                memory.Write(buffer, 0, read);
                if (memory.Length > MaxBytes)
                {
                    return [];
                }
            }

            return memory.ToArray();
        }
        catch (Exception ex) when (ex is IOException or Java.Lang.Exception)
        {
            return [];
        }
    }
}
