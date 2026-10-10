using Microsoft.EntityFrameworkCore;
using Phonebook.Api.Models.Entities;

namespace Phonebook.Api.Data;

/// <summary>
/// EF Core DbContext for the Phonebook database.
/// Schema matches the Java reference (Flyway V1__create_schema.sql):
/// users and contacts tables with snake_case column names.
/// </summary>
public class PhonebookDbContext : DbContext
{
    public PhonebookDbContext(DbContextOptions<PhonebookDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Contact> Contacts => Set<Contact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // --- Users table ---
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.HashedPassword).HasColumnName("hashed_password").HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.Username).HasDatabaseName("idx_users_username").IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // --- Contacts table ---
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.ToTable("contacts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.PhoneNumber).HasDatabaseName("idx_contacts_phone_number").IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }
}
