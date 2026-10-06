using System.Globalization;
using System.Text.RegularExpressions;
using PiRadar.Core.Domain;

namespace PiRadar.Core.Receipts;

/// <summary>
/// Extrae total, fecha, comercio, NIT y moneda de un recibo a partir del OCR. No depende del motor de OCR ni
/// escribe logs (el texto y los montos del recibo son datos del usuario).
/// </summary>
public static partial class ReceiptParser
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Filas de cabecera en las que se busca el comercio.</summary>
    private const int MaxHeaderRows = 14;

    /// <param name="fallbackCurrency">
    /// Moneda cuando el recibo no trae NIT ni pistas de moneda (p. ej. USD si la app está en inglés).
    /// </param>
    public static ReceiptParseResult Parse(OcrPage page, DateOnly today, Currency fallbackCurrency = Currency.COP) =>
        Parse(RowBuilder.Build(page).Select(r => r.Text).ToList(), today, fallbackCurrency);

    /// <summary>Re-parsea desde filas de texto, p. ej. las guardadas en <c>Receipt.OcrText</c>.</summary>
    public static ReceiptParseResult Parse(
        IReadOnlyList<string> rows, DateOnly today, Currency fallbackCurrency = Currency.COP)
    {
        var lines = rows
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select((text, index) => new Line(index, text.Trim()))
            .ToList();

        var taxId = FindTaxId(lines);
        var currency = DetectCurrency(lines, hasTaxId: taxId is not null, fallbackCurrency);
        var totals = FindTotals(lines, currency);
        var date = FindDate(lines, today, preferMonthFirst: currency == Currency.USD);
        var merchants = FindMerchants(lines, taxId?.Line);

        return new ReceiptParseResult
        {
            Currency = currency,
            Total = totals.Candidates.Count > 0 ? totals.Candidates[0] : null,
            TotalConfirmed = totals.Confirmed,
            TotalCandidates = totals.Candidates,
            Date = date?.Value,
            HasTime = date?.HasTime ?? false,
            Merchant = merchants.Count > 0 ? merchants[0] : null,
            MerchantCandidates = merchants,
            TaxId = taxId?.Value,
            TaxIdVerified = taxId?.Verified ?? false,
            Rows = lines.Select(l => l.Text).ToList(),
        };
    }

    private sealed class Line(int index, string text)
    {
        private IReadOnlyList<AmountToken>? _amounts;

        public int Index { get; } = index;
        public string Text { get; } = text;
        public string Folded { get; } = TextNormalizer.Fold(text);
        public string Compact { get; } = TextNormalizer.Compact(text);
        public IReadOnlyList<AmountToken> Amounts => _amounts ??= AmountParser.Find(Text);

        public bool Has(params ReadOnlySpan<string> keywords) => TextNormalizer.ContainsAny(Compact, keywords);

        /// <summary>Palabras de recibo en inglés que restan del total (TAX, TIP, CHANGE...), como palabra completa.</summary>
        public bool HasEnglishExclusion => EnglishExclusion().IsMatch(Folded);

        public bool HasEnglishChange => EnglishChange().IsMatch(Folded);

        public bool HasEnglishTaxOrTip => EnglishTaxOrTip().IsMatch(Folded);

        /// <summary>Fila con montos y casi sin letras ("31.492", "$ 63.500").</summary>
        public bool IsAmountOnly
        {
            get
            {
                if (Amounts.Count == 0)
                {
                    return false;
                }

                var rest = Text;
                foreach (var a in Amounts.OrderByDescending(a => a.Start))
                {
                    rest = rest.Remove(a.Start, Math.Min(a.Length, rest.Length - a.Start));
                }

                return rest.Count(char.IsLetter) <= 2;
            }
        }
    }

    // ---------------------------------------------------------------- Total

    private static readonly string[] TotalExclusions =
    [
        "SUBTOTAL", "SUBTOT", "SUBT", "TOTALIVA", "IMPTO", "IMPUESTO", "IMPC", "TOTALSIN", "ITEM", "LINEA",
        "AHORRO", "DESCUENTO", "DSCTO", "ARTICULO", "PRODUCTO", "UNIDADES", "BASE", "CAMBIO", "PUNTOS",
        "VALORIVA", "VALORUNIT", "VALORBRUTO", "COSTO",
    ];

    // Prioridad: lo que efectivamente se pagó (con propina) le gana al valor de la factura.
    private static readonly (string Keyword, int Priority)[] TotalKeywords =
    [
        ("TOTALAPAGAR", 3), ("TOTALCONPROPINA", 3), ("NETOAPAGAR", 3), ("VALORAPAGAR", 3),
        ("AMOUNTDUE", 3), ("BALANCEDUE", 3), ("TOTALDUE", 3),
        ("TOTALFACTURA", 2), ("VALORTOTAL", 2), ("TOTALVENTA", 2), ("ORDERTOTAL", 2), ("GRANDTOTAL", 2),
        ("COMPRANETA", 2), ("TOTALCOP", 2), ("TOTALNETO", 2), ("IMPORTETOTAL", 2), ("TOTALAMOUNT", 2),
        ("TOTAL", 1),
        // Comprobantes de transferencia ("Valor de la transferencia" y el monto en la fila de abajo): la etiqueta más débil.
        ("VALOR", 0),
    ];

    private static readonly string[] PaymentKeywords =
    [
        "TARJ", "EFECTIVO", "DEBITO", "CREDITO", "MASTERCARD", "MASTER", "VISA", "AMEX", "DINERS", "NEQUI",
        "DAVIPLATA", "RECIBIDO", "TRANSFERENCIA", "MEDIOSDEPAGO", "CASH", "DEBIT", "TENDER",
    ];

    private static readonly string[] TaxAndTipKeywords =
        ["IVA", "INC", "IMPOCONSUMO", "IMPC", "IMPUESTO", "PROPINA", "SERV", "ICO", "IBUA", "ICUI"];

    private sealed record TotalResult(IReadOnlyList<Money> Candidates, bool Confirmed);

    private static TotalResult FindTotals(List<Line> lines, Currency currency)
    {
        var scored = new List<(decimal Value, int Score, int Line)>();

        foreach (var line in lines)
        {
            if (line.Has(TotalExclusions) || line.HasEnglishExclusion)
            {
                continue;
            }

            var match = TotalKeywords.FirstOrDefault(k => line.Compact.Contains(k.Keyword, StringComparison.Ordinal));
            if (match.Keyword is null)
            {
                continue;
            }

            // El monto va después de la palabra clave ("Tot. Unds: 3 TOTAL : 8,580").
            var keywordEnd = KeywordEnd(line.Text, match.Keyword);
            var amounts = line.Amounts.Where(a => a.Start + a.Length > keywordEnd).ToList();

            // Resumen de impuestos: "TOTAL 93,400 84,034 9,366".
            if (amounts.Count >= 3 && match.Priority < 3)
            {
                continue;
            }

            var amount = amounts.Count > 0 ? amounts[0] : NeighborAmount(lines, line.Index);
            if (amount is { } a && IsPlausible(a.Value, currency))
            {
                scored.Add((a.Value, match.Priority * 10 + (a.HasCurrencySymbol ? 2 : 0), line.Index));
            }
        }

        var payments = lines
            .Where(l => l.Has(PaymentKeywords) && !l.Has("AHORRO", "IMPUESTO", "CAMBIO") && !l.HasEnglishChange
                && !l.HasEnglishExclusion && l.Amounts.Count > 0)
            .Select(l => PaymentAmount(l))
            .Where(v => v is { } p && IsPlausible(p, currency))
            .Select(v => v!.Value)
            .ToList();

        if (scored.Count == 0 && payments.Count > 0)
        {
            // Sin palabra de total (Homecenter): lo pagado menos el cambio.
            var change = lines.Where(l => (l.Has("CAMBIO") || l.HasEnglishChange) && l.Amounts.Count > 0).Select(l => l.Amounts[^1].Value)
                .FirstOrDefault();
            var paid = payments.Max() - change;
            if (IsPlausible(paid, currency))
            {
                scored.Add((paid, 5, lines.Count));
            }
        }

        if (scored.Count == 0)
        {
            // Último recurso: el mayor monto de la parte inferior, con baja prioridad.
            foreach (var line in lines.Skip(lines.Count / 3))
            {
                if (!line.Has(TotalExclusions) && !line.HasEnglishExclusion && line.Amounts.Count is > 0 and < 3)
                {
                    // Un número sin "$" ni separadores suele ser un código (PLU, consecutivo).
                    foreach (var a in line.Amounts.Where(a => IsPlausible(a.Value, currency)))
                    {
                        var separated = a.HasDecimals || line.Text.AsSpan(a.Start, a.Length).IndexOfAny('.', ',') >= 0;
                        scored.Add((a.Value, a.HasCurrencySymbol ? 3 : separated || a.Value < 1000 ? 2 : 0, line.Index));
                    }
                }
            }
        }

        var candidates = scored
            .GroupBy(s => s.Value)
            .Select(g => (Value: g.Key, Score: g.Max(s => s.Score), Line: g.Min(s => s.Line)))
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Value)
            .Take(3)
            .Select(s => ToMoney(s.Value, currency))
            .ToList();

        var confirmed = candidates.Count > 0
            && (payments.Contains(candidates[0].ToDecimal()) || MatchesSubtotalPlusTaxes(lines, candidates[0].ToDecimal()));
        return new TotalResult(candidates, confirmed);
    }

    /// <summary>
    /// Último monto después de la palabra de medio de pago: "RRN: 26526 MASTERCARD" no paga 26.526.
    /// Si hay un monto antes de la palabra y ninguno después ("$50.000 EFECTIVO"), se usa ese.
    /// </summary>
    private static decimal? PaymentAmount(Line line)
    {
        var keyword = PaymentKeywords.OrderByDescending(k => k.Length)
            .FirstOrDefault(k => line.Compact.Contains(k, StringComparison.Ordinal));
        var end = keyword is null ? 0 : KeywordEnd(line.Text, keyword);
        var after = line.Amounts.Where(a => a.Start >= end).ToList();
        if (after.Count > 0)
        {
            return after[^1].Value;
        }

        // Antes de la palabra solo vale con "$" o separadores (un número pelado es un código: RRN, recibo).
        var before = line.Amounts.Where(a => a.HasCurrencySymbol || a.HasDecimals
            || line.Text.AsSpan(a.Start, a.Length).Trim().IndexOfAny('.', ',', ' ') >= 0).ToList();
        return before.Count > 0 ? before[^1].Value : null;
    }

    /// <summary>Monto de la fila siguiente o anterior cuando la etiqueta quedó sola (papel curvo, columnas).</summary>
    private static AmountToken? NeighborAmount(List<Line> lines, int index)
    {
        foreach (var i in (int[])[index + 1, index - 1])
        {
            if (i >= 0 && i < lines.Count && lines[i].IsAmountOnly)
            {
                return lines[i].Amounts[0];
            }
        }

        return null;
    }

    /// <summary>Subtotal + impuestos/propina de las filas entre el subtotal y el total = total.</summary>
    private static bool MatchesSubtotalPlusTaxes(List<Line> lines, decimal total)
    {
        var subtotalIndex = lines.FindIndex(l => l.Has("SUBTOTAL", "SUBTOT") && l.Amounts.Count > 0);
        if (subtotalIndex < 0)
        {
            return false;
        }

        var sum = lines[subtotalIndex].Amounts[^1].Value;
        foreach (var line in lines.Skip(subtotalIndex + 1).Take(6))
        {
            if (line.Amounts.Count > 0 && line.Amounts[^1].Value == total && sum == total)
            {
                return true;
            }

            if ((line.Has(TaxAndTipKeywords) || line.HasEnglishTaxOrTip) && !line.Has("TOTAL") && line.Amounts.Count > 0)
            {
                sum += line.Amounts[^1].Value;
                if (sum == total)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPlausible(decimal value, Currency currency) => currency == Currency.COP
        ? value is >= 100 and <= 100_000_000
        : value is >= 0.5m and <= 1_000_000;

    private static Money ToMoney(decimal value, Currency currency) =>
        Money.FromDecimal(decimal.Round(value, currency.Scale(), MidpointRounding.AwayFromZero), currency);

    /// <summary>Posición en <paramref name="text"/> justo después de la palabra clave compacta.</summary>
    private static int KeywordEnd(string text, string compactKeyword)
    {
        var letters = new List<(char Letter, int Index)>();
        for (var i = 0; i < text.Length; i++)
        {
            foreach (var c in TextNormalizer.Compact(text[i].ToString()))
            {
                letters.Add((c, i));
            }
        }

        var compact = new string(letters.Select(l => l.Letter).ToArray());
        var at = compact.IndexOf(compactKeyword, StringComparison.Ordinal);
        return at < 0 ? 0 : letters[at + compactKeyword.Length - 1].Index + 1;
    }

    // ---------------------------------------------------------------- Fecha

    private static readonly string[] DateHints =
        ["FECHA", "DATE", "EMISION", "EXPEDICION", "GENERACION", "GENERA", "ORDEROF", "VALIDA",
        // Parqueaderos: se paga al salir.
        "SALIDA"];

    private static readonly string[] DateNoise =
    [
        "RESOLUCION", "RESOL", "RESDIAN", "VIGENCIA", "VENCE", "VENCIMIENTO", "HASTA", "DESDE", "AUTORIZ", "RANGO",
        "DEVOLU", "PREFIJO", "HABILITA", "AUTORRETENEDOR", "CONTRIBUYENTE", "PUNTOS", "TIENES",
    ];

    private sealed record DateResult(DateTime Value, bool HasTime);

    private static DateResult? FindDate(List<Line> lines, DateOnly today, bool preferMonthFirst)
    {
        var candidates = new List<(DateOnly Date, TimeOnly? Time, int Score, int Line)>();
        foreach (var line in lines)
        {
            foreach (var token in DateParser.FindDates(line.Text, preferMonthFirst))
            {
                if (token.Date > today.AddDays(1))
                {
                    continue;
                }

                // La hora puede venir pegada a la fecha ("2026-09-2117:36:41"): se busca en el texto que sigue a la fecha.
                var after = token.End <= line.Text.Length ? line.Text[token.End..] : string.Empty;
                var time = DateParser.FindSplitTimeAfterDate(after)
                    ?? DateParser.FindTimes(after).Select(t => (TimeOnly?)t.Time).FirstOrDefault()
                    ?? DateParser.FindTimes(line.Text).Select(t => (TimeOnly?)t.Time).FirstOrDefault();
                var hinted = line.Has(DateHints);
                if (time is null && hinted)
                {
                    time = NearbyTime(lines, line.Index);
                }

                var score = (hinted ? 3 : 0) + (time is not null ? 2 : 0) - (line.Has(DateNoise) ? 6 : 0)
                    - (token.Date < today.AddDays(-400) ? 4 : 0);
                candidates.Add((token.Date, time, score, line.Index));
            }
        }

        // Solo fechas de resolución, vigencia, etc.: mejor no proponer ninguna que proponer una equivocada.
        candidates.RemoveAll(c => c.Score < 0);
        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => candidates.Count(o => o.Date == c.Date))
            .ThenBy(c => c.Line)
            .First();
        // La mejor fila puede no traer hora, pero otra con la misma fecha sí.
        var bestTime = best.Time ?? candidates.Where(c => c.Date == best.Date && c.Time is not null && c.Score >= 0)
            .OrderByDescending(c => c.Score).Select(c => c.Time).FirstOrDefault();
        var dateTime = best.Date.ToDateTime(bestTime ?? TimeOnly.MinValue, DateTimeKind.Local);
        return new DateResult(dateTime, bestTime is not null);
    }

    /// <summary>Hora en una fila vecina con "HORA" ("HORA DE GENERACION:04:07:18 PM").</summary>
    private static TimeOnly? NearbyTime(List<Line> lines, int index)
    {
        foreach (var i in (int[])[index + 1, index - 1, index + 2])
        {
            if (i >= 0 && i < lines.Count && lines[i].Has("HORA", "TIME") && DateParser.FindTimes(lines[i].Text) is [var t, ..])
            {
                return t.Time;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------- NIT

    private sealed record TaxIdResult(string Value, bool Verified, int Line);

    private static TaxIdResult? FindTaxId(List<Line> lines)
    {
        // El NIT del comercio va antes de los totales; los del pie son de proveedores de software o facturación.
        var firstTotal = lines.FindIndex(
            l => !l.Has(TotalExclusions) && !l.HasEnglishExclusion && l.Has("TOTAL") && l.Amounts.Count > 0);
        var searchEnd = firstTotal >= 0 ? firstTotal : Math.Max(MaxHeaderRows, lines.Count * 2 / 5);
        var headerEnd = Math.Min(searchEnd, Math.Max(MaxHeaderRows, lines.Count * 2 / 5));
        foreach (var line in lines.Take(searchEnd))
        {
            if (IsCustomerRow(line))
            {
                continue;
            }

            var m = NitWithKeyword().Match(line.Folded);
            if (!m.Success && line.Index < headerEnd)
            {
                // "900.335.241-1" o "900480569-1" sin la palabra NIT, solo en la cabecera.
                m = NitWithoutKeyword().Match(line.Folded);
            }

            if (m.Success && ToTaxId(m) is { } result)
            {
                return result with { Line = line.Index };
            }
        }

        return null;
    }

    private static TaxIdResult? ToTaxId(Match m)
    {
        var digits = new string(m.Groups["num"].Value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 6 or > 12 || digits.Distinct().Count() == 1)
        {
            // "222222222222" es el NIT genérico de consumidor final.
            return null;
        }

        if (m.Groups["dv"].Success)
        {
            // "900480569-L": el OCR lee a veces el 1 del DV como L o I.
            var dv = m.Groups["dv"].Value[0] is 'L' or 'I' ? 1 : m.Groups["dv"].Value[0] - '0';
            return new TaxIdResult(TaxId.Format(digits, dv), TaxId.CheckDigit(digits) == dv, -1);
        }

        // DV pegado: "8909006089".
        if (digits.Length == 10 && TaxId.CheckDigit(digits[..9]) == digits[9] - '0')
        {
            return new TaxIdResult(TaxId.Format(digits[..9], digits[9] - '0'), true, -1);
        }

        return digits.Length > 10 ? null : new TaxIdResult(TaxId.Format(digits, TaxId.CheckDigit(digits)), false, -1);
    }

    private static bool IsCustomerRow(Line line) =>
        line.Has("CLIENTE", "IDENTIFICA", "RECEPTOR", "ADQUIR", "NOMBRE", "DOCUMENTO", "CEDULA")
        || CustomerNitCc().IsMatch(line.Folded)
        || line.Has("PROVEEDOR", "FABRICANTE", "SOFTWARE", "DESARROLLADO", "EFACTURA", "AGENTECOMERCIAL",
            "IMPLEMENTADO", "TECNOLOG");

    [GeneratedRegex(@"N\s?\.?\s?I\s?\.?\s?T\s?\.?\s?[:.]?\s?(?<num>\d[\d.\s]{4,15}\d)(?:\s?-\s?(?<dv>\d)(?!\d))?")]
    private static partial Regex NitWithKeyword();

    [GeneratedRegex(@"(?<![\d.])(?<num>\d{2,3}\.\d{3}\.\d{3}|\d{9})\s?-\s?(?<dv>\d|[LI]\b)(?!\d)")]
    private static partial Regex NitWithoutKeyword();

    [GeneratedRegex(@"NIT\s?/\s?C\.?\s?C|C\.?\s?C\.?\s?/\s?NIT")]
    private static partial Regex CustomerNitCc();

    // ---------------------------------------------------------------- Moneda

    private static Currency DetectCurrency(List<Line> lines, bool hasTaxId, Currency fallback)
    {
        if (hasTaxId)
        {
            return Currency.COP;
        }

        var usd = lines.Any(l => l.Folded.Contains("US$", StringComparison.Ordinal)
                || l.Folded.Contains("U$S", StringComparison.Ordinal) || UsdCode().IsMatch(l.Folded))
            || lines.Any(l => l.Has("ORDERTOTAL", "SHIPPING", "YOURORDER", "SALESTAX", "GRANDTOTAL", "ITEMPRICE"))
            || lines.Any(l => UsReceiptWords().IsMatch(l.Folded));
        return usd ? Currency.USD : fallback;
    }

    /// <summary>Palabras que solo salen en recibos en inglés (sin NIT): TAX, CASHIER, THANK YOU, AMOUNT DUE...</summary>
    [GeneratedRegex(@"\b(?:TAX|CASHIER|THANK YOU|AMOUNT DUE|BALANCE DUE|TOTAL DUE|GRATUITY|TENDERED|CHANGE DUE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex UsReceiptWords();

    [GeneratedRegex(@"\b(?:TAX|TAXES|TIP|GRATUITY|CHANGE|CHANGE DUE|DISCOUNT|SAVINGS|TENDER|TENDERED|COUPON|BALANCE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishExclusion();

    [GeneratedRegex(@"\b(?:CHANGE|CHANGE DUE|CASH BACK)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishChange();

    [GeneratedRegex(@"\b(?:TAX|TAXES|TIP|GRATUITY)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishTaxOrTip();

    [GeneratedRegex(@"\bUSD\b")]
    private static partial Regex UsdCode();

    // ---------------------------------------------------------------- Comercio

    /// <summary>Palabras cortas que descartan la fila solo si aparecen como palabra completa.</summary>
    private static readonly HashSet<string> MerchantNoiseWords = new(StringComparer.Ordinal)
    {
        "NIT", "N.I.T", "TEL", "TEL.", "CEL", "POR", "IVA", "ORD", "NO", "NO.", "DIR", "DIR.", "MESA", "CAJA", "SEDE", "DIAN",
        "SERVER", "TABLE", "GUEST", "GUESTS", "CHECK", "ORDER",
    };

    private static readonly string[] MerchantNoise =
    [
        "TELEFONO", "CELULAR", "WHATSAPP", "CALLE", "CARRERA", "AVENIDA", "DIRECCION", "RESPONSABLE",
        "REGIMEN", "CONTRIBUYENTE", "RESOLUCION", "AGENTE", "RETENEDOR", "RETENCION", "FACTURA", "COMPROBANTE",
        "DOCUMENTO", "ORDEN", "CAJERO", "VENDEDOR", "MESERO", "NUMERO", "ACTIVIDAD", "CIIU", "TARIFA", "CONSUMIDOR", "CLIENTE", "CUENTANOS", "EXPERIENCIA", "GRACIAS", "BIENVENIDO", "COMPRA", "LUGAR",
        "EXPEDICION", "REFERENCIA", "IGNORE", "RECEIPT", "EMAIL", "CORREO", "CUFE", "PEDIDO", "TURNO", "SOMOS",
        "GRANDES", "SEGUN", "AUTORIZ", "ELECTRONICA",
        "CASHIER", "THANKYOU", "WELCOME", "SURVEY", "TRANSACTION", "TERMINAL",
    ];

    private static readonly HashSet<string> PlaceWords = new(StringComparer.Ordinal)
    {
        "SANTIAGO", "DE", "CALI", "BOGOTA", "MEDELLIN", "COLOMBIA", "VALLE", "DEL", "CAUCA", "CO", "DC", "D.C",
        "BARRANQUILLA", "CARTAGENA", "BUCARAMANGA", "PEREIRA", "PALMIRA", "MANIZALES", "ANTIOQUIA", "CUNDINAMARCA",
    };

    private static readonly HashSet<string> AddressWords = new(StringComparer.Ordinal)
    {
        "CL", "CLL", "CR", "CRA", "KR", "KRA", "AV", "AK", "AC", "DG", "TV", "CC", "C.C", "LC", "LOCAL",
        "AVE", "BLVD", "HWY", "PKWY", "STE", "SUITE", "STREET", "AVENUE",
    };

    private static readonly HashSet<string> LowercaseConnectors = new(StringComparer.Ordinal) { "de", "del", "y", "e" };

    private static IReadOnlyList<string> FindMerchants(List<Line> lines, int? taxIdLine)
    {
        var headerEnd = lines.FindIndex(l => l.Index > 0 && (DateParser.FindDates(l.Text).Count > 0
            || l.Has("FECHA", "PLU", "DESCRIPCION", "PRODUCTOS", "CODIGO", "CANT")));
        headerEnd = headerEnd < 0 ? Math.Min(lines.Count, MaxHeaderRows) : Math.Min(headerEnd, MaxHeaderRows);

        var header = new List<(int Index, string Name, bool Legal)>();
        for (var i = 0; i < headerEnd; i++)
        {
            var text = lines[i].Text;

            // "ALMACENES EXITO S.A NIT 8909006089" o "... S.A.S. 900480569-1": el nombre va antes del NIT.
            var folded = TextNormalizer.Fold(text);
            var nit = NitPrefix().Match(folded) is { Success: true } withKeyword ? withKeyword : NitWithoutKeyword().Match(folded);
            if (nit.Success && nit.Index > 0)
            {
                text = text[..nit.Index];
            }

            if (CleanName(text) is { } name)
            {
                // "SON 20-240 00 SA" (fila ilegible) no es una razón social: al quitar el sufijo debe quedar un nombre.
                var isLegal = LegalSuffix().IsMatch(TextNormalizer.Fold(name)) && StripLegalSuffix(name).Count(char.IsLetter) >= 4;
                header.Add((i, name, isLegal));
            }
        }

        // Razón social: la que tiene sufijo societario; si ninguna lo tiene, la fila justo encima del NIT
        // (persona natural: "APELLIDO1 APELLIDO2 NOMBRE1 NOMBRE2").
        var legalIndex = header.FindIndex(h => h.Legal);
        if (legalIndex < 0 && taxIdLine is { } nitLine && header.FindIndex(h => h.Index == nitLine - 1) is var above and >= 0)
        {
            header[above] = header[above] with { Legal = true };
            legalIndex = above;
        }

        string? legal = legalIndex >= 0 ? StripLegalSuffix(header[legalIndex].Name) : null;
        string? brand;
        if (legalIndex < 0)
        {
            brand = header.Select(h => h.Name).FirstOrDefault();
        }
        else
        {
            // La marca va antes de la razón social, entre ella y el NIT, o en las 2 filas siguientes.
            var anchor = Math.Max(header[legalIndex].Index, taxIdLine is { } t && t < headerEnd ? t : -1);
            var brands = header
                .Where(h => !h.Legal && (h.Index < header[legalIndex].Index || (h.Index > header[legalIndex].Index && h.Index <= anchor + 2)))
                .Select(h => h.Name)
                .ToList();

            // Un nombre muy corto suele ser el logo mal leído ("Llnle", "La P"); con razón social, mejor esa.
            brand = brands.FirstOrDefault(b => b.Count(char.IsLetter) >= 6);
        }

        // Logo leído a medias: la "marca" es un pedazo de la razón social que no calza con palabras completas
        // ("TENDER" en "MR TENDERS"); la razón social es la lectura completa. "TIENDAS ARA" en
        // "TIENDAS ARA JERONIMO MARTINS" sí calza con palabras completas y se deja como marca.
        if (brand is not null && legal is not null && IsTruncatedPartOf(brand, legal))
        {
            (brand, legal) = (legal, brand);
        }

        return new[] { brand, legal }
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => TitleCase(n!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsTruncatedPartOf(string brand, string legal)
    {
        var compactBrand = TextNormalizer.Compact(brand);
        var compactLegal = TextNormalizer.Compact(legal);
        if (compactBrand.Length == 0 || compactBrand == compactLegal || !compactLegal.Contains(compactBrand, StringComparison.Ordinal))
        {
            return false;
        }

        var legalWords = TextNormalizer.Fold(legal).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var brandWords = TextNormalizer.Fold(brand).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return !brandWords.All(legalWords.Contains);
    }

    /// <summary>Deja solo el nombre: sin códigos, números, direcciones ni prefijos. <c>null</c> si no parece un nombre.</summary>
    private static string? CleanName(string text)
    {
        text = UsCompanySuffix().Replace(NamePrefix().Replace(text, string.Empty), string.Empty);

        // Teléfonos y consecutivos: "COMPRA Y RECOGE 3054829046".
        if (LongNumber().IsMatch(text) || text.Contains('#') || text.Contains('@') || text.Contains('<') || text.Contains('>')
            || TextNormalizer.Fold(text).Contains("WWW", StringComparison.Ordinal))
        {
            return null;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('~', '*', '-', '_', ':', ',', '|', '!', '¡', '(', ')', '"'))
            .Where(w => w.Length > 0 && (!w.Any(char.IsDigit) || Ordinal().IsMatch(w)))
            .ToList();
        if (words.Count == 0)
        {
            return null;
        }

        var foldedWords = words.Select(w => TextNormalizer.Fold(w).TrimEnd('.', ',')).ToList();
        if (foldedWords.All(PlaceWords.Contains) || IsOnlyPlaces(TextNormalizer.Compact(text))
            || foldedWords.Any(AddressWords.Contains)
            || foldedWords.Any(MerchantNoiseWords.Contains))
        {
            return null;
        }

        var name = string.Join(' ', words);
        name = DotCom().Replace(name, string.Empty).Trim('.', ' ', '-');
        var letters = name.Count(char.IsLetter);
        if (letters < 3 || letters < 0.7 * name.Count(c => !char.IsWhiteSpace(c)))
        {
            return null;
        }

        // Logos leídos como basura: "Llllli", palabras sin vocales.
        if (foldedWords.Any(w => w.Length > 2 && (!w.Any(c => "AEIOUY".Contains(c)) || RepeatedLetter().IsMatch(w))))
        {
            return null;
        }

        // Aproximado: ML Kit parte y confunde letras ("RECEIP", "CULNTANOS").
        return TextNormalizer.ContainsApprox(name, MerchantNoise) ? null : name;
    }

    /// <summary>¿El texto compacto es solo nombres de lugar pegados? ("SANTIAGODECALICOLOMBIA", aunque ML Kit parta palabras).</summary>
    private static bool IsOnlyPlaces(string compact)
    {
        if (compact.Length == 0)
        {
            return false;
        }

        var reachable = new bool[compact.Length + 1];
        reachable[0] = true;
        for (var i = 0; i < compact.Length; i++)
        {
            if (!reachable[i])
            {
                continue;
            }

            foreach (var place in PlaceWords)
            {
                var p = place.Replace(".", string.Empty, StringComparison.Ordinal);
                if (compact.AsSpan(i).StartsWith(p, StringComparison.Ordinal))
                {
                    reachable[i + p.Length] = true;
                }
            }
        }

        return reachable[compact.Length];
    }

    private static string StripLegalSuffix(string name)
    {
        var folded = TextNormalizer.Fold(name);
        var m = LegalSuffix().Match(folded);
        var stripped = m.Success ? name[..m.Index] : name;

        // "COLOMBIA" al final, también partido o con una letra mal leída ("COLOMBI A", "Col Ombia", "COLOMEIA").
        var words = stripped.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        for (var k = Math.Min(3, words.Count - 1); k >= 1; k--)
        {
            var tail = TextNormalizer.Compact(string.Join(string.Empty, words.TakeLast(k)));
            if (tail.Length >= 6 && (TextNormalizer.EditDistanceAtMostOne(tail, "COLOMBIA")
                || TextNormalizer.EditDistanceAtMostOne(tail, "DECOLOMBIA")))
            {
                words.RemoveRange(words.Count - k, k);
                break;
            }
        }

        return string.Join(' ', words).Trim(' ', '.', ',', '-');
    }

    private static string TitleCase(string name)
    {
        var words = Colombia.TextInfo.ToTitleCase(name.ToLower(Colombia)).Split(' ');
        for (var i = 1; i < words.Length; i++)
        {
            // Conectores y ordinales ("5ta", no "5Ta").
            if (LowercaseConnectors.Contains(words[i].ToLowerInvariant()) || Ordinal().IsMatch(words[i]))
            {
                words[i] = words[i].ToLowerInvariant();
            }
        }

        return string.Join(' ', words);
    }

    [GeneratedRegex(@"\s(?:S\s?\.?\s?A\s?\.?\s?S|S\s?\.?\s?A|LTDA|S\s?EN\s?C|E\s?\.?\s?U|S\s?\.?\s?C\s?\.?\s?A)\s?\.?\s*$")]
    private static partial Regex LegalSuffix();

    /// <summary>Sufijo societario estadounidense ("THE GRILL HOUSE LLC"): se quita del nombre sin tratarlo como razón social.</summary>
    [GeneratedRegex(@"[\s,]+(?:INC|LLC|CORP|LTD|L\.L\.C)\.?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex UsCompanySuffix();

    [GeneratedRegex(@"\bN\s?\.?\s?I\s?\.?\s?T\b")]
    private static partial Regex NitPrefix();

    [GeneratedRegex(@"^\s*(?:PUNTO\s+DE\s+VEN\w{2}|RAZ[OÓ]N\s+SOCIAL)\s*:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex NamePrefix();

    // "PARQUEADERO LA 5TA", "LA 14".
    [GeneratedRegex(@"^\d{1,2}(?:TA|RA|DA|MA|NA|VA|TO|RO|DO|MO|NO|VO)$", RegexOptions.IgnoreCase)]
    private static partial Regex Ordinal();

    [GeneratedRegex(@"\d{7,}")]
    private static partial Regex LongNumber();

    [GeneratedRegex(@"(\p{L})\1\1")]
    private static partial Regex RepeatedLetter();

    [GeneratedRegex(@"\.com(\.co)?$", RegexOptions.IgnoreCase)]
    private static partial Regex DotCom();
}
