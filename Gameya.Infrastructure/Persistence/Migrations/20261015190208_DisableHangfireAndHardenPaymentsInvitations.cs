using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gameya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DisableHangfireAndHardenPaymentsInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentUserInvitations_OwnerId",
                table: "PaymentUserInvitations");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RecordedByUserId",
                table: "Payments");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "PaymentUserInvitations",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAtUtc",
                table: "PaymentUserInvitations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevokedByUserId",
                table: "PaymentUserInvitations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentUserInvitations_OwnerId_CreatedAtUtc",
                table: "PaymentUserInvitations",
                columns: new[] { "OwnerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RecordedByUserId_IdempotencyKey",
                table: "Payments",
                columns: new[] { "RecordedByUserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentUserInvitations_OwnerId_CreatedAtUtc",
                table: "PaymentUserInvitations");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RecordedByUserId_IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "PaymentUserInvitations");

            migrationBuilder.DropColumn(
                name: "RevokedAtUtc",
                table: "PaymentUserInvitations");

            migrationBuilder.DropColumn(
                name: "RevokedByUserId",
                table: "PaymentUserInvitations");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentUserInvitations_OwnerId",
                table: "PaymentUserInvitations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RecordedByUserId",
                table: "Payments",
                column: "RecordedByUserId");
        }
    }
}
