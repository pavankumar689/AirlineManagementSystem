using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<RewardPointsLog> RewardPointsLogs => Set<RewardPointsLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // RefreshToken → User relationship
        modelBuilder.Entity<RefreshToken>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(r => r.Token)
            .IsUnique();

        // PasswordResetToken → User relationship
        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(p => p.Token)
            .IsUnique();

        // RewardPointsLog → User
        modelBuilder.Entity<RewardPointsLog>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed SuperAdmin — only one exists in the system
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = 1,
            FullName = "Super Admin",
            Email = "superadmin@airline.com",
            // Password: SuperAdmin@123
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SuperAdmin@123"),
            Role = "SuperAdmin",
            CreatedAt = new DateTime(2026, 1, 1)
        });
    }
}