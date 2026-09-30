using Microsoft.EntityFrameworkCore;

namespace Stikling.Api.Data;

public class StiklingDbContext(DbContextOptions<StiklingDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<SyncedRecord> Records => Set<SyncedRecord>();
    public DbSet<PersonSettings> PersonSettings => Set<PersonSettings>();
    public DbSet<PhotoImage> PhotoImages => Set<PhotoImage>();

    /// <summary>
    /// Locks the collection's row until the end of the transaction, so changes to one collection
    /// take turns. Call it inside a transaction.
    /// </summary>
    public Task LockCollectionAsync(Guid collectionId) =>
        Collections
            .Where(c => c.Id == collectionId)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.LastVersion, c => c.LastVersion));

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Person>(person =>
        {
            // Clerk's ids are case sensitive, and SQL Server compares text without case by default
            person.Property(p => p.ClerkUserId).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
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

        model.Entity<SyncedRecord>(record =>
        {
            // Ids are made on the devices, and a record the app creates on its own has the same
            // fixed id in every collection, so an id is only unique within its collection
            record.HasKey(r => new { r.CollectionId, r.Kind, r.Id });
            record.Property(r => r.Kind).HasMaxLength(30);
            record.HasIndex(r => new { r.CollectionId, r.Version }).IsUnique();
            record.HasOne<Collection>().WithMany().HasForeignKey(r => r.CollectionId);
        });

        model.Entity<PersonSettings>(settings =>
        {
            settings.HasKey(s => s.PersonId);
            settings.HasOne<Person>().WithOne().HasForeignKey<PersonSettings>(s => s.PersonId);
        });

        model.Entity<PhotoImage>(image =>
        {
            image.HasKey(i => new { i.CollectionId, i.PhotoId, i.Size });
            image.Property(i => i.Size).HasConversion<string>().HasMaxLength(20);
            image.HasOne<Collection>().WithMany().HasForeignKey(i => i.CollectionId);
        });
    }
}
