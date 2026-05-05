using Microsoft.EntityFrameworkCore;
using iVault.Api.Models;

namespace iVault.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        
        public DbSet<RecordDefinition> RecordDefinitions { get; set; }
        public DbSet<Record> Records { get; set; }
        public DbSet<Content> Contents { get; set;   }

        // This tells the code that the "users" table in Postgres 
        // maps to our "User" model in C#
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ensure our Dictionary maps to PostgreSQL JSONB
            modelBuilder.Entity<Record>()
                .Property(b => b.Metadata)
                .HasColumnType("jsonb");

            modelBuilder.Entity<RecordDefinition>()
                .Property(b => b.FieldSchema)
                .HasColumnType("jsonb");

            modelBuilder.Entity<User>().ToTable("users");

            // Add this line to fix the "Id" vs "id" error
            modelBuilder.Entity<User>()
                .Property(u => u.Id)
                .HasColumnName("id");

            // It's good practice to do the same for the others since Postgres 
            // prefers lowercase
            modelBuilder.Entity<User>().Property(u => u.Username).HasColumnName("username");
            //modelBuilder.Entity<User>().Property(u => u.PasswordHash).HasColumnName("password_hash");
            modelBuilder.Entity<User>().Property(u => u.PasswordHash).HasColumnName("password");
        }
    }
}