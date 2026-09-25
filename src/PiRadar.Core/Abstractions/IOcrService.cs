using PiRadar.Core.Receipts;

namespace PiRadar.Core.Abstractions;

/// <summary>OCR local en el dispositivo (ML Kit en Android, Vision en iOS). Nunca usa la red.</summary>
public interface IOcrService
{
    /// <param name="image">JPEG; la implementación aplica la rotación EXIF si el motor no la respeta.</param>
    Task<OcrPage> RecognizeAsync(byte[] image, CancellationToken cancellationToken = default);
}
