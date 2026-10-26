# Rango de fechas en la búsqueda de movimientos — plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Al buscar en Movimientos (texto o categoría), filtrar además por Desde/Hasta (solo día, por defecto del 1 del mes a hoy), con un interruptor "Todo el historial" que deshabilita las fechas.

**Architecture:** Un tipo pequeño e inmutable de Core (`SearchDateRange`) tiene la lógica del rango (sugerencia, fechas al revés, límites para `TransactionFilter`) y se prueba en Core.Tests. `TransactionsViewState` guarda el rango y el interruptor; `Transactions.razor` muestra dos `MudDatePicker` y un `MudSwitch` y pasa el rango a la consulta de la BD. La ayuda se actualiza en `HelpContent`.

**Tech Stack:** .NET 10, MAUI Blazor Hybrid, MudBlazor 9, xunit.v3.

**Spec:** `docs/superpowers/specs/2026-10-26-search-date-range-design.md`

## Global Constraints

- Todo texto visible con `Pick("español", "inglés")`; nunca solo en español. Sin marcas ni nombres de plataformas o tiendas en la ayuda (`HelpContentTests`).
- Las fechas solo aplican mientras `IsSearching` (texto o categoría). Sin búsqueda, la lista sigue el selector de periodo como hoy.
- Rango inclusivo por día: `From` = Desde 00:00; `To` = (Hasta + 1 día) 00:00, exclusivo, como `TransactionFilter`.
- Desde > Hasta ⇒ Hasta = Desde; Hasta < Desde ⇒ Desde = Hasta. Sin mensajes de error.
- Al abrir y al cerrar la lupa: rango = mes en curso hasta hoy, interruptor apagado. Al editar un movimiento y volver se conserva todo.
- Sin cambios en la BD, migraciones, respaldo ni Análisis. Se mantiene el tope `MaxSearchRows = 300`.
- Commit y push a `main` al terminar cada tarea, terminando el mensaje con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. Un movimiento a las 23:59 del día Hasta debe aparecer; uno a las 00:00 del día siguiente no (Tarea 1, prueba `Bounds_IncludeTheWholeLastDay`).
2. Abrir la lupa el día 1 del mes: Desde = Hasta = hoy, no un rango vacío o del mes anterior (Tarea 1, `MonthToDate_OnTheFirst_IsASingleDay`).
3. Un rango que cruza el año (20 dic → 10 ene) debe filtrar y el contador debe mostrar los años (Tarea 1, `Bounds_AcrossYears`; el contador usa `DateLabels.Short`, que incluye el año).
4. Encender "Todo el historial", cambiar el filtro Gastos/Ingresos y apagarlo: las fechas elegidas siguen ahí y vuelven a aplicarse (Tarea 2, verificación en el simulador).
5. La app abierta de un mes a otro (lupa abierta el 31, se reabre el 1): al reabrir debe sugerir el mes nuevo (Tarea 2: reinicio al abrir, no solo al cerrar).

---

### Task 1: `SearchDateRange` en Core

**Files:**
- Create: `src/PiRadar.Core/Analytics/SearchDateRange.cs`
- Test: `tests/PiRadar.Core.Tests/Analytics/SearchDateRangeTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record SearchDateRange(DateOnly From, DateOnly To)` (namespace `PiRadar.Core.Analytics`)
  - `static SearchDateRange MonthToDate(DateOnly today)` → `(1 del mes de today, today)`
  - `SearchDateRange WithFrom(DateOnly from)` → si `from > To`, `(from, from)`; si no, `(from, To)`
  - `SearchDateRange WithTo(DateOnly to)` → si `to < From`, `(to, to)`; si no, `(From, to)`
  - `DateTime Start` → `From` a las 00:00; `DateTime EndExclusive` → `To + 1 día` a las 00:00 (ambos `DateTimeKind.Unspecified`, hora local como `OccurredAt`)

- [ ] **Step 1: Escribir las pruebas que fallan**

```csharp
public class SearchDateRangeTests
{
    [Fact]
    public void MonthToDate_StartsOnTheFirst()
    {
        var r = SearchDateRange.MonthToDate(new DateOnly(2026, 10, 26));
        Assert.Equal(new DateOnly(2026, 10, 1), r.From);
        Assert.Equal(new DateOnly(2026, 10, 26), r.To);
    }

    [Fact]
    public void MonthToDate_OnTheFirst_IsASingleDay()
    {
        var r = SearchDateRange.MonthToDate(new DateOnly(2026, 11, 1));
        Assert.Equal(new DateOnly(2026, 11, 1), r.From);
        Assert.Equal(new DateOnly(2026, 11, 1), r.To);
    }

    [Fact]
    public void WithFrom_AfterTo_MovesToToTheSameDay()
    {
        var r = new SearchDateRange(new(2026, 10, 1), new(2026, 10, 10)).WithFrom(new(2026, 10, 15));
        Assert.Equal(new SearchDateRange(new(2026, 10, 15), new(2026, 10, 15)), r);
    }

    [Fact]
    public void WithTo_BeforeFrom_MovesFromToTheSameDay()
    {
        var r = new SearchDateRange(new(2026, 10, 10), new(2026, 10, 20)).WithTo(new(2026, 10, 5));
        Assert.Equal(new SearchDateRange(new(2026, 10, 5), new(2026, 10, 5)), r);
    }

    [Fact]
    public void WithFromAndWithTo_InOrder_KeepTheOtherEnd()
    {
        var r = new SearchDateRange(new(2026, 10, 1), new(2026, 10, 20));
        Assert.Equal(new SearchDateRange(new(2026, 9, 15), new(2026, 10, 20)), r.WithFrom(new(2026, 9, 15)));
        Assert.Equal(new SearchDateRange(new(2026, 10, 1), new(2026, 10, 5)), r.WithTo(new(2026, 10, 5)));
    }

    [Fact]
    public void Bounds_IncludeTheWholeLastDay()
    {
        var r = new SearchDateRange(new(2026, 3, 15), new(2026, 4, 10));
        Assert.Equal(new DateTime(2026, 3, 15, 0, 0, 0), r.Start);
        Assert.Equal(new DateTime(2026, 4, 11, 0, 0, 0), r.EndExclusive);
        Assert.True(new DateTime(2026, 4, 10, 23, 59, 0) < r.EndExclusive);
    }

    [Fact]
    public void Bounds_AcrossYears()
    {
        var r = new SearchDateRange(new(2025, 12, 20), new(2026, 1, 10));
        Assert.Equal(new DateTime(2025, 12, 20), r.Start);
        Assert.Equal(new DateTime(2026, 1, 11), r.EndExclusive);
    }
}
```

- [ ] **Step 2: Correr y ver que fallan**

Run: `dotnet test --project tests/PiRadar.Core.Tests`
Expected: error de compilación, `SearchDateRange` no existe.

- [ ] **Step 3: Implementar `SearchDateRange`** con las firmas del bloque Interfaces y un `<summary>` en español como los demás tipos de `Analytics`.

- [ ] **Step 4: Correr y ver que pasan**

Run: `dotnet test --project tests/PiRadar.Core.Tests`
Expected: todas en verde (379 + 7).

- [ ] **Step 5: Commit y push**

```bash
git add src/PiRadar.Core/Analytics/SearchDateRange.cs tests/PiRadar.Core.Tests/Analytics/SearchDateRangeTests.cs
git commit -m "Search date range: month-to-date default, reversed dates snap to one day, inclusive day bounds"
git push origin main
```

### Task 2: Fechas e interruptor en la pantalla Movimientos

**Files:**
- Modify: `src/PiRadar.App/Services/TransactionsViewState.cs`
- Modify: `src/PiRadar.App/Components/Pages/Transactions.razor` (panel de búsqueda, contador, `SearchAsync`, `ToggleSearchAsync`)

**Interfaces:**
- Consumes: `SearchDateRange` (Tarea 1).
- Produces en `TransactionsViewState`:
  - `SearchDateRange SearchRange { get; set; }` = `SearchDateRange.MonthToDate(DateOnly.FromDateTime(DateTime.Today))`
  - `bool SearchWholeHistory { get; set; }` (falso por defecto)
  - `IsSearching` no cambia.

- [ ] **Step 1: `TransactionsViewState`**: agregar las dos propiedades; actualizar el `<summary>` de `IsSearching` ("ignora el periodo y usa el rango de la búsqueda o todo el historial").

- [ ] **Step 2: `ToggleSearchAsync`**: al abrir **y** al cerrar la lupa, `SearchRange = MonthToDate(hoy)` y `SearchWholeHistory = false` (Review Focus 5); al cerrar, además, borra texto y categoría como hoy.

- [ ] **Step 3: Panel de búsqueda**, debajo de `CategoryFilterPicker`:
  - Fila `d-flex gap-3` con dos `MudDatePicker` (`Date` = `SearchRange.From/To` como `DateTime`; `DateChanged` → `SetSearchFromAsync(DateTime?)` / `SetSearchToAsync(DateTime?)`), etiquetas `Pick("Desde", "From")` y `Pick("Hasta", "To")`, mismos parámetros que en `TransactionEdit.razor:90` (`Variant.Outlined`, `DateFormat` según `IsEnglish`, `PickerVariant.Dialog`, `AutoClose`, `ShowToolbar="false"`), `Margin="Margin.Dense"`, `Disabled="State.SearchWholeHistory"`. Cada uno envuelto en un `div` con id `search-from` / `search-to` (selectores para `UiScript`).
  - `MudSwitch T="bool"` con id `search-whole-history`, `Value="State.SearchWholeHistory"`, `ValueChanged` → `SetSearchWholeHistoryAsync(bool)`, `Label="@Pick("Todo el historial", "Whole history")"`, `Color.Primary`.
  - Los `Set…Async` aplican `WithFrom`/`WithTo` (ignoran `null`), guardan en `State` y llaman a `LoadAsync()`.

- [ ] **Step 4: `SearchAsync`**: el filtro pasa a ser `new TransactionFilter { Type = State.Type, From = State.SearchWholeHistory ? null : State.SearchRange.Start, To = State.SearchWholeHistory ? null : State.SearchRange.EndExclusive }`; actualizar su `<summary>`.

- [ ] **Step 5: Contador (`#search-count`)**: con el interruptor encendido, los textos actuales ("… en todo el historial"). Si no, con `DateLabels.Short` de cada punta:
  - 1: `Pick($"1 resultado del {desde} al {hasta}", $"1 result from {desde} to {hasta}")`
  - N: `Pick($"{n} resultados del {desde} al {hasta}", $"{n} results from {desde} to {hasta}")`
  - Si Desde = Hasta: `Pick($"… el {desde}", $"… on {desde}")` con el mismo singular/plural.

- [ ] **Step 6: Compilar y probar**

Run: `dotnet build src/PiRadar.App -f net10.0-ios26.0 && dotnet test`
Expected: compila sin errores nuevos; todas las pruebas en verde.

- [ ] **Step 7: Verificar en el simulador iOS 18.1** con datos de demostración y `UiScript` (ver CLAUDE.md):
  - `go /transactions`, `click #search-button`: se ven Desde = 1 del mes, Hasta = hoy; la lista sigue el periodo (sin texto). Captura.
  - `fill Buscar movimientos=<palabra de un movimiento de otro mes>`: 0 resultados con el rango del mes; `click #search-whole-history input`: aparece; las fechas quedan grises. Captura.
  - Apagar el interruptor: vuelve el rango anterior. Cambiar Desde a mano en el calendario (Review Focus 4) y ver que el contador cambia.
  - Comprobar que los textos salen en inglés con `theme`/idioma inglés.

- [ ] **Step 8: Commit y push**

```bash
git add src/PiRadar.App/Services/TransactionsViewState.cs src/PiRadar.App/Components/Pages/Transactions.razor
git commit -m "Transactions search: From/To dates (month to date by default) and a whole-history switch"
git push origin main
```

### Task 3: Ayuda en español e inglés

**Files:**
- Modify: `src/PiRadar.Core/Help/HelpContent.cs` (artículo `filtrar-movimientos`)
- Regenerate: `store/help/ayuda.html`, `store/help/help.html`

- [ ] **Step 1: Cambiar el paso de la búsqueda** ("Busca en todo tu historial…") y agregar uno sobre las fechas:
  - es: "Para saber si ya registraste un gasto, toca la lupa de **Movimientos** y escribe una palabra del comercio, la descripción o las notas, o un monto. No importan las mayúsculas ni las tildes."
  - en: "To check whether you already recorded an expense, tap the magnifier in **Transactions** and type a word from the merchant, description or notes, or an amount. Capitals and accents don't matter."
  - es: "La búsqueda mira del día 1 del mes hasta hoy. Cambia **Desde** y **Hasta** para elegir otras fechas, o activa **Todo el historial** para buscar en todos tus movimientos."
  - en: "The search covers the 1st of the month through today. Change **From** and **To** to pick other dates, or turn on **Whole history** to search all your transactions."

- [ ] **Step 2: Probar y regenerar**

Run: `dotnet test --project tests/PiRadar.Core.Tests && dotnet run tools/help/build-help.cs`
Expected: pruebas en verde (incluye `HelpContentTests`); `store/help/*.html` actualizados con el texto nuevo.

- [ ] **Step 3: Commit y push**

```bash
git add src/PiRadar.Core/Help/HelpContent.cs store/help/ayuda.html store/help/help.html
git commit -m "Help: date range and whole history in the transaction search (es + en)"
git push origin main
```

Avisar al usuario que suba `store/help/ayuda.html` y `help.html` a la web.
