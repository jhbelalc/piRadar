using PiRadar.Core.Domain;
using PiRadar.Core.Import;

namespace PiRadar.Core.Tests.Import;

public class ImportDuplicatesTests
{
    private static ImportRow Row(int n, int day = 3, long amount = 5000, TransactionType type = TransactionType.Expense,
        Currency cur = Currency.COP, string text = "Mercado") =>
        new(n, type, new Money(amount, cur), new DateTime(2026, 8, day, 12, 0, 0, DateTimeKind.Local),
            null, null, null, text, null, null, false, false);

    private static Transaction Tx(int day = 3, long amount = 5000, TransactionType type = TransactionType.Expense,
        Currency cur = Currency.COP, string? desc = "mercado") =>
        new() { Type = type, Amount = new Money(amount, cur), OccurredAt = new DateTime(2026, 8, day, 9, 0, 0), Description = desc };

    [Fact]
    public void Reimport_marks_all()
    {
        var rows = new[] { Row(2), Row(3, day: 4, amount: 100) };
        var db = new[] { Tx(), Tx(day: 4, amount: 100) };
        Assert.Equal(new HashSet<int> { 2, 3 }, ImportDuplicates.Find(rows, db));
    }

    [Fact]
    public void Two_in_file_one_in_db_marks_first_only()
    {
        Assert.Equal(new HashSet<int> { 2 }, ImportDuplicates.Find([Row(2), Row(3)], [Tx()]));
    }

    [Fact]
    public void Repeats_inside_file_without_db_are_not_marked()
    {
        Assert.Empty(ImportDuplicates.Find([Row(2), Row(3)], []));
    }

    [Fact]
    public void Any_difference_is_not_duplicate()
    {
        var db = new[] { Tx() };
        Assert.Empty(ImportDuplicates.Find([Row(2, day: 4)], db));
        Assert.Empty(ImportDuplicates.Find([Row(2, amount: 5001)], db));
        Assert.Empty(ImportDuplicates.Find([Row(2, type: TransactionType.Income)], db));
        Assert.Empty(ImportDuplicates.Find([Row(2, cur: Currency.USD)], db));
        Assert.Empty(ImportDuplicates.Find([Row(2, text: "Taxi")], db));
    }

    [Fact]
    public void Merchant_is_used_when_no_description()
    {
        Assert.Equal(new HashSet<int> { 2 }, ImportDuplicates.Find([Row(2)], [Tx(desc: null).WithMerchant("Mercado")]));
    }
}

internal static class TxExt
{
    public static Transaction WithMerchant(this Transaction t, string merchant)
    {
        t.Merchant = merchant;
        return t;
    }
}
