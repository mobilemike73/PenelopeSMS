using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenelopeSMS.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneEnrichmentSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CampaignEligibilityStatus",
                table: "PhoneNumberRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EligibilityEvaluatedAtUtc",
                table: "PhoneNumberRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EnrichmentFailureStatus",
                table: "PhoneNumberRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEnrichedAtUtc",
                table: "PhoneNumberRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEnrichmentAttemptedAtUtc",
                table: "PhoneNumberRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEnrichmentErrorCode",
                table: "PhoneNumberRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEnrichmentErrorMessage",
                table: "PhoneNumberRecords",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEnrichmentFailedAtUtc",
                table: "PhoneNumberRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwilioCarrierName",
                table: "PhoneNumberRecords",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwilioCountryCode",
                table: "PhoneNumberRecords",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwilioLineType",
                table: "PhoneNumberRecords",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwilioLookupPayloadJson",
                table: "PhoneNumberRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwilioMobileCountryCode",
                table: "PhoneNumberRecords",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwilioMobileNetworkCode",
                table: "PhoneNumberRecords",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CampaignEligibilityStatus",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "EligibilityEvaluatedAtUtc",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "EnrichmentFailureStatus",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "LastEnrichedAtUtc",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "LastEnrichmentAttemptedAtUtc",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "LastEnrichmentErrorCode",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "LastEnrichmentErrorMessage",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "LastEnrichmentFailedAtUtc",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "TwilioCarrierName",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "TwilioCountryCode",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "TwilioLineType",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "TwilioLookupPayloadJson",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "TwilioMobileCountryCode",
                table: "PhoneNumberRecords");

            migrationBuilder.DropColumn(
                name: "TwilioMobileNetworkCode",
                table: "PhoneNumberRecords");
        }
    }
}
