using Foundation;
using UIKit;

namespace PiRadar.App;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	private NSObject? _activeObserver;

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
	{
		// Al abrir o volver a la app: la extensión de compartir pudo dejar una imagen.
		_activeObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidBecomeActiveNotification, _ => ShareInbox.Check());
		return base.FinishedLaunching(application, launchOptions!);
	}
}
