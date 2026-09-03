using App.GitHealth.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.GitHealth.Api.Persistence.Configuration;

internal sealed class AssistantMessageEntityConfiguration
    : IEntityTypeConfiguration<AssistantMessageEntity>
{
    private const int RoleLength = 10;
    private const int StatusLength = 20;
    private const int EffortLength = 20;
    private const int CommandLength = 2000;
    private const int FailureCodeLength = 100;
    private const int FailureMessageLength = 2000;

    public void Configure(EntityTypeBuilder<AssistantMessageEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AssistantMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Role).HasMaxLength(RoleLength).IsRequired();
        builder.Property(message => message.Text).IsRequired();
        builder.Property(message => message.WrittenAtUtc)
            .HasConversion<UtcDateTimeOffsetConverter>();
        ConfigureOptional(builder);
        builder.HasIndex(message => new { message.ConversationId, message.Position }).IsUnique();
    }

    private static void ConfigureOptional(EntityTypeBuilder<AssistantMessageEntity> builder)
    {
        builder.Property(message => message.Status).HasMaxLength(StatusLength);
        builder.Property(message => message.Effort).HasMaxLength(EffortLength);
        builder.Property(message => message.CommandLine).HasMaxLength(CommandLength);
        builder.Property(message => message.FailureCode).HasMaxLength(FailureCodeLength);
        builder.Property(message => message.FailureMessage).HasMaxLength(FailureMessageLength);
    }
}
