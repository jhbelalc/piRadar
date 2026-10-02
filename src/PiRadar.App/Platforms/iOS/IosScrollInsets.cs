using Foundation;
using UIKit;

namespace PiRadar.App;

/// <summary>
/// Mantiene en cero los márgenes de scroll del WKWebView. La app ya respeta las áreas seguras con CSS
/// (<c>env(safe-area-inset-*)</c>), así que cualquier margen que deje el teclado o un cambio de estado de la app
/// corre la barra inferior y el contenido (se vio la barra subida más o menos lo que mide el teclado).
/// </summary>
internal static class IosScrollInsets
{
    // Los observadores deben seguir vivos mientras dure la app.
    private static readonly List<NSObject> Observers = [];

    private static bool _watching;

    public static void Watch(UIScrollView scrollView)
    {
        if (_watching)
        {
            return;
        }

        _watching = true;
        void Reset()
        {
            scrollView.ContentInset = UIEdgeInsets.Zero;
            scrollView.ScrollIndicatorInsets = UIEdgeInsets.Zero;
            // Fuera de rango tras cerrar el teclado: se vuelve al límite válido.
            var maxY = Math.Max(0, scrollView.ContentSize.Height - scrollView.Bounds.Height);
            if (scrollView.ContentOffset.Y > maxY)
            {
                scrollView.ContentOffset = new CoreGraphics.CGPoint(scrollView.ContentOffset.X, maxY);
            }
            else if (scrollView.ContentOffset.Y < 0)
            {
                scrollView.ContentOffset = new CoreGraphics.CGPoint(scrollView.ContentOffset.X, 0);
            }
        }

        Observers.Add(UIKeyboard.Notifications.ObserveWillHide((_, _) => Reset()));
        Observers.Add(UIKeyboard.Notifications.ObserveDidHide((_, _) => Reset()));
        Observers.Add(UIApplication.Notifications.ObserveDidBecomeActive((_, _) => Reset()));
    }
}
