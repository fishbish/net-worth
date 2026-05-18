using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetWorth.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountInstrumentCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Instruments_Accounts_AccountId",
                table: "Instruments");

            migrationBuilder.AddForeignKey(
                name: "FK_Instruments_Accounts_AccountId",
                table: "Instruments",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Instruments_Accounts_AccountId",
                table: "Instruments");

            migrationBuilder.AddForeignKey(
                name: "FK_Instruments_Accounts_AccountId",
                table: "Instruments",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId");
        }
    }
}
