namespace PiRadar.Core.Domain;

/// <summary>Datos que no cumplen una regla del dominio. El mensaje está en español y se puede mostrar al usuario.</summary>
public sealed class DomainValidationException(string message) : Exception(message);
