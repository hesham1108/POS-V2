using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoresPOS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class ProductAverageCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AverageCost",
                table: "Products",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // Start every product's average at its current purchase price.
            migrationBuilder.Sql("UPDATE [Products] SET [AverageCost] = [PurchasePrice] WHERE [AverageCost] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageCost",
                table: "Products");
        }
    }
}
