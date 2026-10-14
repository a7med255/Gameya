using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gameya.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssociationEditorIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_People_NormalizedFullName",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_People_PhoneNumber",
                table: "People");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhoneNumber",
                table: "People",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "OwnerPersonAccess",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "OwnerPersonAccess",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Associations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "AssociationMemberships",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE [People]
                SET [NormalizedPhoneNumber] = REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([PhoneNumber])), ' ', ''), CHAR(9), ''), CHAR(160), '');

                UPDATE [Associations]
                SET [NormalizedName] = UPPER(LTRIM(RTRIM([Name])));
                WHILE EXISTS (SELECT 1 FROM [Associations] WHERE [NormalizedName] LIKE '%  %')
                    UPDATE [Associations] SET [NormalizedName] = REPLACE([NormalizedName], '  ', ' ')
                    WHERE [NormalizedName] LIKE '%  %';

                INSERT INTO [OwnerPersonAccess] ([OwnerId], [PersonId], [NormalizedName], [NormalizedPhone], [GrantedAtUtc])
                SELECT DISTINCT a.[OwnerId], m.[PersonId], p.[NormalizedFullName], p.[NormalizedPhoneNumber], a.[CreatedAtUtc]
                FROM [AssociationMemberships] m
                INNER JOIN [Associations] a ON a.[Id] = m.[AssociationId]
                INNER JOIN [People] p ON p.[Id] = m.[PersonId]
                WHERE NOT EXISTS (
                    SELECT 1 FROM [OwnerPersonAccess] opa
                    WHERE opa.[OwnerId] = a.[OwnerId] AND opa.[PersonId] = m.[PersonId]);

                UPDATE opa
                SET opa.[NormalizedName] = p.[NormalizedFullName],
                    opa.[NormalizedPhone] = p.[NormalizedPhoneNumber]
                FROM [OwnerPersonAccess] opa
                INNER JOIN [People] p ON p.[Id] = opa.[PersonId];

                ;WITH OrderedMembers AS (
                    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [AssociationId] ORDER BY [JoinedAtUtc], [Id]) - 1 AS [Position]
                    FROM [AssociationMemberships]
                )
                UPDATE m SET [DisplayOrder] = o.[Position]
                FROM [AssociationMemberships] m
                INNER JOIN OrderedMembers o ON o.[Id] = m.[Id];

                -- Preserve legacy duplicate associations while making their owner-scoped names unambiguous.
                -- The oldest association keeps its original name; later duplicates receive the first free numeric suffix.
                DECLARE @DuplicateAssociationId uniqueidentifier;
                DECLARE @DuplicateOwnerId nvarchar(450);
                DECLARE @DuplicateBaseName nvarchar(200);
                DECLARE @DuplicatePosition int;
                DECLARE @DuplicateSuffix int;
                DECLARE @DuplicateCandidate nvarchar(200);
                DECLARE @DuplicateCandidateNormalized nvarchar(200);

                DECLARE AssociationDuplicateCursor CURSOR LOCAL STATIC READ_ONLY FOR
                    SELECT [Id], [OwnerId], [Name], [DuplicatePosition]
                    FROM (
                        SELECT [Id], [OwnerId], [Name],
                            ROW_NUMBER() OVER (
                                PARTITION BY [OwnerId], [NormalizedName]
                                ORDER BY [CreatedAtUtc], [Id]) AS [DuplicatePosition]
                        FROM [Associations]
                    ) ranked
                    WHERE [DuplicatePosition] > 1
                    ORDER BY [OwnerId], [DuplicatePosition], [Id];

                OPEN AssociationDuplicateCursor;
                FETCH NEXT FROM AssociationDuplicateCursor INTO @DuplicateAssociationId, @DuplicateOwnerId,
                    @DuplicateBaseName, @DuplicatePosition;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @DuplicateSuffix = @DuplicatePosition;
                    WHILE 1 = 1
                    BEGIN
                        SET @DuplicateCandidate = CONCAT(LEFT(LTRIM(RTRIM(@DuplicateBaseName)), 180),
                            N' (', CONVERT(nvarchar(10), @DuplicateSuffix), N')');
                        SET @DuplicateCandidateNormalized = UPPER(LTRIM(RTRIM(@DuplicateCandidate)));
                        WHILE CHARINDEX(N'  ', @DuplicateCandidateNormalized) > 0
                            SET @DuplicateCandidateNormalized = REPLACE(@DuplicateCandidateNormalized, N'  ', N' ');

                        IF NOT EXISTS (
                            SELECT 1 FROM [Associations]
                            WHERE [OwnerId] = @DuplicateOwnerId
                                AND [Id] <> @DuplicateAssociationId
                                AND [NormalizedName] = @DuplicateCandidateNormalized)
                            BREAK;
                        SET @DuplicateSuffix += 1;
                    END;

                    UPDATE [Associations]
                    SET [Name] = @DuplicateCandidate,
                        [NormalizedName] = @DuplicateCandidateNormalized
                    WHERE [Id] = @DuplicateAssociationId;

                    FETCH NEXT FROM AssociationDuplicateCursor INTO @DuplicateAssociationId, @DuplicateOwnerId,
                        @DuplicateBaseName, @DuplicatePosition;
                END;
                CLOSE AssociationDuplicateCursor;
                DEALLOCATE AssociationDuplicateCursor;

                IF EXISTS (
                    SELECT 1 FROM [Associations]
                    GROUP BY [OwnerId], [NormalizedName]
                    HAVING COUNT(*) > 1)
                    THROW 51000, 'Automatic association-name disambiguation could not satisfy owner-scoped uniqueness.', 1;

                IF EXISTS (
                    SELECT 1 FROM [OwnerPersonAccess]
                    GROUP BY [OwnerId], [NormalizedName]
                    HAVING COUNT(*) > 1)
                    THROW 51001, 'Duplicate normalized person names exist for an owner. Resolve them before applying this migration.', 1;

                IF EXISTS (
                    SELECT 1 FROM [OwnerPersonAccess]
                    GROUP BY [OwnerId], [NormalizedPhone]
                    HAVING COUNT(*) > 1)
                    THROW 51002, 'Duplicate normalized phone numbers exist for an owner. Resolve them before applying this migration.', 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_People_NormalizedFullName",
                table: "People",
                column: "NormalizedFullName");

            migrationBuilder.CreateIndex(
                name: "IX_People_NormalizedPhoneNumber",
                table: "People",
                column: "NormalizedPhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_OwnerPersonAccess_OwnerId_NormalizedName",
                table: "OwnerPersonAccess",
                columns: new[] { "OwnerId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OwnerPersonAccess_OwnerId_NormalizedPhone",
                table: "OwnerPersonAccess",
                columns: new[] { "OwnerId", "NormalizedPhone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Associations_OwnerId_NormalizedName",
                table: "Associations",
                columns: new[] { "OwnerId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [People] GROUP BY [NormalizedFullName] HAVING COUNT(*) > 1)
                    THROW 51003, 'Cannot restore global person-name uniqueness while cross-owner duplicates exist.', 1;
                IF EXISTS (SELECT 1 FROM [People] GROUP BY [PhoneNumber] HAVING COUNT(*) > 1)
                    THROW 51004, 'Cannot restore global phone uniqueness while cross-owner duplicates exist.', 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_People_NormalizedFullName",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_People_NormalizedPhoneNumber",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_OwnerPersonAccess_OwnerId_NormalizedName",
                table: "OwnerPersonAccess");

            migrationBuilder.DropIndex(
                name: "IX_OwnerPersonAccess_OwnerId_NormalizedPhone",
                table: "OwnerPersonAccess");

            migrationBuilder.DropIndex(
                name: "IX_Associations_OwnerId_NormalizedName",
                table: "Associations");

            migrationBuilder.DropColumn(
                name: "NormalizedPhoneNumber",
                table: "People");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "OwnerPersonAccess");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "OwnerPersonAccess");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Associations");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "AssociationMemberships");

            migrationBuilder.CreateIndex(
                name: "IX_People_NormalizedFullName",
                table: "People",
                column: "NormalizedFullName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_People_PhoneNumber",
                table: "People",
                column: "PhoneNumber",
                unique: true);
        }
    }
}
