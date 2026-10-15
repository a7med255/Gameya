IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [DisplayName] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [AssociationCredits] (
        [Id] uniqueidentifier NOT NULL,
        [PersonId] uniqueidentifier NOT NULL,
        [AssociationId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AssociationCredits] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [Associations] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [StartDate] date NOT NULL,
        [NumberOfMonths] int NOT NULL,
        [MonthlyContribution] decimal(18,2) NOT NULL,
        [EndDate] date NULL,
        [Status] int NOT NULL,
        [OwnerId] nvarchar(450) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Associations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(max) NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityName] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(450) NOT NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [TimestampUtc] datetimeoffset NOT NULL,
        [IpAddress] nvarchar(64) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentUserInvitations] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] nvarchar(max) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        [AcceptedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_PaymentUserInvitations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [People] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [NormalizedFullName] nvarchar(200) NOT NULL,
        [PhoneNumber] nvarchar(30) NOT NULL,
        [SubscriberCode] nvarchar(8) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_People] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NULL,
        [PersonId] uniqueidentifier NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        [RevokedAtUtc] datetimeoffset NULL,
        [ReplacedByTokenHash] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [Settlements] (
        [Id] uniqueidentifier NOT NULL,
        [AssociationId] uniqueidentifier NOT NULL,
        [MembershipId] uniqueidentifier NOT NULL,
        [TotalRequired] decimal(18,2) NOT NULL,
        [TotalPaid] decimal(18,2) NOT NULL,
        [Balance] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_Settlements] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [UserAssociationAccess] (
        [UserId] nvarchar(450) NOT NULL,
        [AssociationId] uniqueidentifier NOT NULL,
        [GrantedByOwnerId] nvarchar(max) NOT NULL,
        [GrantedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_UserAssociationAccess] PRIMARY KEY ([UserId], [AssociationId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [AssociationPeriods] (
        [Id] uniqueidentifier NOT NULL,
        [AssociationId] uniqueidentifier NOT NULL,
        [PeriodNumber] int NOT NULL,
        [PeriodDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_AssociationPeriods] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssociationPeriods_Associations_AssociationId] FOREIGN KEY ([AssociationId]) REFERENCES [Associations] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [Replacements] (
        [Id] uniqueidentifier NOT NULL,
        [AssociationId] uniqueidentifier NOT NULL,
        [OldMembershipId] uniqueidentifier NOT NULL,
        [Reason] nvarchar(500) NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Replacements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Replacements_Associations_AssociationId] FOREIGN KEY ([AssociationId]) REFERENCES [Associations] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [InvitationAssociations] (
        [InvitationId] uniqueidentifier NOT NULL,
        [AssociationId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_InvitationAssociations] PRIMARY KEY ([InvitationId], [AssociationId]),
        CONSTRAINT [FK_InvitationAssociations_PaymentUserInvitations_InvitationId] FOREIGN KEY ([InvitationId]) REFERENCES [PaymentUserInvitations] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [AssociationMemberships] (
        [Id] uniqueidentifier NOT NULL,
        [AssociationId] uniqueidentifier NOT NULL,
        [PersonId] uniqueidentifier NOT NULL,
        [ShareCount] decimal(9,2) NOT NULL,
        [Kind] int NOT NULL,
        [Status] int NOT NULL,
        [JoinedAtUtc] datetimeoffset NOT NULL,
        [LeftAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_AssociationMemberships] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssociationMemberships_Associations_AssociationId] FOREIGN KEY ([AssociationId]) REFERENCES [Associations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AssociationMemberships_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [People] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [PersonId] uniqueidentifier NOT NULL,
        [RecordedByUserId] nvarchar(450) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Method] int NOT NULL,
        [PaidAtUtc] datetimeoffset NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [EntryType] int NOT NULL,
        [AdjustsPaymentId] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [People] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [PayoutAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [AssociationId] uniqueidentifier NOT NULL,
        [PeriodId] uniqueidentifier NOT NULL,
        [RecipientType] int NOT NULL,
        [MembershipId] uniqueidentifier NULL,
        [BeneficiaryPersonId] uniqueidentifier NULL,
        [ShareCount] decimal(9,2) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PayoutAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayoutAssignments_AssociationMemberships_MembershipId] FOREIGN KEY ([MembershipId]) REFERENCES [AssociationMemberships] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayoutAssignments_AssociationPeriods_PeriodId] FOREIGN KEY ([PeriodId]) REFERENCES [AssociationPeriods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayoutAssignments_Associations_AssociationId] FOREIGN KEY ([AssociationId]) REFERENCES [Associations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayoutAssignments_People_BeneficiaryPersonId] FOREIGN KEY ([BeneficiaryPersonId]) REFERENCES [People] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [ReplacementShares] (
        [Id] uniqueidentifier NOT NULL,
        [ReplacementId] uniqueidentifier NOT NULL,
        [NewMembershipId] uniqueidentifier NOT NULL,
        [ShareCount] decimal(9,2) NOT NULL,
        CONSTRAINT [PK_ReplacementShares] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReplacementShares_AssociationMemberships_NewMembershipId] FOREIGN KEY ([NewMembershipId]) REFERENCES [AssociationMemberships] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReplacementShares_Replacements_ReplacementId] FOREIGN KEY ([ReplacementId]) REFERENCES [Replacements] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentAllocations] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NOT NULL,
        [AssociationId] uniqueidentifier NOT NULL,
        [PeriodId] uniqueidentifier NOT NULL,
        [AllocatedAmount] decimal(18,2) NOT NULL,
        [DueDate] date NOT NULL,
        [LateDays] int NOT NULL,
        CONSTRAINT [PK_PaymentAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentAllocations_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AssociationCredits_PersonId_AssociationId] ON [AssociationCredits] ([PersonId], [AssociationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AssociationMemberships_AssociationId_PersonId] ON [AssociationMemberships] ([AssociationId], [PersonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AssociationMemberships_PersonId] ON [AssociationMemberships] ([PersonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AssociationPeriods_AssociationId_PeriodNumber] ON [AssociationPeriods] ([AssociationId], [PeriodNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AssociationPeriods_DueDate] ON [AssociationPeriods] ([DueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Associations_OwnerId_Status] ON [Associations] ([OwnerId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityName_EntityId] ON [AuditLogs] ([EntityName], [EntityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TimestampUtc] ON [AuditLogs] ([TimestampUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_AssociationId_PeriodId] ON [PaymentAllocations] ([AssociationId], [PeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_PaymentId] ON [PaymentAllocations] ([PaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_AdjustsPaymentId] ON [Payments] ([AdjustsPaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_PersonId_PaidAtUtc] ON [Payments] ([PersonId], [PaidAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentUserInvitations_TokenHash] ON [PaymentUserInvitations] ([TokenHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PayoutAssignments_AssociationId_PeriodId] ON [PayoutAssignments] ([AssociationId], [PeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PayoutAssignments_BeneficiaryPersonId] ON [PayoutAssignments] ([BeneficiaryPersonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PayoutAssignments_MembershipId] ON [PayoutAssignments] ([MembershipId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PayoutAssignments_PeriodId] ON [PayoutAssignments] ([PeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_People_NormalizedFullName] ON [People] ([NormalizedFullName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_People_PhoneNumber] ON [People] ([PhoneNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_People_SubscriberCode] ON [People] ([SubscriberCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Replacements_AssociationId] ON [Replacements] ([AssociationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReplacementShares_NewMembershipId] ON [ReplacementShares] ([NewMembershipId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReplacementShares_ReplacementId] ON [ReplacementShares] ([ReplacementId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Settlements_MembershipId] ON [Settlements] ([MembershipId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserAssociationAccess_AssociationId] ON [UserAssociationAccess] ([AssociationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233030_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013233030_InitialCreate', N'8.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233448_AddCreditApplications'
)
BEGIN
    ALTER TABLE [AssociationCredits] ADD [PaymentId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233448_AddCreditApplications'
)
BEGIN
    CREATE TABLE [AssociationCreditApplications] (
        [Id] uniqueidentifier NOT NULL,
        [PersonId] uniqueidentifier NOT NULL,
        [AssociationId] uniqueidentifier NOT NULL,
        [PeriodId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [AppliedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AssociationCreditApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssociationCreditApplications_AssociationPeriods_PeriodId] FOREIGN KEY ([PeriodId]) REFERENCES [AssociationPeriods] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233448_AddCreditApplications'
)
BEGIN
    CREATE INDEX [IX_AssociationCredits_PaymentId] ON [AssociationCredits] ([PaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233448_AddCreditApplications'
)
BEGIN
    CREATE INDEX [IX_AssociationCreditApplications_PeriodId] ON [AssociationCreditApplications] ([PeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233448_AddCreditApplications'
)
BEGIN
    CREATE INDEX [IX_AssociationCreditApplications_PersonId_AssociationId_PeriodId] ON [AssociationCreditApplications] ([PersonId], [AssociationId], [PeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233448_AddCreditApplications'
)
BEGIN
    ALTER TABLE [AssociationCredits] ADD CONSTRAINT [FK_AssociationCredits_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233448_AddCreditApplications'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013233448_AddCreditApplications', N'8.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233514_AddIdempotentReportJobs'
)
BEGIN
    CREATE TABLE [BackgroundJobExecutions] (
        [Id] uniqueidentifier NOT NULL,
        [ExecutionKey] nvarchar(200) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [StartedAtUtc] datetimeoffset NOT NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [LastError] nvarchar(2000) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BackgroundJobExecutions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233514_AddIdempotentReportJobs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BackgroundJobExecutions_ExecutionKey] ON [BackgroundJobExecutions] ([ExecutionKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013233514_AddIdempotentReportJobs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013233514_AddIdempotentReportJobs', N'8.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    DROP INDEX [IX_PaymentAllocations_PaymentId] ON [PaymentAllocations];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    DROP INDEX [IX_AssociationCredits_PaymentId] ON [AssociationCredits];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserAssociationAccess]') AND [c].[name] = N'GrantedByOwnerId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [UserAssociationAccess] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [UserAssociationAccess] ALTER COLUMN [GrantedByOwnerId] nvarchar(450) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PaymentUserInvitations]') AND [c].[name] = N'OwnerId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [PaymentUserInvitations] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [PaymentUserInvitations] ALTER COLUMN [OwnerId] nvarchar(450) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    CREATE INDEX [IX_UserAssociationAccess_GrantedByOwnerId] ON [UserAssociationAccess] ([GrantedByOwnerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    CREATE INDEX [IX_Settlements_AssociationId] ON [Settlements] ([AssociationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    CREATE INDEX [IX_Replacements_OldMembershipId] ON [Replacements] ([OldMembershipId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    CREATE INDEX [IX_PaymentUserInvitations_OwnerId] ON [PaymentUserInvitations] ([OwnerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    CREATE INDEX [IX_Payments_RecordedByUserId] ON [Payments] ([RecordedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentAllocations_PaymentId_AssociationId_PeriodId] ON [PaymentAllocations] ([PaymentId], [AssociationId], [PeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_PeriodId] ON [PaymentAllocations] ([PeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    CREATE INDEX [IX_InvitationAssociations_AssociationId] ON [InvitationAssociations] ([AssociationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    CREATE INDEX [IX_AssociationCredits_AssociationId] ON [AssociationCredits] ([AssociationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AssociationCredits_PaymentId_AssociationId] ON [AssociationCredits] ([PaymentId], [AssociationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    CREATE INDEX [IX_AssociationCreditApplications_AssociationId] ON [AssociationCreditApplications] ([AssociationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [AssociationCreditApplications] ADD CONSTRAINT [FK_AssociationCreditApplications_Associations_AssociationId] FOREIGN KEY ([AssociationId]) REFERENCES [Associations] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [AssociationCreditApplications] ADD CONSTRAINT [FK_AssociationCreditApplications_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [People] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [AssociationCredits] ADD CONSTRAINT [FK_AssociationCredits_Associations_AssociationId] FOREIGN KEY ([AssociationId]) REFERENCES [Associations] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [AssociationCredits] ADD CONSTRAINT [FK_AssociationCredits_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [People] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [Associations] ADD CONSTRAINT [FK_Associations_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [InvitationAssociations] ADD CONSTRAINT [FK_InvitationAssociations_Associations_AssociationId] FOREIGN KEY ([AssociationId]) REFERENCES [Associations] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [PaymentAllocations] ADD CONSTRAINT [FK_PaymentAllocations_AssociationPeriods_PeriodId] FOREIGN KEY ([PeriodId]) REFERENCES [AssociationPeriods] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [PaymentAllocations] ADD CONSTRAINT [FK_PaymentAllocations_Associations_AssociationId] FOREIGN KEY ([AssociationId]) REFERENCES [Associations] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_AspNetUsers_RecordedByUserId] FOREIGN KEY ([RecordedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_Payments_AdjustsPaymentId] FOREIGN KEY ([AdjustsPaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [PaymentUserInvitations] ADD CONSTRAINT [FK_PaymentUserInvitations_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [Replacements] ADD CONSTRAINT [FK_Replacements_AssociationMemberships_OldMembershipId] FOREIGN KEY ([OldMembershipId]) REFERENCES [AssociationMemberships] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [Settlements] ADD CONSTRAINT [FK_Settlements_AssociationMemberships_MembershipId] FOREIGN KEY ([MembershipId]) REFERENCES [AssociationMemberships] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [Settlements] ADD CONSTRAINT [FK_Settlements_Associations_AssociationId] FOREIGN KEY ([AssociationId]) REFERENCES [Associations] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [UserAssociationAccess] ADD CONSTRAINT [FK_UserAssociationAccess_AspNetUsers_GrantedByOwnerId] FOREIGN KEY ([GrantedByOwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [UserAssociationAccess] ADD CONSTRAINT [FK_UserAssociationAccess_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    ALTER TABLE [UserAssociationAccess] ADD CONSTRAINT [FK_UserAssociationAccess_Associations_AssociationId] FOREIGN KEY ([AssociationId]) REFERENCES [Associations] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014090722_HardenFinancialRelationships'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014090722_HardenFinancialRelationships', N'8.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014091031_AddOwnerPersonAccess'
)
BEGIN
    CREATE TABLE [OwnerPersonAccess] (
        [OwnerId] nvarchar(450) NOT NULL,
        [PersonId] uniqueidentifier NOT NULL,
        [GrantedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_OwnerPersonAccess] PRIMARY KEY ([OwnerId], [PersonId]),
        CONSTRAINT [FK_OwnerPersonAccess_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OwnerPersonAccess_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [People] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014091031_AddOwnerPersonAccess'
)
BEGIN
    CREATE INDEX [IX_OwnerPersonAccess_PersonId] ON [OwnerPersonAccess] ([PersonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014091031_AddOwnerPersonAccess'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014091031_AddOwnerPersonAccess', N'8.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    DROP INDEX [IX_People_NormalizedFullName] ON [People];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    DROP INDEX [IX_People_PhoneNumber] ON [People];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    ALTER TABLE [People] ADD [NormalizedPhoneNumber] nvarchar(30) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    ALTER TABLE [OwnerPersonAccess] ADD [NormalizedName] nvarchar(200) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    ALTER TABLE [OwnerPersonAccess] ADD [NormalizedPhone] nvarchar(30) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    ALTER TABLE [Associations] ADD [NormalizedName] nvarchar(200) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    ALTER TABLE [AssociationMemberships] ADD [DisplayOrder] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    CREATE INDEX [IX_People_NormalizedFullName] ON [People] ([NormalizedFullName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    CREATE INDEX [IX_People_NormalizedPhoneNumber] ON [People] ([NormalizedPhoneNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OwnerPersonAccess_OwnerId_NormalizedName] ON [OwnerPersonAccess] ([OwnerId], [NormalizedName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OwnerPersonAccess_OwnerId_NormalizedPhone] ON [OwnerPersonAccess] ([OwnerId], [NormalizedPhone]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Associations_OwnerId_NormalizedName] ON [Associations] ([OwnerId], [NormalizedName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014203630_AssociationEditorIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014203630_AssociationEditorIntegrity', N'8.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015190208_DisableHangfireAndHardenPaymentsInvitations'
)
BEGIN
    DROP INDEX [IX_PaymentUserInvitations_OwnerId] ON [PaymentUserInvitations];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015190208_DisableHangfireAndHardenPaymentsInvitations'
)
BEGIN
    DROP INDEX [IX_Payments_RecordedByUserId] ON [Payments];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015190208_DisableHangfireAndHardenPaymentsInvitations'
)
BEGIN
    ALTER TABLE [PaymentUserInvitations] ADD [CreatedAtUtc] datetimeoffset NOT NULL DEFAULT (SYSUTCDATETIME());
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015190208_DisableHangfireAndHardenPaymentsInvitations'
)
BEGIN
    ALTER TABLE [PaymentUserInvitations] ADD [RevokedAtUtc] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015190208_DisableHangfireAndHardenPaymentsInvitations'
)
BEGIN
    ALTER TABLE [PaymentUserInvitations] ADD [RevokedByUserId] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015190208_DisableHangfireAndHardenPaymentsInvitations'
)
BEGIN
    ALTER TABLE [Payments] ADD [IdempotencyKey] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015190208_DisableHangfireAndHardenPaymentsInvitations'
)
BEGIN
    CREATE INDEX [IX_PaymentUserInvitations_OwnerId_CreatedAtUtc] ON [PaymentUserInvitations] ([OwnerId], [CreatedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015190208_DisableHangfireAndHardenPaymentsInvitations'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_RecordedByUserId_IdempotencyKey] ON [Payments] ([RecordedByUserId], [IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015190208_DisableHangfireAndHardenPaymentsInvitations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015190208_DisableHangfireAndHardenPaymentsInvitations', N'8.0.14');
END;
GO

COMMIT;
GO

