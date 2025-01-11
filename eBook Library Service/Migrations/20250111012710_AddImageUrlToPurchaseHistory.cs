using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eBook_Library_Service.Migrations
{
    /// <inheritdoc />
    public partial class AddImageUrlToPurchaseHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "PurchaseHistories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "PurchaseHistories");
        }
    }
}
