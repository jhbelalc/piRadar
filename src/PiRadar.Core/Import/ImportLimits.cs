using PiRadar.Core.Domain;

namespace PiRadar.Core.Import;

/// <summary>Límites para leer archivos de importación.</summary>
public static class ImportLimits
{
    public const long MaxFileBytes = 20L * 1024 * 1024;
    public const long MaxPartUncompressedBytes = 200L * 1024 * 1024;
    public const int MaxDataRows = 50_000;
    public const int MaxColumns = 100;
}

/// <summary>Límites inyectables para las pruebas.</summary>
internal sealed record ImportLimitSet(long MaxFileBytes, long MaxPartUncompressedBytes, int MaxDataRows, int MaxColumns)
{
    public static ImportLimitSet Default { get; } = new(
        ImportLimits.MaxFileBytes, ImportLimits.MaxPartUncompressedBytes, ImportLimits.MaxDataRows, ImportLimits.MaxColumns);

    /// <summary>Se admiten unas filas más por los encabezados y filas de título.</summary>
    public int MaxRows => MaxDataRows + 50;
}

internal static class ImportIO
{
    public static ImportFormatException TooBig() => new(AppLocale.Pick(
        "El archivo es demasiado grande para importarlo.", "This file is too large to import."));

    public static ImportFormatException TooManyRows(ImportLimitSet l) => new(AppLocale.Pick(
        $"El archivo tiene demasiadas filas (máximo {l.MaxDataRows:N0}).", $"This file has too many rows (maximum {l.MaxDataRows:N0})."));

    public static ImportFormatException TooManyColumns(ImportLimitSet l) => new(AppLocale.Pick(
        $"El archivo tiene demasiadas columnas (máximo {l.MaxColumns}).", $"This file has too many columns (maximum {l.MaxColumns})."));

    /// <summary>Copia el stream a memoria; falla si pasa de <paramref name="max"/> bytes.</summary>
    public static MemoryStream ReadLimited(Stream source, long max)
    {
        if (source.CanSeek && source.Length - source.Position > max) throw TooBig();
        var ms = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int n;
        while ((n = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += n;
            if (total > max) throw TooBig();
            ms.Write(buffer, 0, n);
        }
        ms.Position = 0;
        return ms;
    }
}

/// <summary>Stream de solo lectura que lanza al pasar de un total de bytes leídos (no confía en el tamaño declarado).</summary>
internal sealed class LimitedReadStream(Stream inner, long max) : Stream
{
    private long _total;

    public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));
    public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

    private int Count(int n)
    {
        _total += n;
        if (_total > max) throw ImportIO.TooBig();
        return n;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
