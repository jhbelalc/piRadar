using Android.App;
using Android.Content.PM;
using Android.OS;

namespace PiRadar.App;

// SingleTop: una imagen compartida reutiliza esta pantalla en vez de abrir otra copia de la app (ShareReceiverActivity).
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
