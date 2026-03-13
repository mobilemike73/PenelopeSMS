using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861

#nullable disable

namespace PenelopeSMS.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryPipelineSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentStatusAtUtc",
                table: "CampaignRecipients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStatusRawValue",
                table: "CampaignRecipients",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentStatusTimeSource",
                table: "CampaignRecipients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryErrorCode",
                table: "CampaignRecipients",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryErrorMessage",
                table: "CampaignRecipients",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDeliveryCallbackReceivedAtUtc",
                table: "CampaignRecipients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CampaignRecipientStatusHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignRecipientId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProviderEventAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventTimeSource = table.Column<int>(type: "int", nullable: false),
                    ProviderEventRawValue = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProviderErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProviderErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RawPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CallbackFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignRecipientStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignRecipientStatusHistory_CampaignRecipients_CampaignRecipientId",
                        column: x => x.CampaignRecipientId,
                        principalTable: "CampaignRecipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RejectedDeliveryCallbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RejectionReason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CallbackFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RawPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignatureHeader = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RejectedDeliveryCallbacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnmatchedDeliveryCallbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TwilioMessageSid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MessageStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CallbackFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RawPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProviderErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ProviderEventRawValue = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnmatchedDeliveryCallbacks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipientStatusHistory_CampaignRecipientId_CallbackFingerprint",
                table: "CampaignRecipientStatusHistory",
                columns: new[] { "CampaignRecipientId", "CallbackFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RejectedDeliveryCallbacks_CallbackFingerprint",
                table: "RejectedDeliveryCallbacks",
                column: "CallbackFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnmatchedDeliveryCallbacks_CallbackFingerprint",
                table: "UnmatchedDeliveryCallbacks",
                column: "CallbackFingerprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignRecipientStatusHistory");

            migrationBuilder.DropTable(
                name: "RejectedDeliveryCallbacks");

            migrationBuilder.DropTable(
                name: "UnmatchedDeliveryCallbacks");

            migrationBuilder.DropColumn(
                name: "CurrentStatusAtUtc",
                table: "CampaignRecipients");

            migrationBuilder.DropColumn(
                name: "CurrentStatusRawValue",
                table: "CampaignRecipients");

            migrationBuilder.DropColumn(
                name: "CurrentStatusTimeSource",
                table: "CampaignRecipients");

            migrationBuilder.DropColumn(
                name: "DeliveryErrorCode",
                table: "CampaignRecipients");

            migrationBuilder.DropColumn(
                name: "DeliveryErrorMessage",
                table: "CampaignRecipients");

            migrationBuilder.DropColumn(
                name: "LastDeliveryCallbackReceivedAtUtc",
                table: "CampaignRecipients");
        }
    }
}
