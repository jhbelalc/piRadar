# Búsqueda de movimientos: rango de fechas

Fecha: 2026-10-26. Tamaño: cambio pequeño (diseño corto).

## Intención
La búsqueda de **Movimientos** (lupa) filtra por texto y categoría, pero siempre recorre todo el historial. El usuario
necesita acotar los resultados a fechas sueltas (p. ej. 15 mar – 10 abr), por día, sin horas.

## Qué se construye (decidido con el usuario)
- En el panel de búsqueda, además del texto y la categoría: **Desde** y **Hasta** (solo día).
- Al abrir la búsqueda se sugiere el mes en curso: Desde = día 1 del mes actual, Hasta = hoy.
- Tocar una fecha abre el calendario normal (`MudDatePicker` en diálogo, el mismo de `TransactionEdit`, con el formato
  de fecha según el idioma).
- Interruptor **"Todo el historial"** / **"Whole history"**: encendido, Desde y Hasta quedan deshabilitados (conservan
  sus valores) y se busca en todo el historial; apagado, vuelven a habilitarse y se aplica el rango.
- Totales (Gastos, Ingresos, Balance) sobre lo que cumple texto + categoría + fechas.

## Enfoques considerados
- **A. Dos campos Desde/Hasta + interruptor (elegido)**: mismo calendario que ya funciona en ambas plataformas; cada
  punta se cambia por separado; cumple tal cual lo de deshabilitar las fechas.
- B. Un solo `MudDateRangePicker`: más compacto, pero para mover una punta hay que elegir las dos y cruzar meses a mitad
  de la selección es torpe en el teléfono; nada parecido en la app.
- C. `<input type="date">` nativo: se ve distinto en iPhone y Android y su idioma/formato sigue al teléfono, no a
  Ajustes > Idioma.

## Decisiones mías (revisar)
- **Con la búsqueda abierta siempre se aplican sus filtros**, aunque no haya texto ni categoría: abrir la lupa muestra
  el mes en curso hasta hoy (antes, sin texto se veía el selector de periodo). El selector de periodo se oculta mientras
  la búsqueda está abierta, como hoy al buscar.
- Rango inclusivo por día: `From` = Desde a las 00:00, `To` = día siguiente a Hasta a las 00:00 (exclusivo), en
  `TransactionFilter`, o sea en la consulta a la BD; texto y categoría siguen evaluándose en memoria.
- Fechas al revés: si se elige un Desde posterior a Hasta, Hasta pasa a ser ese mismo día (y al revés), sin error.
- Las fechas y el interruptor se guardan en `TransactionsViewState` (se conservan al editar un movimiento y volver);
  al cerrar la lupa vuelven al mes en curso y el interruptor se apaga.
- Contador: "N resultados del 1 oct. al 26 oct." / "N results from Oct 1 to Oct 26" (formato de `DateLabels`), y
  "en todo el historial" con el interruptor encendido. Se mantiene el tope de 300 filas.
- Lógica del rango (sugerencia por defecto, normalización, conversión a filtro) en un tipo pequeño de Core con pruebas;
  la página solo lo usa.
- Ayuda: actualizar el artículo `filtrar-movimientos` de `HelpContent` (es + en) y regenerar `store/help/*.html`.

## Fuera de alcance
- Atajos de rango (semana pasada, año, etc.), horas, filtros por medio de pago o monto.
- Cambios en la BD, el respaldo o la pantalla de Análisis.

## Verificación
- `dotnet test` (pruebas nuevas del rango en Core).
- Simulador iOS 18.1 con `UiScript`: abrir la lupa, ver fechas sugeridas, cambiar Desde, encender/apagar
  "Todo el historial", captura. Android lo confirma el usuario en el teléfono.
