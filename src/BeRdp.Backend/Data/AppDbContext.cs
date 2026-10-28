using BeRdp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace BeRdp.Backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<UserMachine> UserMachines => Set<UserMachine>();
    public DbSet<MachineAdGroupEntitlement> MachineAdGroupEntitlements => Set<MachineAdGroupEntitlement>();
    public DbSet<AdminAccessGroup> AdminAccessGroups => Set<AdminAccessGroup>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(256).IsRequired();
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.Property(x => x.SecurityStamp).HasMaxLength(32).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DomainName).HasMaxLength(128);
            entity.Property(x => x.UserPrincipalName).HasMaxLength(256);
        });

        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasIndex(x => x.IpAddress).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
            entity.Property(x => x.RdpHostName).HasMaxLength(253);
        });

        modelBuilder.Entity<UserMachine>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.MachineId }).IsUnique();

            entity.HasOne(x => x.User)
                .WithMany(u => u.UserMachines)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Machine)
                .WithMany(m => m.UserMachines)
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MachineAdGroupEntitlement>(entity =>
        {
            entity.HasIndex(x => new { x.MachineId, x.GroupName, x.DomainName }).IsUnique();
            entity.Property(x => x.GroupName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DomainName).HasMaxLength(128);

            entity.HasOne(x => x.Machine)
                .WithMany(x => x.AdGroupEntitlements)
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminAccessGroup>(entity =>
        {
            entity.HasIndex(x => new { x.GroupName, x.DomainName }).IsUnique();
            entity.Property(x => x.GroupName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DomainName).HasMaxLength(128);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(128);
            entity.Property(x => x.Value).HasMaxLength(2048).IsRequired();
        });
    }
}
