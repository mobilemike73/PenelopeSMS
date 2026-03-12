using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenelopeSMS.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialImportSchema : Migration
    {
        private static readonly string[] CustomerPhoneLinkKeyColumns =
        [
            "CustSid",
            "PhoneNumberRecordId"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowsRead = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RowsImported = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RowsRejected = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhoneNumberRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CanonicalPhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneNumberRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPhoneLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustSid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RawPhoneNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PhoneNumberRecordId = table.Column<int>(type: "int", nullable: false),
                    ImportBatchId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPhoneLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPhoneLinks_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerPhoneLinks_PhoneNumberRecords_PhoneNumberRecordId",
                        column: x => x.PhoneNumberRecordId,
                        principalTable: "PhoneNumberRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhoneLinks_CustSid_PhoneNumberRecordId",
                table: "CustomerPhoneLinks",
                columns: CustomerPhoneLinkKeyColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhoneLinks_ImportBatchId",
                table: "CustomerPhoneLinks",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhoneLinks_PhoneNumberRecordId",
                table: "CustomerPhoneLinks",
                column: "PhoneNumberRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneNumberRecords_CanonicalPhoneNumber",
                table: "PhoneNumberRecords",
                column: "CanonicalPhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerPhoneLinks");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "PhoneNumberRecords");
        }
    }
}
