using Microsoft.EntityFrameworkCore;
using Tabby.Api.Models;

namespace Tabby.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Friendship> Friendships => Set<Friendship>();
        public DbSet<Receipt> Receipts => Set<Receipt>();
        public DbSet<ReceiptShare> ReceiptShares => Set<ReceiptShare>();
        public DbSet<ReceiptMessage> ReceiptMessages => Set<ReceiptMessage>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Email is the login identifier, so it must be unique
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // username#tag must be unique as a pair
            modelBuilder.Entity<User>()
                .HasIndex(u => new { u.Username, u.Tag })
                .IsUnique();

            // FullTag is computed, so it isn't a column
            modelBuilder.Entity<User>()
                .Ignore(u => u.FullTag);

            // Friendship has two FKs to User, so we spell out both relationships
            modelBuilder.Entity<Friendship>()
                .HasOne(f => f.Requester)
                .WithMany()
                .HasForeignKey(f => f.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Friendship>()
                .HasOne(f => f.Addressee)
                .WithMany()
                .HasForeignKey(f => f.AddresseeId)
                .OnDelete(DeleteBehavior.Restrict);

            // No sending the same request twice in the same direction
            modelBuilder.Entity<Friendship>()
                .HasIndex(f => new { f.RequesterId, f.AddresseeId })
                .IsUnique();

            // Receipt -> the user who paid
            modelBuilder.Entity<Receipt>()
                .HasOne(r => r.PaidBy)
                .WithMany()
                .HasForeignKey(r => r.PaidById)
                .OnDelete(DeleteBehavior.Restrict);

            // Deleting a receipt deletes its shares and messages
            modelBuilder.Entity<Receipt>()
                .HasMany(r => r.Shares)
                .WithOne(s => s.Receipt)
                .HasForeignKey(s => s.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Receipt>()
                .HasMany(r => r.Messages)
                .WithOne(m => m.Receipt)
                .HasForeignKey(m => m.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            // A person can only appear once per receipt
            modelBuilder.Entity<ReceiptShare>()
                .HasIndex(s => new { s.ReceiptId, s.UserId })
                .IsUnique();

            modelBuilder.Entity<ReceiptShare>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReceiptMessage>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Money precision (SQLite ignores it, Postgres will use it later)
            modelBuilder.Entity<Receipt>()
                .Property(r => r.TotalAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ReceiptShare>()
                .Property(s => s.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Recipient)
                .WithMany()
                .HasForeignKey(n => n.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Actor)
                .WithMany()
                .HasForeignKey(n => n.ActorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Deleting a receipt also deletes its notifications
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Receipt)
                .WithMany()
                .HasForeignKey(n => n.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            // Fast lookup of "my unread notifications"
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.RecipientId, n.IsRead });
            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // We look tokens up by their hash
            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(t => t.TokenHash)
                .IsUnique();

            modelBuilder.Entity<DirectMessage>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DirectMessage>()
                .HasOne(m => m.Recipient)
                .WithMany()
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Loading a conversation filters on the pair, so index it
            modelBuilder.Entity<DirectMessage>()
                .HasIndex(m => new { m.SenderId, m.RecipientId, m.SentAt });

            modelBuilder.Entity<DirectMessage>()
                .HasIndex(m => new { m.RecipientId, m.ReadAt });
        }
    }
}
