using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetWorth.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountInstruments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Instruments_Accounts_AccountId",
                table: "Instruments");

            migrationBuilder.DropIndex(
                name: "IX_Instruments_AccountId_Name",
                table: "Instruments");

            migrationBuilder.CreateTable(
                name: "AccountInstruments",
                columns: table => new
                {
                    AccountInstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountInstruments", x => x.AccountInstrumentId);
                    table.ForeignKey(
                        name: "FK_AccountInstruments_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountInstruments_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "InstrumentId");
                });

            migrationBuilder.CreateTable(
                name: "AccountInstrumentSnapshots",
                columns: table => new
                {
                    AccountInstrumentSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountInstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountInstrumentSnapshots", x => x.AccountInstrumentSnapshotId);
                    table.ForeignKey(
                        name: "FK_AccountInstrumentSnapshots_AccountInstruments_AccountInstrumentId",
                        column: x => x.AccountInstrumentId,
                        principalTable: "AccountInstruments",
                        principalColumn: "AccountInstrumentId");
                    table.ForeignKey(
                        name: "FK_AccountInstrumentSnapshots_AccountSnapshots_AccountSnapshotId",
                        column: x => x.AccountSnapshotId,
                        principalTable: "AccountSnapshots",
                        principalColumn: "AccountSnapshotId");
                });

            migrationBuilder.Sql("""
INSERT INTO AccountInstruments (AccountInstrumentId, AccountId, InstrumentId, CreatedUtc)
SELECT NEWID(), AccountId, InstrumentId, CreatedUtc
FROM Instruments

INSERT INTO AccountInstrumentSnapshots (AccountInstrumentSnapshotId, AccountSnapshotId, AccountInstrumentId, Balance, CreatedUtc)
SELECT NEWID(), iss.AccountSnapshotId, ai.AccountInstrumentId, Balance, iss.CreatedUtc
FROM InstrumentSnapshots iss
INNER JOIN AccountSnapshots ass ON ass.AccountSnapshotId = iss.AccountSnapshotId
INNER JOIN AccountInstruments ai ON ai.InstrumentId = iss.InstrumentId AND ai.AccountId = ass.AccountId
""");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Instruments");

            migrationBuilder.DropTable(
                name: "InstrumentSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Name",
                table: "Instruments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Ticker",
                table: "Instruments",
                column: "Ticker",
                unique: true,
                filter: "[Ticker] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountInstruments_AccountId_InstrumentId",
                table: "AccountInstruments",
                columns: new[] { "AccountId", "InstrumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountInstruments_InstrumentId",
                table: "AccountInstruments",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountInstrumentSnapshots_AccountInstrumentId",
                table: "AccountInstrumentSnapshots",
                column: "AccountInstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountInstrumentSnapshots_AccountSnapshotId_AccountInstrumentId",
                table: "AccountInstrumentSnapshots",
                columns: new[] { "AccountSnapshotId", "AccountInstrumentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountInstrumentSnapshots");

            migrationBuilder.DropTable(
                name: "AccountInstruments");

            migrationBuilder.DropIndex(
                name: "IX_Instruments_Name",
                table: "Instruments");

            migrationBuilder.DropIndex(
                name: "IX_Instruments_Ticker",
                table: "Instruments");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "Instruments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "InstrumentSnapshots",
                columns: table => new
                {
                    InstrumentSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentSnapshots", x => x.InstrumentSnapshotId);
                    table.ForeignKey(
                        name: "FK_InstrumentSnapshots_AccountSnapshots_AccountSnapshotId",
                        column: x => x.AccountSnapshotId,
                        principalTable: "AccountSnapshots",
                        principalColumn: "AccountSnapshotId");
                    table.ForeignKey(
                        name: "FK_InstrumentSnapshots_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "InstrumentId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_AccountId_Name",
                table: "Instruments",
                columns: new[] { "AccountId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentSnapshots_AccountSnapshotId_InstrumentId",
                table: "InstrumentSnapshots",
                columns: new[] { "AccountSnapshotId", "InstrumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentSnapshots_InstrumentId",
                table: "InstrumentSnapshots",
                column: "InstrumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Instruments_Accounts_AccountId",
                table: "Instruments",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
