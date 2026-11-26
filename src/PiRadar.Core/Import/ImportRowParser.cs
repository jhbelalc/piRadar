using System.Globalization;
using System.Text.RegularExpressions;
using PiRadar.Core.Domain;
using PiRadar.Core.Receipts;

namespace PiRadar.Core.Import;

/// <summary>Convierte las filas de una hoja en movimientos propuestos. Nunca usa double para dinero.</summary>
public static partial class ImportRowParser
{
    private const decimal MaxAmount = 10_000_000_000_000m;

    private static readonly string[] OpeningPhrases =
    [
        "SALDO ANTERIOR", "SALDO INICIAL", "SALDO MES ANTERIOR", "MES ANTERIOR", "MES ANT",
        "OPENING BALANCE", "PREVIOUS BALANCE", "BALANCE FORWARD", "STARTING BALANCE",
    ];

    private static readonly string[] TotalWords = ["TOTAL", "TOTALES", "SUBTOTAL", "SUMA", "SUM", "SALDO FINAL", "BALANCE"];

    private static readonly HashSet<string> IncomeTypes = ["INGRESO", "INGRESOS", "ENTRADA", "ABONO", "CREDITO", "INCOME", "CREDIT", "DEPOSIT", "IN"];
    private static readonly HashSet<string> ExpenseTypes = ["GASTO", "GASTOS", "EGRESO", "SALIDA", "CARGO", "DEBITO", "EXPENSE", "DEBIT", "WITHDRAWAL", "OUT"];

    private static readonly Dictionary<string, int> Months = BuildMonths();

    private static Dictionary<string, int> BuildMonths()
    {
        string[][] names =
        [
            ["ENE", "ENERO", "JAN", "JANUARY"], ["FEB", "FEBRERO", "FEBRUARY"], ["MAR", "MARZO", "MARCH"],
            ["ABR", "ABRIL", "APR", "APRIL"], ["MAY", "MAYO"], ["JUN", "JUNIO", "JUNE"], ["JUL", "JULIO", "JULY"],
            ["AGO", "AGOSTO", "AUG", "AUGUST"], ["SEP", "SEPT", "SEPTIEMBRE", "SETIEMBRE", "SEPTEMBER"],
            ["OCT", "OCTUBRE", "OCTOBER"], ["NOV", "NOVIEMBRE", "NOVEMBER"], ["DIC", "DICIEMBRE", "DEC", "DECEMBER"],
        ];
        var d = new Dictionary<string, int>();
        for (var i = 0; i < names.Length; i++)
            foreach (var n in names[i]) d[n] = i + 1;
        return d;
    }

    private static readonly string[] DayFirstFormats = ["d/M/yyyy", "d-M-yyyy", "d.M.yyyy", "d/M/yy", "d-M-yy"];
    private static readonly string[] MonthFirstFormats = ["M/d/yyyy", "M-d-yyyy", "M/d/yy"];
    private static readonly string[] AlwaysFormats = ["yyyy-MM-dd", "yyyy/MM/dd"];

    [GeneratedRegex(@"(?<![A-Za-z])([ap])\.?\s?m\.?(?![A-Za-z])", RegexOptions.IgnoreCase)]
    private static partial Regex AmPmRegex();

    [GeneratedRegex(@"(?<=[A-Za-z])\.")]
    private static partial Regex AbbrevDotRegex();

    [GeneratedRegex(@"(?:T|\s+)(\d{1,2}):(\d{2})(?::(\d{2}))?(?:\.\d+)?\s*(AM|PM)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"^(\d{1,2})[\s\-/.]*(?:DE\s+)?([A-Z]+)[\s\-/.,]*(?:DE\s+)?(\d{4}|\d{2})$")]
    private static partial Regex DayMonthNameRegex();

    [GeneratedRegex(@"^([A-Z]+)\s*(\d{1,2}),?\s*(\d{4})$")]
    private static partial Regex MonthNameDayRegex();

    // ---- Orden de fecha ----

    /// <summary>Orden que deja más fechas de texto válidas; en empate, el del idioma.</summary>
    public static DateOrder SuggestDateOrder(SheetData sheet, ImportMapping mapping)
    {
        var fallback = AppLocale.IsEnglish ? DateOrder.MonthFirst : DateOrder.DayFirst;
        if (mapping.ColumnOf(ImportField.Date) is not { } col) return fallback;
        int day = 0, month = 0;
        for (var r = (mapping.HeaderRow ?? -1) + 1; r < sheet.Rows.Count; r++)
        {
            var cell = sheet.Rows[r][col];
            if (cell.Kind != CellKind.Text || string.IsNullOrWhiteSpace(cell.Text)) continue;
            if (TryParseDateText(cell.Text, DateOrder.DayFirst, out var d1, out _) && YearOk(d1)) day++;
            if (TryParseDateText(cell.Text, DateOrder.MonthFirst, out var d2, out _) && YearOk(d2)) month++;
        }
        if (day == month) return fallback;
        return day > month ? DateOrder.DayFirst : DateOrder.MonthFirst;
    }

    // ---- Parse ----

    public static ImportPreview Parse(SheetData sheet, ImportOptions options)
    {
        var map = options.Mapping;
        if (!map.IsComplete) throw new ArgumentException("The mapping needs a date and an amount.", nameof(options));

        var colDate = map.ColumnOf(ImportField.Date)!.Value;
        var colTime = map.ColumnOf(ImportField.Time);
        var colAmount = map.ColumnOf(ImportField.Amount);
        var colIncome = map.ColumnOf(ImportField.Income);
        var colExpense = map.ColumnOf(ImportField.Expense);
        var colType = map.ColumnOf(ImportField.Type);
        var colCurrency = map.ColumnOf(ImportField.Currency);
        var mode = map.AmountMode;

        var rows = new List<ImportRow>();
        var errors = new List<ImportIssue>();
        var rounded = new List<int>();
        var opening = new List<int>();
        int skippedEmpty = 0, skippedTotals = 0, skippedZero = 0;

        for (var r = (map.HeaderRow ?? -1) + 1; r < sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r];
            var number = r + 1;

            bool Fail(string es, string en)
            {
                errors.Add(new ImportIssue(number, AppLocale.Pick($"Fila {number}: {es}", $"Row {number}: {en}")));
                return false;
            }

            if (IsEmptyRow(row, map)) { skippedEmpty++; continue; }

            // Fecha.
            var dateOk = TryReadDate(row[colDate], options.DateOrder, out var date, out var dateHasTime);
            if (!dateOk)
            {
                if (LooksLikeTotalRow(row)) skippedTotals++;
                else Fail("la fecha no es válida.", "the date is not valid.");
                continue;
            }
            var time = dateHasTime ? date.TimeOfDay : TimeSpan.FromHours(12);
            if (colTime is { } tc && TryReadTime(row[tc], out var t)) time = t;
            var occurredAt = DateTime.SpecifyKind(date.Date + time, DateTimeKind.Local);

            // Moneda y monto.
            Currency? detected = null;
            TransactionType type;
            decimal amount;

            if (mode == AmountMode.IncomeExpense)
            {
                if (!ReadOptionalAmount(colIncome is { } ci ? row[ci] : default, out var inc, out var incCur, out var incBad)
                    || !ReadOptionalAmount(colExpense is { } ce ? row[ce] : default, out var exp, out var expCur, out var expBad))
                {
                    Fail("el monto no es válido.", "the amount is not valid.");
                    continue;
                }
                _ = incBad; _ = expBad;
                var hasInc = inc is { } iv && iv != 0;
                var hasExp = exp is { } ev && ev != 0;
                if (hasInc && hasExp) { Fail("tiene ingreso y gasto a la vez.", "has both income and expense."); continue; }
                if (!hasInc && !hasExp)
                {
                    if (inc is null && exp is null) Fail("no tiene monto.", "has no amount.");
                    else skippedZero++;
                    continue;
                }
                type = hasInc ? TransactionType.Income : TransactionType.Expense;
                amount = Math.Abs(hasInc ? inc!.Value : exp!.Value);
                detected = hasInc ? incCur : expCur;
            }
            else
            {
                if (!ReadOptionalAmount(row[colAmount!.Value], out var value, out detected, out _))
                {
                    Fail("el monto no es válido.", "the amount is not valid.");
                    continue;
                }
                if (value is null) { Fail("no tiene monto.", "has no amount."); continue; }
                if (value == 0) { skippedZero++; continue; }
                amount = Math.Abs(value.Value);

                if (mode == AmountMode.Signed)
                {
                    var negative = value < 0;
                    type = negative == options.NegativeIsExpense ? TransactionType.Expense : TransactionType.Income;
                }
                else
                {
                    var typeText = row[colType!.Value].ToDisplayString().Trim();
                    var known = ReadType(typeText);
                    if (known is { } k) type = k;
                    else if (value < 0) type = TransactionType.Expense;
                    else
                    {
                        if (typeText.Length == 0) Fail("no tiene tipo (ingreso o gasto).", "has no type (income or expense).");
                        else Fail($"el tipo \"{typeText}\" no es ingreso ni gasto.", $"the type \"{typeText}\" is neither income nor expense.");
                        continue;
                    }
                }
            }

            Currency currency;
            if (colCurrency is { } cc && !IsBlank(row[cc]))
            {
                var raw = row[cc].ToDisplayString();
                if (ReadCurrency(raw) is { } parsed) currency = parsed;
                else
                {
                    var shown = ImportText.Clean(raw) ?? raw;
                    Fail($"la moneda {shown} no es COP ni USD.", $"the currency {shown} is neither COP nor USD.");
                    continue;
                }
            }
            else currency = detected ?? options.DefaultCurrency;

            if (amount > MaxAmount) { Fail("el monto es demasiado grande.", "the amount is too large."); continue; }
            var scaled = decimal.Round(amount, currency.Scale(), MidpointRounding.AwayFromZero);
            if (scaled == 0) { skippedZero++; continue; }
            var wasRounded = scaled != amount;
            Money money;
            try { money = Money.FromDecimal(scaled, currency); }
            catch (OverflowException) { Fail("el monto es demasiado grande.", "the amount is too large."); continue; }

            string? Text(ImportField f) => map.ColumnOf(f) is { } c ? ImportText.CleanCell(row[c]) : null;
            var category = Text(ImportField.Category);
            var sub = Text(ImportField.Subcategory);
            var payment = Text(ImportField.PaymentMethod);
            var description = Text(ImportField.Description);
            var merchant = Text(ImportField.Merchant);
            var notes = Text(ImportField.Notes);

            var isOpening = new[] { description, merchant, category }
                .Any(s => OpeningPhrases.Any(p => ImportText.ContainsWords(ImportText.Key(s), p)));

            rows.Add(new ImportRow(number, type, money, occurredAt, category, sub, payment, description, merchant, notes,
                wasRounded, isOpening));
            if (wasRounded) rounded.Add(number);
            if (isOpening) opening.Add(number);
        }

        return new ImportPreview
        {
            Rows = rows,
            Errors = errors,
            RoundedRows = rounded,
            OpeningBalanceRows = opening,
            SkippedEmpty = skippedEmpty,
            SkippedTotals = skippedTotals,
            SkippedZero = skippedZero,
            Totals = Summarize(rows),
            Categories = DistinctCategories(rows),
            Subcategories = DistinctSubcategories(rows),
            PaymentMethods = DistinctPaymentMethods(rows),
        };
    }

    // ---- Resúmenes ----

    private static List<CurrencySummary> Summarize(List<ImportRow> rows) =>
        rows.GroupBy(r => r.Amount.Currency).OrderBy(g => g.Key).Select(g =>
        {
            var c = g.Key;
            var income = g.Where(r => r.Type == TransactionType.Income).Sum(r => r.Amount.AmountMinor);
            var expense = g.Where(r => r.Type == TransactionType.Expense).Sum(r => r.Amount.AmountMinor);
            return new CurrencySummary(c, g.Count(), new Money(income, c), new Money(expense, c),
                g.Min(r => r.OccurredAt), g.Max(r => r.OccurredAt));
        }).ToList();

    private static List<ImportName> DistinctCategories(List<ImportRow> rows)
    {
        var order = new List<(string Key, TransactionType Type)>();
        var map = new Dictionary<(string, TransactionType), (string Name, int Count)>();
        foreach (var r in rows)
        {
            if (r.Category is null) continue;
            var k = (ImportText.Key(r.Category), r.Type);
            if (map.TryGetValue(k, out var v)) map[k] = (v.Name, v.Count + 1);
            else { map[k] = (r.Category, 1); order.Add(k); }
        }
        return order.Select(k => new ImportName(map[k].Name, k.Type, map[k].Count)).ToList();
    }

    private static List<ImportSubName> DistinctSubcategories(List<ImportRow> rows)
    {
        var order = new List<(string, string, TransactionType)>();
        var map = new Dictionary<(string, string, TransactionType), (string Parent, string Name, int Count)>();
        foreach (var r in rows)
        {
            if (r.Category is null || r.Subcategory is null) continue;
            var k = (ImportText.Key(r.Category), ImportText.Key(r.Subcategory), r.Type);
            if (map.TryGetValue(k, out var v)) map[k] = (v.Parent, v.Name, v.Count + 1);
            else { map[k] = (r.Category, r.Subcategory, 1); order.Add(k); }
        }
        return order.Select(k => new ImportSubName(map[k].Parent, map[k].Name, k.Item3, map[k].Count)).ToList();
    }

    private static List<ImportName> DistinctPaymentMethods(List<ImportRow> rows)
    {
        var order = new List<string>();
        var map = new Dictionary<string, (string Name, TransactionType Type, int Count)>();
        foreach (var r in rows)
        {
            if (r.PaymentMethod is null) continue;
            var k = ImportText.Key(r.PaymentMethod);
            if (map.TryGetValue(k, out var v)) map[k] = (v.Name, v.Type, v.Count + 1);
            else { map[k] = (r.PaymentMethod, r.Type, 1); order.Add(k); }
        }
        return order.Select(k => new ImportName(map[k].Name, map[k].Type, map[k].Count)).ToList();
    }

    // ---- Filas ----

    private static bool IsBlank(CellValue c) => c.IsEmpty || (c.Kind == CellKind.Text && string.IsNullOrWhiteSpace(c.Text));

    private static bool IsEmptyRow(IReadOnlyList<CellValue> row, ImportMapping map)
    {
        for (var i = 0; i < row.Count && i < map.Columns.Count; i++)
            if (map.Columns[i] != ImportField.Ignore && !IsBlank(row[i])) return false;
        return true;
    }

    private static bool LooksLikeTotalRow(IReadOnlyList<CellValue> row)
    {
        foreach (var c in row)
        {
            if (c.Kind != CellKind.Text) continue;
            var key = ImportText.Key(c.Text);
            if (TotalWords.Any(w => key == w || key.StartsWith(w + " ", StringComparison.Ordinal))) return true;
        }
        return false;
    }

    // ---- Fechas ----

    private static bool YearOk(DateTime d) => d.Year >= 1900 && d <= DateTime.Today.AddYears(1);

    private static bool TryReadDate(CellValue cell, DateOrder order, out DateTime date, out bool hasTime)
    {
        date = default;
        hasTime = false;
        switch (cell.Kind)
        {
            case CellKind.Date:
                date = cell.Date!.Value;
                hasTime = date.TimeOfDay != TimeSpan.Zero;
                return YearOk(date);
            case CellKind.Number when cell.Number is >= 20000m and <= 80000m:
                if (XlsxReader.FromSerial(cell.Number.Value, false) is not { } d) return false;
                date = d;
                hasTime = d.TimeOfDay != TimeSpan.Zero;
                return YearOk(date);
            case CellKind.Text:
                return TryParseDateText(cell.Text!, order, out date, out hasTime) && YearOk(date);
            default:
                return false;
        }
    }

    private static bool TryReadTime(CellValue cell, out TimeSpan time)
    {
        time = default;
        switch (cell.Kind)
        {
            case CellKind.Date when cell.Date!.Value.TimeOfDay != TimeSpan.Zero:
                time = cell.Date.Value.TimeOfDay;
                return true;
            case CellKind.Number when cell.Number is > 0m and < 1m:
                time = TimeSpan.FromSeconds((double)Math.Round(cell.Number.Value * 86400m));
                return true;
            case CellKind.Text:
                var s = NormalizeAmPm(cell.Text!.Trim());
                var m = TimeRegex().Match(" " + s);
                return m.Success && TryBuildTime(m, out time);
            default:
                return false;
        }
    }

    private static string NormalizeAmPm(string s) =>
        AmPmRegex().Replace(s, m => m.Groups[1].Value.ToUpperInvariant() + "M");

    private static bool TryBuildTime(Match m, out TimeSpan time)
    {
        time = default;
        var h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var min = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var sec = m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
        if (m.Groups[4].Success)
        {
            if (h is < 1 or > 12) return false;
            var pm = m.Groups[4].Value.Equals("PM", StringComparison.OrdinalIgnoreCase);
            h = h % 12 + (pm ? 12 : 0);
        }
        if (h > 23 || min > 59 || sec > 59) return false;
        time = new TimeSpan(h, min, sec);
        return true;
    }

    /// <summary>Interpreta un texto de fecha (con hora opcional). <paramref name="date"/> trae la hora solo si <paramref name="hasTime"/>.</summary>
    internal static bool TryParseDateText(string text, DateOrder order, out DateTime date, out bool hasTime)
    {
        date = default;
        hasTime = false;
        var s = text.Trim();
        if (s.Length == 0 || s.Length > 60) return false;

        s = NormalizeAmPm(s);
        s = AbbrevDotRegex().Replace(s, "");

        var time = TimeSpan.Zero;
        var tm = TimeRegex().Match(s);
        if (tm.Success)
        {
            if (!TryBuildTime(tm, out time)) return false;
            hasTime = true;
            s = s[..tm.Index].Trim();
        }

        if (TryParseDay(s, order, out var day))
        {
            date = day + time;
            return true;
        }
        hasTime = false;
        return false;
    }

    private static bool TryParseDay(string s, DateOrder order, out DateTime day)
    {
        var formats = (order == DateOrder.DayFirst ? DayFirstFormats : MonthFirstFormats).Concat(AlwaysFormats).ToArray();
        if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return true;

        var folded = TextNormalizer.Fold(s);
        var m = DayMonthNameRegex().Match(folded);
        if (m.Success && Months.TryGetValue(m.Groups[2].Value, out var mo))
            return TryBuild(m.Groups[3].Value, mo, m.Groups[1].Value, out day);
        m = MonthNameDayRegex().Match(folded);
        if (m.Success && Months.TryGetValue(m.Groups[1].Value, out mo))
            return TryBuild(m.Groups[3].Value, mo, m.Groups[2].Value, out day);
        day = default;
        return false;
    }

    private static bool TryBuild(string year, int month, string dayText, out DateTime day)
    {
        day = default;
        var y = int.Parse(year, CultureInfo.InvariantCulture);
        if (year.Length == 2) y += y <= 49 ? 2000 : 1900;
        var d = int.Parse(dayText, CultureInfo.InvariantCulture);
        if (y < 1 || d < 1 || d > DateTime.DaysInMonth(y, month)) return false;
        day = new DateTime(y, month, d);
        return true;
    }

    // ---- Montos ----

    /// <summary>
    /// Interpreta un monto escrito: quita símbolos y espacios, paréntesis o guion = negativo, y detecta la moneda de
    /// <c>US$</c>/<c>USD</c>/<c>COP</c>.
    /// </summary>
    internal static bool ParseAmount(string text, out decimal value, out Currency? currency)
    {
        value = 0;
        currency = null;
        var s = text;
        if (Regex.IsMatch(s, "US\\$|USD", RegexOptions.IgnoreCase)) currency = Currency.USD;
        else if (Regex.IsMatch(s, "COP", RegexOptions.IgnoreCase)) currency = Currency.COP;

        s = Regex.Replace(s, "US\\$|USD|COP", "", RegexOptions.IgnoreCase);
        s = new string(s.Where(c => !char.IsWhiteSpace(c) && c is not ('$' or '€')).ToArray());
        s = s.Replace('−', '-');

        var negative = false;
        if (s.Length >= 2 && s[0] == '(' && s[^1] == ')') { negative = true; s = s[1..^1]; }
        if (s.StartsWith('-')) { negative = true; s = s[1..]; }
        else if (s.EndsWith('-')) { negative = true; s = s[..^1]; }
        if (s.StartsWith('+')) s = s[1..];

        if (!MoneyParser.TryParseDecimal(s, out var n)) return false;
        value = negative ? -n : n;
        return true;
    }

    /// <summary>Texto que, sin espacios ni símbolos de moneda, son solo guiones: un cero o vacío en formato contable.</summary>
    internal static bool IsDashOnly(string text)
    {
        var s = Regex.Replace(text, "US\\$|USD|COP", "", RegexOptions.IgnoreCase);
        s = new string(s.Where(c => !char.IsWhiteSpace(c) && c is not ('$' or '€')).ToArray());
        return s.Length > 0 && s.All(c => c is '-' or '–' or '—' or '−');
    }

    /// <summary>Vacío ⇒ ok con valor null; número o texto de monto ⇒ ok; otra cosa ⇒ false.</summary>
    private static bool ReadOptionalAmount(CellValue cell, out decimal? value, out Currency? currency, out bool bad)
    {
        value = null;
        currency = null;
        bad = false;
        if (IsBlank(cell) || (cell.Kind == CellKind.Text && IsDashOnly(cell.Text!))) return true;
        switch (cell.Kind)
        {
            case CellKind.Number:
                value = cell.Number;
                return true;
            case CellKind.Text when ParseAmount(cell.Text!, out var v, out currency):
                value = v;
                return true;
            default:
                bad = true;
                return false;
        }
    }

    private static Currency? ReadCurrency(string raw)
    {
        if (raw.Trim() == "$") return Currency.COP;
        return ImportText.Key(raw) switch
        {
            "COP" or "PESOS" or "PESO" or "PESOS COLOMBIANOS" => Currency.COP,
            "USD" or "US" or "DOLARES" or "DOLAR" or "DOLLAR" or "DOLLARS" => Currency.USD,
            _ => null,
        };
    }

    private static TransactionType? ReadType(string text)
    {
        if (text == "+") return TransactionType.Income;
        if (text == "-") return TransactionType.Expense;
        var key = ImportText.Key(text);
        if (IncomeTypes.Contains(key)) return TransactionType.Income;
        if (ExpenseTypes.Contains(key)) return TransactionType.Expense;
        return null;
    }
}
