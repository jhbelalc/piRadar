import Foundation
import ImageIO
import UniformTypeIdentifiers
// uso: bake <entrada> <salida> <maxPx|0>  → JPEG 90 % con la orientación EXIF aplicada a los píxeles
let a = CommandLine.arguments
let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: a[1]) as CFURL, nil)!
let props = CGImageSourceCopyPropertiesAtIndex(src, 0, nil) as! [CFString: Any]
let w = props[kCGImagePropertyPixelWidth] as! Int, h = props[kCGImagePropertyPixelHeight] as! Int
let maxPx = Int(a[3])! == 0 ? max(w, h) : Int(a[3])!
let opts: [CFString: Any] = [kCGImageSourceCreateThumbnailFromImageAlways: true,
    kCGImageSourceCreateThumbnailWithTransform: true, kCGImageSourceThumbnailMaxPixelSize: maxPx]
let img = CGImageSourceCreateThumbnailAtIndex(src, 0, opts as CFDictionary)!
let dst = CGImageDestinationCreateWithURL(URL(fileURLWithPath: a[2]) as CFURL, UTType.jpeg.identifier as CFString, 1, nil)!
CGImageDestinationAddImage(dst, img, [kCGImageDestinationLossyCompressionQuality: 0.9] as CFDictionary)
CGImageDestinationFinalize(dst)
print("\(img.width)x\(img.height)")
