using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gameya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                table: "AssociationCredits",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "AssociationCreditApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssociationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociationCreditApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssociationCreditApplications_AssociationPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "AssociationPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssociationCredits_PaymentId",
                table: "AssociationCredits",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationCreditApplications_PeriodId",
                table: "AssociationCreditApplications",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationCreditApplications_PersonId_AssociationId_PeriodId",
                table: "AssociationCreditApplications",
                columns: new[] { "PersonId", "AssociationId", "PeriodId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AssociationCredits_Payments_PaymentId",
                table: "AssociationCredits",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssociationCredits_Payments_PaymentId",
                table: "AssociationCredits");

            migrationBuilder.DropTable(
                name: "AssociationCreditApplications");

            migrationBuilder.DropIndex(
                name: "IX_AssociationCredits_PaymentId",
                table: "AssociationCredits");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "AssociationCredits");
        }
    }
}
