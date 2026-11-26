using SQLite;

namespace PiRadar.Data.Migrations;

/// <summary>
/// Lotes de importación: cada importación de Excel/CSV crea un lote y sus movimientos lo referencian para poder
/// deshacerla. Si se borra el lote, los movimientos quedan sin lote (ON DELETE SET NULL).
/// </summary>
internal sealed class Migration002_ImportBatch : IMigration
{
    public int Version => 2;

    public void Apply(SQLiteConnection db)
    {
        db.Execute("""
            CREATE TABLE "ImportBatch" (
                "Id" varchar(36) PRIMARY KEY NOT NULL,
                "ImportedAt" bigint NOT NULL,
                "FileName" varchar NULL,
                "RowCount" integer NOT NULL
            )
            """);
        db.Execute("""
            ALTER TABLE "Transactions" ADD COLUMN "ImportBatchId" varchar(36) NULL REFERENCES "ImportBatch"("Id") ON DELETE SET NULL
            """);
        db.Execute("""CREATE INDEX "IX_Transactions_ImportBatchId" ON "Transactions" ("ImportBatchId")""");
    }
}
