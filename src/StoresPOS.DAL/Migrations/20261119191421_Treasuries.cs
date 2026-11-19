using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StoresPOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Treasuries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HandoverAmount",
                table: "Shifts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HandoverTreasuryId",
                table: "Shifts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaidFromTreasuryId",
                table: "PurchaseInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Treasuries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Treasuries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TreasuryTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TreasuryId = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceType = table.Column<byte>(type: "tinyint", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TransferGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreasuryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreasuryTransactions_Treasuries_TreasuryId",
                        column: x => x.TreasuryId,
                        principalTable: "Treasuries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreasuryTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Treasuries",
                columns: new[] { "Id", "CreatedAtUtc", "IsActive", "IsDefault", "NameAr", "NameEn", "OpeningBalance", "Type", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Utc), true, true, "الخزنة النقدية", "Main cash", 0m, (byte)1, null },
                    { 2, new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "الحساب البنكي", "Main bank", 0m, (byte)2, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_HandoverTreasuryId",
                table: "Shifts",
                column: "HandoverTreasuryId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_PaidFromTreasuryId",
                table: "PurchaseInvoices",
                column: "PaidFromTreasuryId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_DefaultTreasuryId",
                table: "PaymentMethods",
                column: "DefaultTreasuryId");

            migrationBuilder.CreateIndex(
                name: "IX_Treasuries_IsActive",
                table: "Treasuries",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Treasuries_IsDefault",
                table: "Treasuries",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_SourceType_SourceId",
                table: "TreasuryTransactions",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_TransferGroupId",
                table: "TreasuryTransactions",
                column: "TransferGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_TreasuryId_CreatedAtUtc",
                table: "TreasuryTransactions",
                columns: new[] { "TreasuryId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_UserId",
                table: "TreasuryTransactions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentMethods_Treasuries_DefaultTreasuryId",
                table: "PaymentMethods",
                column: "DefaultTreasuryId",
                principalTable: "Treasuries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Treasuries_PaidFromTreasuryId",
                table: "PurchaseInvoices",
                column: "PaidFromTreasuryId",
                principalTable: "Treasuries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Treasuries_HandoverTreasuryId",
                table: "Shifts",
                column: "HandoverTreasuryId",
                principalTable: "Treasuries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentMethods_Treasuries_DefaultTreasuryId",
                table: "PaymentMethods");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Treasuries_PaidFromTreasuryId",
                table: "PurchaseInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Treasuries_HandoverTreasuryId",
                table: "Shifts");

            migrationBuilder.DropTable(
                name: "TreasuryTransactions");

            migrationBuilder.DropTable(
                name: "Treasuries");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_HandoverTreasuryId",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_PaidFromTreasuryId",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PaymentMethods_DefaultTreasuryId",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "HandoverAmount",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "HandoverTreasuryId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "PaidFromTreasuryId",
                table: "PurchaseInvoices");
        }
    }
}
