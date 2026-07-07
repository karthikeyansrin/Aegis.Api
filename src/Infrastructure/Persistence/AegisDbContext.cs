using Aegis.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Infrastructure.Persistence;

public class AegisDbContext : DbContext
{
    public AegisDbContext(DbContextOptions<AegisDbContext> options) : base(options) { }

    public DbSet<ConversationSession> Conversations => Set<ConversationSession>();
    public DbSet<MessageEntry> Messages => Set<MessageEntry>();
    public DbSet<ExtractedIntelligence> ExtractedIntelligence => Set<ExtractedIntelligence>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConversationSession>(b =>
        {
            b.ToTable("Conversations");
            b.HasKey(x => x.SessionId);
            
            b.HasMany(x => x.History)
             .WithOne(x => x.Conversation)
             .HasForeignKey(x => x.SessionId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.AggregatedIntelligence)
             .WithOne(x => x.Conversation)
             .HasForeignKey<ExtractedIntelligence>(x => x.SessionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MessageEntry>(b =>
        {
            b.ToTable("Messages");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ExtractedIntelligence>(b =>
        {
            b.ToTable("ExtractedIntelligence");
            b.HasKey(x => x.Id);

            b.HasMany(x => x.BankAccounts)
             .WithOne(x => x.ExtractedIntelligence)
             .HasForeignKey(x => x.ExtractedIntelligenceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BankAccount>(b =>
        {
            b.ToTable("BankAccounts");
            b.HasKey(x => x.Id);
        });
    }
}