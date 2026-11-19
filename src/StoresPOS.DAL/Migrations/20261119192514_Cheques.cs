using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoresPOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Cheques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cheques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Bank = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    SalesInvoiceId = table.Column<int>(type: "int", nullable: true),
                    PurchaseInvoiceId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SettledTreasuryId = table.Column<int>(type: "int", nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cheques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cheques_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cheques_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cheques_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cheques_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cheques_Treasuries_SettledTreasuryId",
                        column: x => x.SettledTreasuryId,
                        principalTable: "Treasuries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cheques_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChequeStatusHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChequeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TreasuryId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChequeStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChequeStatusHistory_Cheques_ChequeId",
                        column: x => x.ChequeId,
                        principalTable: "Cheques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChequeStatusHistory_Treasuries_TreasuryId",
                        column: x => x.TreasuryId,
                        principalTable: "Treasuries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChequeStatusHistory_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_CreatedByUserId",
                table: "Cheques",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_CustomerId",
                table: "Cheques",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_Direction",
                table: "Cheques",
                column: "Direction");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_DueDate",
                table: "Cheques",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_Number",
                table: "Cheques",
                column: "Number");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_PurchaseInvoiceId",
                table: "Cheques",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_SalesInvoiceId",
                table: "Cheques",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_SettledTreasuryId",
                table: "Cheques",
                column: "SettledTreasuryId");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_Status",
                table: "Cheques",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_SupplierId",
                table: "Cheques",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ChequeStatusHistory_ChequeId_CreatedAtUtc",
                table: "ChequeStatusHistory",
                columns: new[] { "ChequeId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChequeStatusHistory_TreasuryId",
                table: "ChequeStatusHistory",
                column: "TreasuryId");

            migrationBuilder.CreateIndex(
                name: "IX_ChequeStatusHistory_UserId",
                table: "ChequeStatusHistory",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChequeStatusHistory");

            migrationBuilder.DropTable(
                name: "Cheques");
        }
    }
}
