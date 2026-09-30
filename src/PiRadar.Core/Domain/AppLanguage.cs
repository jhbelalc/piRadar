using System.Globalization;

namespace PiRadar.Core.Domain;

/// <summary>Idiomas de la interfaz. Se guardan en <c>AppSetting</c> como <c>es</c> / <c>en</c>.</summary>
public enum AppLanguage
{
    Spanish = 0,
    English = 1,
}

/// <summary>
/// Idioma vigente de la app. Un solo usuario por dispositivo, así que basta un valor global; las pruebas lo
/// cambian solo dentro de <see cref="Use"/>, que es local al flujo asíncrono y no afecta a otras pruebas en paralelo.
/// </summary>
public static class AppLocale
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");
    private static readonly CultureInfo UnitedStates = CultureInfo.GetCultureInfo("en-US");
    private static readonly AsyncLocal<AppLanguage?> Override = new();
    private static AppLanguage global = AppLanguage.Spanish;

    public static AppLanguage Language
    {
        get => Override.Value ?? global;
        set => global = value;
    }

    public static bool IsEnglish => Language == AppLanguage.English;

    public static CultureInfo Culture => For(Language);

    public static CultureInfo For(AppLanguage language) => language == AppLanguage.English ? UnitedStates : Colombia;

    /// <summary>Primer día de la semana: lunes en español (ISO), domingo en inglés (EE. UU.).</summary>
    public static DayOfWeek FirstDayOfWeek => Language == AppLanguage.English ? DayOfWeek.Sunday : DayOfWeek.Monday;

    /// <summary>Elige el texto del idioma vigente. Todo texto visible de la app pasa por aquí.</summary>
    public static string Pick(string spanish, string english) => IsEnglish ? english : spanish;

    /// <summary>Idioma del dispositivo: inglés si el idioma de la interfaz es inglés; si no, español.</summary>
    public static AppLanguage FromDevice(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName == "en" ? AppLanguage.English : AppLanguage.Spanish;

    /// <summary>Código para guardar en ajustes ("es" / "en").</summary>
    public static string Code(AppLanguage language) => language == AppLanguage.English ? "en" : "es";

    /// <summary>Lee el ajuste guardado; <c>null</c> significa "seguir el idioma del dispositivo".</summary>
    public static AppLanguage? ParseCode(string? code) => code switch
    {
        "es" => AppLanguage.Spanish,
        "en" => AppLanguage.English,
        _ => null,
    };

    /// <summary>Cambia el idioma solo dentro del flujo actual (para pruebas). Al liberar vuelve al anterior.</summary>
    public static IDisposable Use(AppLanguage language)
    {
        var previous = Override.Value;
        Override.Value = language;
        return new Restore(previous);
    }

    private sealed class Restore(AppLanguage? previous) : IDisposable
    {
        public void Dispose() => Override.Value = previous;
    }
}
