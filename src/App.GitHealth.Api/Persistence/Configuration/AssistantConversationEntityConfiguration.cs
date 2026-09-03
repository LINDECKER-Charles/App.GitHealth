using App.GitHealth.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.GitHealth.Api.Persistence.Configuration;

internal sealed class AssistantConversationEntityConfiguration
    : IEntityTypeConfiguration<AssistantConversationEntity>
{
    private const int AgentIdLength = 40;
    private const int AgentNameLength = 100;
    private const int TitleLength = AssistantConversationEntity.MaximumTitleLength;

    public void Configure(EntityTypeBuilder<AssistantConversationEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AssistantConversations");
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Id).ValueGeneratedNever();
        builder.Property(conversation => conversation.AgentId)
            .HasMaxLength(AgentIdLength).IsRequired();
        builder.Property(conversation => conversation.AgentName)
            .HasMaxLength(AgentNameLength).IsRequired();
        builder.Property(conversation => conversation.Title)
            .HasMaxLength(TitleLength).IsRequired();
        builder.Property(conversation => conversation.StartedAtUtc)
            .HasConversion<UtcDateTimeOffsetConverter>();
        builder.Property(conversation => conversation.UpdatedAtUtc)
            .HasConversion<UtcDateTimeOffsetConverter>();
        builder.HasIndex(conversation => conversation.AnalysisRunId);
        builder.HasIndex(conversation => conversation.UpdatedAtUtc);
        builder.HasOne(conversation => conversation.AnalysisRun)
            .WithMany()
            .HasForeignKey(conversation => conversation.AnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(conversation => conversation.Messages)
            .WithOne(message => message.Conversation)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
