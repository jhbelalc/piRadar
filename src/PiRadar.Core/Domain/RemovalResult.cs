namespace PiRadar.Core.Domain;

/// <summary>Qué pasó al eliminar una categoría o un medio de pago.</summary>
public enum RemovalResult
{
    /// <summary>No tenía movimientos ni dependientes: se borró.</summary>
    Deleted = 1,

    /// <summary>Tiene movimientos (o subcategorías): se archivó para no perder el historial.</summary>
    Archived = 2,
}
