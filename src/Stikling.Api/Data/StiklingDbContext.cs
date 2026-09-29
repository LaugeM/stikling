using Microsoft.EntityFrameworkCore;

namespace Stikling.Api.Data;

public class StiklingDbContext(DbContextOptions<StiklingDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<Membership> Memberships => Set<Membership>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Person>(person =>
        {
            person.Property(p => p.ClerkUserId).HasMaxLength(100);
            person.HasIndex(p => p.ClerkUserId).IsUnique();
        });

        model.Entity<Collection>(collection =>
        {
            collection.Property(c => c.Name).HasMaxLength(Collection.MaxNameLength);
        });

        model.Entity<Membership>(membership =>
        {
            membership.HasKey(m => new { m.CollectionId, m.PersonId });
            membership.HasIndex(m => m.PersonId);
            membership.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
            membership.HasOne(m => m.Collection).WithMany(c => c.Members).HasForeignKey(m => m.CollectionId);
            membership.HasOne(m => m.Person).WithMany(p => p.Memberships).HasForeignKey(m => m.PersonId);
        });
    }
}
