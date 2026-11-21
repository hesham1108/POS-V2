using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoresPOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class CustomerType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "CustomerType",
                table: "Customers",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1); // 1 = Retail for any existing row

            // Backfill: customers whose default price level is wholesale become Wholesale (2); everyone else stays Retail (1).
            migrationBuilder.Sql("UPDATE [Customers] SET [CustomerType] = CASE WHEN [DefaultSaleType] = 2 THEN 2 ELSE 1 END;");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerType",
                table: "Customers",
                column: "CustomerType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_CustomerType",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerType",
                table: "Customers");
        }
    }
}
