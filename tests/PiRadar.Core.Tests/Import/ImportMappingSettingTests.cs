using PiRadar.Core.Import;

namespace PiRadar.Core.Tests.Import;

public class ImportMappingSettingTests
{
    [Fact]
    public void Round_trips()
    {
        var saved = new SavedImportMapping("FECHA|MONTO", [ImportField.Date, ImportField.Amount, ImportField.Ignore], DateOrder.MonthFirst);
        var back = ImportMappingSetting.Parse(ImportMappingSetting.Serialize(saved));
        Assert.NotNull(back);
        Assert.Equal(saved.Fingerprint, back.Fingerprint);
        Assert.Equal(saved.Columns, back.Columns);
        Assert.Equal(DateOrder.MonthFirst, back.DateOrder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("{}")]
    [InlineData("[1,2]")]
    [InlineData("{\"F\":\"a\",\"C\":[99],\"D\":0}")]
    [InlineData("{\"F\":\"a\",\"C\":[1],\"D\":7}")]
    [InlineData("{\"F\":\"a\",\"C\":\"x\",\"D\":0}")]
    [InlineData("{\"F\":\"a\",\"C\":[],\"D\":0}")]
    public void Garbage_gives_null(string? value) => Assert.Null(ImportMappingSetting.Parse(value));

    [Fact]
    public void Apply_requires_same_headers_and_column_count()
    {
        var sheet = new SheetData("S",
            [[CellValue.FromText("Fecha"), CellValue.FromText("Valor")], [CellValue.FromText("1"), CellValue.FromText("2")]], 2);
        var detected = new ImportMapping(0, [ImportField.Ignore, ImportField.Ignore]);
        var fp = detected.Fingerprint(sheet)!;
        var ok = ImportMappingSetting.Apply(new SavedImportMapping(fp, [ImportField.Date, ImportField.Amount], DateOrder.DayFirst), detected, sheet);
        Assert.Equal([ImportField.Date, ImportField.Amount], ok!.Columns);
        Assert.Null(ImportMappingSetting.Apply(new SavedImportMapping("OTHER", [ImportField.Date, ImportField.Amount], DateOrder.DayFirst), detected, sheet));
        Assert.Null(ImportMappingSetting.Apply(new SavedImportMapping(fp, [ImportField.Date], DateOrder.DayFirst), detected, sheet));
        Assert.Null(ImportMappingSetting.Apply(null, detected, sheet));
        Assert.Null(ImportMappingSetting.Apply(new SavedImportMapping(fp, [ImportField.Date, ImportField.Amount], DateOrder.DayFirst),
            new ImportMapping(null, [ImportField.Ignore, ImportField.Ignore]), sheet));
    }
}
