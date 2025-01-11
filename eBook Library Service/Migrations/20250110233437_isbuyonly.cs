using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eBook_Library_Service.Migrations
{
    /// <inheritdoc />
    public partial class isbuyonly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBuyOnly",
                table: "Books",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Books",
                keyColumn: "BookId",
                keyValue: 1,
                column: "IsBuyOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "Books",
                keyColumn: "BookId",
                keyValue: 2,
                column: "IsBuyOnly",
                value: false);

            migrationBuilder.UpdateData(
                table: "Books",
                keyColumn: "BookId",
                keyValue: 3,
                column: "IsBuyOnly",
                value: false);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistories_BookId",
                table: "PurchaseHistories",
                column: "BookId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseHistories_Books_BookId",
                table: "PurchaseHistories",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "BookId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseHistories_Books_BookId",
                table: "PurchaseHistories");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseHistories_BookId",
                table: "PurchaseHistories");

            migrationBuilder.DropColumn(
                name: "IsBuyOnly",
                table: "Books");
        }
    }
}
