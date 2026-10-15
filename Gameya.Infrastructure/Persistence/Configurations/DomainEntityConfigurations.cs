using Gameya.Domain.Entities;
using Gameya.Domain.ValueObjects;
using Gameya.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gameya.Infrastructure.Persistence.Configurations;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> b)
    {
        b.ToTable("People"); b.HasKey(x => x.Id);
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.NormalizedFullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.NormalizedPhoneNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.SubscriberCode).HasConversion(x => x.Value, x => SubscriberCode.Create(x)).HasMaxLength(8).IsRequired();
        b.HasIndex(x => x.NormalizedPhoneNumber);
        b.HasIndex(x => x.SubscriberCode).IsUnique();
        b.HasIndex(x => x.NormalizedFullName);
        b.Ignore(x => x.DomainEvents);
    }
}

public sealed class AssociationConfiguration : IEntityTypeConfiguration<Association>
{
    public void Configure(EntityTypeBuilder<Association> b)
    {
        b.ToTable("Associations"); b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(200).IsRequired();
        b.Property(x => x.MonthlyContribution).HasPrecision(18, 2);
        b.Property(x => x.OwnerId).HasMaxLength(450).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Ignore(x => x.FullSharePayoutValue); b.Ignore(x => x.DomainEvents);
        b.HasIndex(x => new { x.OwnerId, x.Status });
        b.HasIndex(x => new { x.OwnerId, x.NormalizedName }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Memberships).WithOne().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Periods).WithOne().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Payouts).WithOne().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Replacements).WithOne().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Memberships).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Periods).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Payouts).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Replacements).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class MembershipConfiguration : IEntityTypeConfiguration<AssociationMembership>
{
    public void Configure(EntityTypeBuilder<AssociationMembership> b)
    {
        b.ToTable("AssociationMemberships"); b.HasKey(x => x.Id);
        b.Property(x => x.ShareCount).HasPrecision(9, 2);
        b.HasIndex(x => new { x.AssociationId, x.PersonId });
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.DomainEvents);
    }
}

public sealed class PeriodConfiguration : IEntityTypeConfiguration<AssociationPeriod>
{
    public void Configure(EntityTypeBuilder<AssociationPeriod> b)
    {
        b.ToTable("AssociationPeriods"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.AssociationId, x.PeriodNumber }).IsUnique();
        b.HasIndex(x => x.DueDate); b.Ignore(x => x.DomainEvents);
    }
}

public sealed class PayoutConfiguration : IEntityTypeConfiguration<PayoutAssignment>
{
    public void Configure(EntityTypeBuilder<PayoutAssignment> b)
    {
        b.ToTable("PayoutAssignments"); b.HasKey(x => x.Id);
        b.Property(x => x.ShareCount).HasPrecision(9, 2); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.AssociationId, x.PeriodId });
        b.HasOne<AssociationPeriod>().WithMany().HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssociationMembership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.BeneficiaryPersonId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Ignore(x => x.DomainEvents);
    }
}

public sealed class ReplacementConfiguration : IEntityTypeConfiguration<Replacement>
{
    public void Configure(EntityTypeBuilder<Replacement> b)
    {
        b.ToTable("Replacements"); b.HasKey(x => x.Id); b.Property(x => x.Reason).HasMaxLength(500);
        b.HasOne<AssociationMembership>().WithMany().HasForeignKey(x => x.OldMembershipId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Shares).WithOne().HasForeignKey(x => x.ReplacementId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Shares).UsePropertyAccessMode(PropertyAccessMode.Field); b.Ignore(x => x.DomainEvents);
    }
}
public sealed class ReplacementShareConfiguration : IEntityTypeConfiguration<ReplacementShare>
{
    public void Configure(EntityTypeBuilder<ReplacementShare> b)
    {
        b.ToTable("ReplacementShares"); b.HasKey(x => x.Id); b.Property(x => x.ShareCount).HasPrecision(9, 2); b.Ignore(x => x.DomainEvents);
        b.HasOne<AssociationMembership>().WithMany().HasForeignKey(x => x.NewMembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments"); b.HasKey(x => x.Id); b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.RecordedByUserId).HasMaxLength(450).IsRequired(); b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100);
        b.HasIndex(x => new { x.PersonId, x.PaidAtUtc }); b.HasIndex(x => x.AdjustsPaymentId);
        b.HasIndex(x => new { x.RecordedByUserId, x.IdempotencyKey }).IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Payment>().WithMany().HasForeignKey(x => x.AdjustsPaymentId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field); b.Ignore(x => x.DomainEvents);
    }
}
public sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> b)
    {
        b.ToTable("PaymentAllocations"); b.HasKey(x => x.Id); b.Property(x => x.AllocatedAmount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.AssociationId, x.PeriodId });
        b.HasIndex(x => new { x.PaymentId, x.AssociationId, x.PeriodId }).IsUnique();
        b.HasOne<Association>().WithMany().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssociationPeriod>().WithMany().HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.DomainEvents);
    }
}
public sealed class CreditConfiguration : IEntityTypeConfiguration<AssociationCredit>
{
    public void Configure(EntityTypeBuilder<AssociationCredit> b)
    {
        b.ToTable("AssociationCredits"); b.HasKey(x => x.Id); b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.PersonId, x.AssociationId });
        b.HasIndex(x => new { x.PaymentId, x.AssociationId }).IsUnique(); b.Ignore(x => x.DomainEvents);
        b.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Association>().WithMany().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CreditApplicationConfiguration : IEntityTypeConfiguration<AssociationCreditApplication>
{
    public void Configure(EntityTypeBuilder<AssociationCreditApplication> b)
    {
        b.ToTable("AssociationCreditApplications"); b.HasKey(x => x.Id); b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.PersonId, x.AssociationId, x.PeriodId }); b.Ignore(x => x.DomainEvents);
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Association>().WithMany().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssociationPeriod>().WithMany().HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> b)
    {
        b.ToTable("Settlements"); b.HasKey(x => x.Id);
        b.Property(x => x.TotalRequired).HasPrecision(18, 2); b.Property(x => x.TotalPaid).HasPrecision(18, 2); b.Property(x => x.Balance).HasPrecision(18, 2);
        b.HasIndex(x => x.MembershipId).IsUnique(); b.Ignore(x => x.DomainEvents);
        b.HasOne<Association>().WithMany().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssociationMembership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs"); b.HasKey(x => x.Id); b.Property(x => x.Action).HasMaxLength(100); b.Property(x => x.EntityName).HasMaxLength(100);
        b.Property(x => x.EntityId).HasMaxLength(450); b.Property(x => x.IpAddress).HasMaxLength(64); b.Ignore(x => x.DomainEvents);
        b.HasIndex(x => new { x.EntityName, x.EntityId }); b.HasIndex(x => x.TimestampUtc);
    }
}

public sealed class IdentitySupportConfiguration : IEntityTypeConfiguration<RefreshToken>,
    IEntityTypeConfiguration<PaymentUserInvitation>, IEntityTypeConfiguration<InvitationAssociation>, IEntityTypeConfiguration<UserAssociationAccess>,
    IEntityTypeConfiguration<OwnerPersonAccess>, IEntityTypeConfiguration<BackgroundJobExecution>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens"); b.HasKey(x => x.Id); b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => x.UserId);
    }
    public void Configure(EntityTypeBuilder<PaymentUserInvitation> b)
    {
        b.ToTable("PaymentUserInvitations"); b.HasKey(x => x.Id); b.Property(x => x.Email).HasMaxLength(256); b.Property(x => x.TokenHash).HasMaxLength(64);
        b.Property(x => x.RevokedByUserId).HasMaxLength(450);
        b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc });
        b.HasMany(x => x.Associations).WithOne().HasForeignKey(x => x.InvitationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<InvitationAssociation> b)
    {
        b.ToTable("InvitationAssociations"); b.HasKey(x => new { x.InvitationId, x.AssociationId });
        b.HasOne<Association>().WithMany().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<UserAssociationAccess> b)
    {
        b.ToTable("UserAssociationAccess"); b.HasKey(x => new { x.UserId, x.AssociationId }); b.HasIndex(x => x.AssociationId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.GrantedByOwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Association>().WithMany().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<OwnerPersonAccess> b)
    {
        b.ToTable("OwnerPersonAccess"); b.HasKey(x => new { x.OwnerId, x.PersonId }); b.HasIndex(x => x.PersonId);
        b.Property(x => x.NormalizedName).HasMaxLength(200).IsRequired();
        b.Property(x => x.NormalizedPhone).HasMaxLength(30).IsRequired();
        b.HasIndex(x => new { x.OwnerId, x.NormalizedName }).IsUnique();
        b.HasIndex(x => new { x.OwnerId, x.NormalizedPhone }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<BackgroundJobExecution> b)
    {
        b.ToTable("BackgroundJobExecutions"); b.HasKey(x => x.Id); b.Property(x => x.ExecutionKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasMaxLength(30).IsRequired(); b.Property(x => x.LastError).HasMaxLength(2000);
        b.Property(x => x.RowVersion).IsRowVersion(); b.HasIndex(x => x.ExecutionKey).IsUnique();
    }
}
