import UIKit

/// Copia la imagen compartida a `inbox/` del App Group y pide abrir piRadar: una extensión no puede abrir su app.
/// No decodifica la imagen (límite de memoria de las extensiones): eso lo hace la app, que la recoge con `ShareInbox`.
@objc(ShareViewController)
final class ShareViewController: UIViewController {
    private static let appGroup = "group.com.jhbelalc.piradar"
    private static let imageType = "public.image"

    private var started = false

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        guard !started else { return }
        started = true

        let items = extensionContext?.inputItems.compactMap { $0 as? NSExtensionItem } ?? []
        let provider = items
            .flatMap { $0.attachments ?? [] }
            .first { $0.hasItemConformingToTypeIdentifier(Self.imageType) }
        guard let provider else {
            finish(saved: false)
            return
        }

        provider.loadItem(forTypeIdentifier: Self.imageType, options: nil) { [weak self] item, error in
            let saved = error == nil && Self.save(item)
            DispatchQueue.main.async { self?.finish(saved: saved) }
        }
    }

    /// La app que comparte entrega un archivo (Fotos), bytes o una imagen en memoria (algunas apps).
    private static func save(_ item: NSSecureCoding?) -> Bool {
        let files = FileManager.default
        guard let group = files.containerURL(forSecurityApplicationGroupIdentifier: appGroup) else { return false }
        let folder = group.appendingPathComponent("inbox", isDirectory: true)
        var target = folder.appendingPathComponent("\(UUID().uuidString).image")

        do {
            try files.createDirectory(at: folder, withIntermediateDirectories: true)
            switch item {
            case let source as URL:
                try files.copyItem(at: source, to: target)
            case let data as Data:
                try data.write(to: target)
            case let image as UIImage:
                guard let png = image.pngData() else { return false }
                try png.write(to: target)
            default:
                return false
            }

            // Fuera de la BD cifrada hasta que se abra piRadar: ilegible con el teléfono bloqueado y fuera de los respaldos.
            try files.setAttributes([.protectionKey: FileProtectionType.complete], ofItemAtPath: target.path)
            var values = URLResourceValues()
            values.isExcludedFromBackup = true
            try target.setResourceValues(values)
            return true
        } catch {
            return false
        }
    }

    private func finish(saved: Bool) {
        let english = Locale.preferredLanguages.first?.lowercased().hasPrefix("en") == true
        let title: String
        let message: String
        switch (saved, english) {
        case (true, false): (title, message) = ("Listo", "Abre piRadar para continuar.")
        case (true, true): (title, message) = ("Done", "Open piRadar to continue.")
        case (false, false): (title, message) = ("No se pudo enviar", "Prueba desde piRadar con Escanear.")
        case (false, true): (title, message) = ("Could not send", "Try from piRadar with Scan.")
        }

        let alert = UIAlertController(title: title, message: message, preferredStyle: .alert)
        alert.addAction(UIAlertAction(title: "OK", style: .default) { [weak self] _ in
            self?.extensionContext?.completeRequest(returningItems: [], completionHandler: nil)
        })
        present(alert, animated: true)
    }
}
