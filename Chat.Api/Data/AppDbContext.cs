using Chat.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<UserConnection> UserConnections => Set<UserConnection>();
    public DbSet<BlockedIp> BlockedIps => Set<BlockedIp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(50).IsRequired();
            entity.Property(x => x.IpHash).HasMaxLength(255).IsRequired();
            entity.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SenderUsername).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ReceiverUsername).HasMaxLength(50);
            entity.Property(x => x.RoomKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(500).IsRequired();
            entity.Property(x => x.AttachmentKind).HasMaxLength(20);
            entity.Property(x => x.AttachmentUrl).HasMaxLength(500);
            entity.Property(x => x.AttachmentFileName).HasMaxLength(255);
            entity.Property(x => x.AttachmentContentType).HasMaxLength(100);
            entity.Property(x => x.AttachmentStorageProvider).HasMaxLength(50);
            entity.Property(x => x.AttachmentPublicId).HasMaxLength(255);
            entity.Property(x => x.AttachmentResourceType).HasMaxLength(50);
            entity.HasIndex(x => new { x.RoomKey, x.CreatedAt });
            entity.HasIndex(x => x.ReplyToMessageId);
        });

        modelBuilder.Entity<UserConnection>(entity =>
        {
            entity.ToTable("UserConnections");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ConnectionId).HasMaxLength(255).IsRequired();
            entity.HasIndex(x => x.Username);
            entity.HasIndex(x => x.ConnectionId).IsUnique();
        });

        modelBuilder.Entity<BlockedIp>(entity =>
        {
            entity.ToTable("BlockedIps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IpHash).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => x.IpHash);
        });
    }
}
