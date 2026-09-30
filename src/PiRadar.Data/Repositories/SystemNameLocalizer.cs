using PiRadar.Core.Abstractions;
using PiRadar.Core.Domain;
using PiRadar.Data.Seed;

namespace PiRadar.Data.Repositories;

internal sealed class SystemNameLocalizer(DatabaseHost host) : RepositoryBase(host), ISystemNameLocalizer
{
    public Task<int> ApplyAsync(AppLanguage target) => InTransactionAsync(db =>
    {
        var changed = 0;
        for (var i = 0; i < DefaultData.SpanishCategories.Count; i++)
        {
            var (spanish, kind, _, _) = DefaultData.SpanishCategories[i];
            var (from, to) = Pair(spanish, DefaultData.EnglishCategoryNames[i], target);
            changed += Rename(db, "Category", from, to, (int)kind);
        }

        for (var i = 0; i < DefaultData.TranslatablePaymentMethods; i++)
        {
            var (from, to) = Pair(
                DefaultData.SpanishPaymentMethods[i].Name, DefaultData.EnglishPaymentMethodNames[i], target);
            changed += Rename(db, "PaymentMethod", from, to, kind: null);
        }

        return changed;
    });

    private static (string From, string To) Pair(string spanish, string english, AppLanguage target) =>
        target == AppLanguage.English ? (spanish, english) : (english, spanish);

    /// <summary>
    /// Renombra la fila de sistema llamada <paramref name="from"/> a <paramref name="to"/>, salvo que ya exista
    /// otra fila (del usuario) con ese nombre entre las activas del mismo tipo: entonces la deja como está.
    /// </summary>
    private static int Rename(SQLite.SQLiteConnection db, string table, string from, string to, int? kind)
    {
        var kindFilter = kind is null ? string.Empty : """ AND "Kind" = ?""";
        object[] kindArgs = kind is null ? [] : [kind.Value];

        var ids = db.Query<IdRow>(
            $"""SELECT "Id" FROM "{table}" WHERE "IsSystem" = 1 AND "Name" = ?{kindFilter}""",
            [from, .. kindArgs]);
        var count = 0;
        foreach (var row in ids)
        {
            var taken = db.ExecuteScalar<int>(
                $"""SELECT EXISTS(SELECT 1 FROM "{table}" WHERE "Name" = ? COLLATE NOCASE AND "Id" <> ?{kindFilter} AND "IsArchived" = 0)""",
                [to, row.Id, .. kindArgs]) == 1;
            if (taken)
            {
                continue;
            }

            db.Execute($"""UPDATE "{table}" SET "Name" = ? WHERE "Id" = ?""", to, row.Id);
            count++;
        }

        return count;
    }

    private sealed class IdRow
    {
        public int Id { get; set; }
    }
}
