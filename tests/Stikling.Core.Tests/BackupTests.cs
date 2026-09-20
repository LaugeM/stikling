using Stikling.Core.Backup;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class BackupTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static Plant Plant(string name, DateTimeOffset updated, Guid? id = null) =>
        new() { Id = id ?? Guid.NewGuid(), Nickname = name, UpdatedAt = updated };

    [Fact]
    public void Plants_only_on_the_backup_are_added()
    {
        var incoming = Plant("Coleus", Now);

        var result = BackupMerge.Merge<Plant>([], [incoming]);

        Assert.Equal([incoming], result.ToSave);
        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public void A_newer_version_in_the_backup_wins()
    {
        var id = Guid.NewGuid();
        var mine = Plant("Coleus", Now.AddDays(-2), id);
        var theirs = Plant("Coleus in the kitchen", Now, id);

        var result = BackupMerge.Merge([mine], [theirs]);

        Assert.Equal([theirs], result.ToSave);
        Assert.Equal(1, result.Updated);
    }

    [Fact]
    public void Newer_changes_on_the_device_are_kept()
    {
        var id = Guid.NewGuid();
        var mine = Plant("Coleus in the kitchen", Now, id);
        var theirs = Plant("Coleus", Now.AddDays(-2), id);

        var result = BackupMerge.Merge([mine], [theirs]);

        Assert.Empty(result.ToSave);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void The_same_plant_twice_in_a_file_keeps_the_newest()
    {
        var id = Guid.NewGuid();
        var older = Plant("Coleus", Now.AddDays(-1), id);
        var newer = Plant("Coleus cutting", Now, id);

        var result = BackupMerge.Merge<Plant>([], [older, newer]);

        Assert.Equal([newer], result.ToSave);
        Assert.Equal(1, result.Added);
    }

    [Fact]
    public void Deleted_plants_in_the_backup_delete_them_here_too()
    {
        var id = Guid.NewGuid();
        var mine = Plant("Coleus", Now.AddDays(-1), id);
        var theirs = Plant("Coleus", Now, id);
        theirs.DeletedAt = Now;

        var result = BackupMerge.Merge([mine], [theirs]);

        var saved = Assert.Single(result.ToSave);
        Assert.True(saved.IsDeleted);
    }

    [Fact]
    public void A_plant_deleted_here_comes_back_from_the_backup()
    {
        var id = Guid.NewGuid();
        var mine = Plant("Coleus", Now, id);
        mine.DeletedAt = Now;
        var theirs = Plant("Coleus", Now.AddDays(-2), id);

        var result = BackupMerge.Merge([mine], [theirs]);

        var saved = Assert.Single(result.ToSave);
        Assert.False(saved.IsDeleted);
        Assert.Equal(1, result.BroughtBack);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void A_plant_deleted_in_both_places_stays_deleted()
    {
        var id = Guid.NewGuid();
        var mine = Plant("Coleus", Now, id);
        mine.DeletedAt = Now;
        var theirs = Plant("Coleus", Now.AddDays(-2), id);
        theirs.DeletedAt = Now.AddDays(-2);

        var result = BackupMerge.Merge([mine], [theirs]);

        Assert.Empty(result.ToSave);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.BroughtBack);
    }

    [Fact]
    public void Counts_describe_what_a_file_holds()
    {
        var gone = Plant("Basil", Now);
        gone.DeletedAt = Now;
        var goneCase = new PestCase { DeletedAt = Now };

        var data = new BackupData
        {
            ExportedAt = Now,
            Plants = [Plant("Coleus", Now), gone],
            Photos = [new Photo(), new Photo()],
            PestCases = [new PestCase(), goneCase],
            PestTreatments = [new PestTreatment()],
            Pots = [new Pot { Name = "Clear nursery pot" }]
        };

        // The deleted plant is in the file, but it isn't something the restore brings back
        Assert.Equal(new BackupCounts(1, 0, 0, 2, 0, 1, 1, 1), data.Counts);
        Assert.Equal(BackupData.CurrentVersion, data.Version);
    }

    [Fact]
    public void A_pest_case_and_its_treatments_survive_a_restore()
    {
        var caseId = Guid.NewGuid();
        var mine = new PestCase { Id = caseId, UpdatedAt = Now.AddDays(-3) };
        var theirs = new PestCase { Id = caseId, UpdatedAt = Now, Status = PestCaseStatus.Resolved };
        var treatment = new PestTreatment { CaseId = caseId, UpdatedAt = Now, What = "Alcohol spray" };

        var cases = BackupMerge.Merge([mine], [theirs]);
        var treatments = BackupMerge.Merge<PestTreatment>([], [treatment]);

        Assert.Equal(PestCaseStatus.Resolved, Assert.Single(cases.ToSave).Status);
        Assert.Equal(caseId, Assert.Single(treatments.ToSave).CaseId);
        Assert.Equal(1, treatments.Added);
    }

    [Fact]
    public void Pots_survive_a_restore()
    {
        var id = Guid.NewGuid();
        var mine = new Pot { Id = id, Name = "Clear nursery pot", Owned = 5, UpdatedAt = Now.AddDays(-3) };
        var theirs = new Pot { Id = id, Name = "Clear nursery pot", Owned = 6, UpdatedAt = Now, TopCm = 13 };

        var merged = BackupMerge.Merge([mine], [theirs]);

        var restored = Assert.Single(merged.ToSave);
        Assert.Equal(6, restored.Owned);
        Assert.Equal(13, restored.TopCm);
        Assert.Equal(1, merged.Updated);
    }
}
