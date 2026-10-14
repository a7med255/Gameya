using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gameya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenFinancialRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentAllocations_PaymentId",
                table: "PaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_AssociationCredits_PaymentId",
                table: "AssociationCredits");

            migrationBuilder.AlterColumn<string>(
                name: "GrantedByOwnerId",
                table: "UserAssociationAccess",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "OwnerId",
                table: "PaymentUserInvitations",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_UserAssociationAccess_GrantedByOwnerId",
                table: "UserAssociationAccess",
                column: "GrantedByOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_AssociationId",
                table: "Settlements",
                column: "AssociationId");

            migrationBuilder.CreateIndex(
                name: "IX_Replacements_OldMembershipId",
                table: "Replacements",
                column: "OldMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentUserInvitations_OwnerId",
                table: "PaymentUserInvitations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RecordedByUserId",
                table: "Payments",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PaymentId_AssociationId_PeriodId",
                table: "PaymentAllocations",
                columns: new[] { "PaymentId", "AssociationId", "PeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PeriodId",
                table: "PaymentAllocations",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationAssociations_AssociationId",
                table: "InvitationAssociations",
                column: "AssociationId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationCredits_AssociationId",
                table: "AssociationCredits",
                column: "AssociationId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationCredits_PaymentId_AssociationId",
                table: "AssociationCredits",
                columns: new[] { "PaymentId", "AssociationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssociationCreditApplications_AssociationId",
                table: "AssociationCreditApplications",
                column: "AssociationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssociationCreditApplications_Associations_AssociationId",
                table: "AssociationCreditApplications",
                column: "AssociationId",
                principalTable: "Associations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssociationCreditApplications_People_PersonId",
                table: "AssociationCreditApplications",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssociationCredits_Associations_AssociationId",
                table: "AssociationCredits",
                column: "AssociationId",
                principalTable: "Associations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssociationCredits_People_PersonId",
                table: "AssociationCredits",
                column: "PersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Associations_AspNetUsers_OwnerId",
                table: "Associations",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvitationAssociations_Associations_AssociationId",
                table: "InvitationAssociations",
                column: "AssociationId",
                principalTable: "Associations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAllocations_AssociationPeriods_PeriodId",
                table: "PaymentAllocations",
                column: "PeriodId",
                principalTable: "AssociationPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAllocations_Associations_AssociationId",
                table: "PaymentAllocations",
                column: "AssociationId",
                principalTable: "Associations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_AspNetUsers_RecordedByUserId",
                table: "Payments",
                column: "RecordedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Payments_AdjustsPaymentId",
                table: "Payments",
                column: "AdjustsPaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentUserInvitations_AspNetUsers_OwnerId",
                table: "PaymentUserInvitations",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Replacements_AssociationMemberships_OldMembershipId",
                table: "Replacements",
                column: "OldMembershipId",
                principalTable: "AssociationMemberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_AssociationMemberships_MembershipId",
                table: "Settlements",
                column: "MembershipId",
                principalTable: "AssociationMemberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_Associations_AssociationId",
                table: "Settlements",
                column: "AssociationId",
                principalTable: "Associations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAssociationAccess_AspNetUsers_GrantedByOwnerId",
                table: "UserAssociationAccess",
                column: "GrantedByOwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAssociationAccess_AspNetUsers_UserId",
                table: "UserAssociationAccess",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAssociationAccess_Associations_AssociationId",
                table: "UserAssociationAccess",
                column: "AssociationId",
                principalTable: "Associations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssociationCreditApplications_Associations_AssociationId",
                table: "AssociationCreditApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_AssociationCreditApplications_People_PersonId",
                table: "AssociationCreditApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_AssociationCredits_Associations_AssociationId",
                table: "AssociationCredits");

            migrationBuilder.DropForeignKey(
                name: "FK_AssociationCredits_People_PersonId",
                table: "AssociationCredits");

            migrationBuilder.DropForeignKey(
                name: "FK_Associations_AspNetUsers_OwnerId",
                table: "Associations");

            migrationBuilder.DropForeignKey(
                name: "FK_InvitationAssociations_Associations_AssociationId",
                table: "InvitationAssociations");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAllocations_AssociationPeriods_PeriodId",
                table: "PaymentAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAllocations_Associations_AssociationId",
                table: "PaymentAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_AspNetUsers_RecordedByUserId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Payments_AdjustsPaymentId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentUserInvitations_AspNetUsers_OwnerId",
                table: "PaymentUserInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Replacements_AssociationMemberships_OldMembershipId",
                table: "Replacements");

            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_AssociationMemberships_MembershipId",
                table: "Settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_Associations_AssociationId",
                table: "Settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAssociationAccess_AspNetUsers_GrantedByOwnerId",
                table: "UserAssociationAccess");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAssociationAccess_AspNetUsers_UserId",
                table: "UserAssociationAccess");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAssociationAccess_Associations_AssociationId",
                table: "UserAssociationAccess");

            migrationBuilder.DropIndex(
                name: "IX_UserAssociationAccess_GrantedByOwnerId",
                table: "UserAssociationAccess");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_AssociationId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Replacements_OldMembershipId",
                table: "Replacements");

            migrationBuilder.DropIndex(
                name: "IX_PaymentUserInvitations_OwnerId",
                table: "PaymentUserInvitations");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RecordedByUserId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAllocations_PaymentId_AssociationId_PeriodId",
                table: "PaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAllocations_PeriodId",
                table: "PaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_InvitationAssociations_AssociationId",
                table: "InvitationAssociations");

            migrationBuilder.DropIndex(
                name: "IX_AssociationCredits_AssociationId",
                table: "AssociationCredits");

            migrationBuilder.DropIndex(
                name: "IX_AssociationCredits_PaymentId_AssociationId",
                table: "AssociationCredits");

            migrationBuilder.DropIndex(
                name: "IX_AssociationCreditApplications_AssociationId",
                table: "AssociationCreditApplications");

            migrationBuilder.AlterColumn<string>(
                name: "GrantedByOwnerId",
                table: "UserAssociationAccess",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "OwnerId",
                table: "PaymentUserInvitations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PaymentId",
                table: "PaymentAllocations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationCredits_PaymentId",
                table: "AssociationCredits",
                column: "PaymentId");
        }
    }
}
