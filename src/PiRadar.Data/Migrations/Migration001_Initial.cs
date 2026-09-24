using PiRadar.Data.Seed;
using SQLite;

namespace PiRadar.Data.Migrations;

/// <summary>Esquema inicial y datos por defecto. Tipos de columna iguales a los que usa sqlite-net.</summary>
internal sealed class Migration001_Initial : IMigration
{
    public int Version => 1;

    public void Apply(SQLiteConnection db)
    {
        db.Execute("""
            CREATE TABLE "Category" (
                "Id" integer PRIMARY KEY AUTOINCREMENT NOT NULL,
                "Name" varchar NOT NULL,
                "Kind" integer NOT NULL,
                "Icon" varchar NOT NULL,
                "Color" varchar NOT NULL,
                "ParentId" integer NULL REFERENCES "Category"("Id"),
                "SortOrder" integer NOT NULL DEFAULT 0,
                "IsArchived" integer NOT NULL DEFAULT 0,
                "IsSystem" integer NOT NULL DEFAULT 0
            )
            """);

        db.Execute("""
            CREATE TABLE "PaymentMethod" (
                "Id" integer PRIMARY KEY AUTOINCREMENT NOT NULL,
                "Name" varchar NOT NULL,
                "Kind" integer NOT NULL,
                "Icon" varchar NOT NULL,
                "LastFourDigits" varchar NULL,
                "SortOrder" integer NOT NULL DEFAULT 0,
                "IsArchived" integer NOT NULL DEFAULT 0,
                "IsSystem" integer NOT NULL DEFAULT 0
            )
            """);

        db.Execute("""
            CREATE TABLE "Receipt" (
                "Id" varchar(36) PRIMARY KEY NOT NULL,
                "CapturedAt" bigint NOT NULL,
                "OcrText" varchar NOT NULL,
                "ParsedTotalMinor" bigint NULL,
                "ParsedCurrency" varchar NULL,
                "ParsedDate" bigint NULL,
                "ParsedMerchant" varchar NULL,
                "ParsedTaxId" varchar NULL,
                "OcrEngine" varchar NOT NULL
            )
            """);

        db.Execute("""
            CREATE TABLE "ReceiptImage" (
                "ReceiptId" varchar(36) PRIMARY KEY NOT NULL REFERENCES "Receipt"("Id") ON DELETE CASCADE,
                "Jpeg" blob NOT NULL,
                "Width" integer NOT NULL,
                "Height" integer NOT NULL
            )
            """);

        db.Execute("""
            CREATE TABLE "Transactions" (
                "Id" varchar(36) PRIMARY KEY NOT NULL,
                "Type" integer NOT NULL,
                "AmountMinor" bigint NOT NULL CHECK ("AmountMinor" >= 0),
                "Currency" varchar NOT NULL,
                "OccurredAt" bigint NOT NULL,
                "CategoryId" integer NOT NULL REFERENCES "Category"("Id"),
                "PaymentMethodId" integer NULL REFERENCES "PaymentMethod"("Id"),
                "Merchant" varchar NULL,
                "Description" varchar NULL,
                "Notes" varchar NULL,
                "ReceiptId" varchar(36) NULL REFERENCES "Receipt"("Id") ON DELETE SET NULL,
                "Source" integer NOT NULL,
                "CreatedAt" bigint NOT NULL,
                "UpdatedAt" bigint NOT NULL
            )
            """);
        db.Execute("""CREATE INDEX "IX_Transactions_OccurredAt" ON "Transactions" ("OccurredAt")""");
        db.Execute("""CREATE INDEX "IX_Transactions_Category_OccurredAt" ON "Transactions" ("CategoryId", "OccurredAt")""");
        db.Execute("""CREATE INDEX "IX_Transactions_PaymentMethodId" ON "Transactions" ("PaymentMethodId")""");
        db.Execute("""CREATE INDEX "IX_Transactions_Currency" ON "Transactions" ("Currency")""");
        db.Execute("""CREATE INDEX "IX_Transactions_ReceiptId" ON "Transactions" ("ReceiptId")""");

        db.Execute("""
            CREATE TABLE "ExchangeRate" (
                "Id" integer PRIMARY KEY AUTOINCREMENT NOT NULL,
                "FromCurrency" varchar NOT NULL,
                "ToCurrency" varchar NOT NULL,
                "RateMicros" bigint NOT NULL CHECK ("RateMicros" > 0),
                "EffectiveDate" bigint NOT NULL
            )
            """);
        db.Execute("""
            CREATE UNIQUE INDEX "UX_ExchangeRate_Pair_Date" ON "ExchangeRate" ("FromCurrency", "ToCurrency", "EffectiveDate")
            """);

        db.Execute("""
            CREATE TABLE "AppSetting" (
                "Key" varchar PRIMARY KEY NOT NULL,
                "Value" varchar NOT NULL
            )
            """);

        var order = 0;
        foreach (var (name, kind, icon, color) in DefaultData.Categories)
        {
            db.Execute(
                """INSERT INTO "Category" ("Name", "Kind", "Icon", "Color", "SortOrder", "IsSystem") VALUES (?, ?, ?, ?, ?, 1)""",
                name, (int)kind, icon, color, order++);
        }

        order = 0;
        foreach (var (name, kind, icon) in DefaultData.PaymentMethods)
        {
            db.Execute(
                """INSERT INTO "PaymentMethod" ("Name", "Kind", "Icon", "SortOrder", "IsSystem") VALUES (?, ?, ?, ?, 1)""",
                name, (int)kind, icon, order++);
        }

        db.Execute("""INSERT INTO "AppSetting" ("Key", "Value") VALUES (?, ?)""", AppSettingKeys.BaseCurrency, "COP");
    }
}
