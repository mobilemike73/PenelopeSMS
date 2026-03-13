using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenelopeSMS.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddVipAudienceAndPhoneSource : Migration
    {
        private static readonly string[] CustomerPhoneLinkSourceIndexColumns =
        [
            "CustSid",
            "PhoneNumberRecordId",
            "ImportedPhoneSource"
        ];

        private static readonly string[] CustomerPhoneLinkVipIndexColumns =
        [
            "PhoneNumberRecordId",
            "IsVip"
        ];

        private static readonly string[] CustomerPhoneLinkLegacyIndexColumns =
        [
            "CustSid",
            "PhoneNumberRecordId"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerPhoneLinks_CustSid_PhoneNumberRecordId",
                table: "CustomerPhoneLinks");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPhoneLinks_PhoneNumberRecordId",
                table: "CustomerPhoneLinks");

            migrationBuilder.AddColumn<int>(
                name: "ImportedPhoneSource",
                table: "CustomerPhoneLinks",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsVip",
                table: "CustomerPhoneLinks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AudienceSegment",
                table: "Campaigns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhoneLinks_CustSid_PhoneNumberRecordId_ImportedPhoneSource",
                table: "CustomerPhoneLinks",
                columns: CustomerPhoneLinkSourceIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhoneLinks_PhoneNumberRecordId_IsVip",
                table: "CustomerPhoneLinks",
                columns: CustomerPhoneLinkVipIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerPhoneLinks_CustSid_PhoneNumberRecordId_ImportedPhoneSource",
                table: "CustomerPhoneLinks");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPhoneLinks_PhoneNumberRecordId_IsVip",
                table: "CustomerPhoneLinks");

            migrationBuilder.DropColumn(
                name: "ImportedPhoneSource",
                table: "CustomerPhoneLinks");

            migrationBuilder.DropColumn(
                name: "IsVip",
                table: "CustomerPhoneLinks");

            migrationBuilder.DropColumn(
                name: "AudienceSegment",
                table: "Campaigns");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhoneLinks_CustSid_PhoneNumberRecordId",
                table: "CustomerPhoneLinks",
                columns: CustomerPhoneLinkLegacyIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPhoneLinks_PhoneNumberRecordId",
                table: "CustomerPhoneLinks",
                column: "PhoneNumberRecordId");
        }
    }
}
