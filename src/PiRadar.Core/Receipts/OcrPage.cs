namespace PiRadar.Core.Receipts;

/// <summary>
/// Palabra o línea reconocida con su recuadro en píxeles de la imagen (origen arriba a la izquierda).
/// En iOS todas las palabras de una línea comparten el recuadro de la línea; en Android cada palabra trae el suyo.
/// </summary>
public sealed record OcrElement(string Text, double X, double Y, double Width, double Height);

/// <summary>Resultado del OCR independiente del motor. <see cref="Elements"/> no viene en orden de lectura.</summary>
public sealed record OcrPage(IReadOnlyList<OcrElement> Elements, string Engine);
