using System.Text.Json;
using Stikling.Core.Backup;
using Stikling.Core.Models;
using Stikling.Core.Photos;

namespace Stikling.Core.Tests;

public class PhotoMarkupTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakePhotoMarkupRepository repo = new();
    private readonly PhotoMarkupService service;
    private readonly Guid photo = Guid.NewGuid();

    public PhotoMarkupTests() => service = new PhotoMarkupService(repo);

    [Fact]
    public void A_mark_needs_a_photo_a_shape_and_points_inside_the_photo()
    {
        var ok = new PhotoMarkup { PhotoId = photo, Shape = MarkupShape.Circle, X1 = 0, Y1 = 1, X2 = 0.5, Y2 = 0.5 };
        Assert.Empty(ok.Validate());

        Assert.NotEmpty(new PhotoMarkup { Shape = MarkupShape.Circle }.Validate());
        Assert.NotEmpty(new PhotoMarkup { PhotoId = photo }.Validate());
        Assert.NotEmpty(new PhotoMarkup { PhotoId = photo, Shape = MarkupShape.Arrow, X1 = 1.1 }.Validate());
        Assert.NotEmpty(new PhotoMarkup { PhotoId = photo, Shape = MarkupShape.Arrow, Y2 = -0.1 }.Validate());
        Assert.NotEmpty(new PhotoMarkup { PhotoId = photo, Shape = MarkupShape.Arrow, X2 = double.NaN }.Validate());
        Assert.NotEmpty(new PhotoMarkup { PhotoId = photo, Shape = MarkupShape.Arrow, Y1 = double.PositiveInfinity }.Validate());
        Assert.NotEmpty(new PhotoMarkup { PhotoId = photo, Shape = MarkupShape.Arrow, Label = new string('a', 41) }.Validate());
    }

    [Fact]
    public async Task Positions_are_clamped_and_rounded_when_added()
    {
        var mark = await service.AddAsync(photo, MarkupShape.Arrow, -0.2, 0.123456, 1.5, double.NaN);

        Assert.NotNull(mark);
        Assert.Equal((0, 0.1235, 1, 0.5), (mark.X1, mark.Y1, mark.X2, mark.Y2));
        Assert.Equal(MarkupShape.Arrow, mark.Shape);
    }

    [Fact]
    public async Task A_circle_with_both_points_the_same_and_a_very_short_arrow_are_not_saved()
    {
        Assert.Null(await service.AddAsync(photo, MarkupShape.Circle, 0.3, 0.3, 0.3, 0.3));
        Assert.Null(await service.AddAsync(photo, MarkupShape.Circle, 0.30001, 0.3, 0.3, 0.3));
        Assert.Null(await service.AddAsync(photo, MarkupShape.Arrow, 0.3, 0.3, 0.305, 0.305));
        Assert.Empty(repo.Marks);

        Assert.NotNull(await service.AddAsync(photo, MarkupShape.Arrow, 0.3, 0.3, 0.3, 0.32));
        Assert.NotNull(await service.AddAsync(photo, MarkupShape.Circle, 0.3, 0.3, 0.31, 0.3));
    }

    [Fact]
    public async Task Moving_a_mark_keeps_the_label_and_a_move_that_makes_it_too_small_is_left_out()
    {
        var mark = (await service.AddAsync(photo, MarkupShape.Arrow, 0.2, 0.2, 0.5, 0.5))!;
        await service.SetLabelAsync(mark.Id, "new node");

        var moved = await service.MoveAsync(mark.Id, 0.6, 0.6, 0.9, 1.4);
        Assert.NotNull(moved);
        Assert.Equal((0.6, 0.6, 0.9, 1.0, "new node"), (moved.X1, moved.Y1, moved.X2, moved.Y2, moved.Label));

        Assert.Null(await service.MoveAsync(mark.Id, 0.4, 0.4, 0.4, 0.4));
        Assert.Equal(0.6, repo.Marks[mark.Id].X1);
        Assert.Null(await service.MoveAsync(Guid.NewGuid(), 0.1, 0.1, 0.5, 0.5));
    }

    [Fact]
    public async Task A_label_is_trimmed_cut_to_forty_characters_and_empty_becomes_none()
    {
        var mark = (await service.AddAsync(photo, MarkupShape.Circle, 0.5, 0.5, 0.6, 0.5))!;

        await service.SetLabelAsync(mark.Id, "  mealybugs here  ");
        Assert.Equal("mealybugs here", repo.Marks[mark.Id].Label);

        await service.SetLabelAsync(mark.Id, new string('x', 60));
        Assert.Equal(40, repo.Marks[mark.Id].Label!.Length);

        await service.SetLabelAsync(mark.Id, "   ");
        Assert.Null(repo.Marks[mark.Id].Label);
    }

    [Fact]
    public async Task Removing_a_mark_soft_deletes_it_and_it_is_no_longer_listed()
    {
        var mark = (await service.AddAsync(photo, MarkupShape.Circle, 0.5, 0.5, 0.6, 0.5))!;

        await service.RemoveAsync(mark.Id);

        Assert.NotNull(repo.Marks[mark.Id].DeletedAt);
        Assert.Empty(await service.GetForPhotosAsync([photo]));
        Assert.Null(await service.SetLabelAsync(mark.Id, "late"));
    }

    [Fact]
    public async Task Marks_come_grouped_by_photo_in_the_order_they_were_made()
    {
        var other = Guid.NewGuid();
        var first = (await service.AddAsync(photo, MarkupShape.Circle, 0.5, 0.5, 0.6, 0.5))!;
        var elsewhere = (await service.AddAsync(other, MarkupShape.Arrow, 0.1, 0.1, 0.4, 0.4))!;
        var second = (await service.AddAsync(photo, MarkupShape.Arrow, 0.7, 0.7, 0.9, 0.9))!;
        await service.AddAsync(Guid.NewGuid(), MarkupShape.Circle, 0.5, 0.5, 0.6, 0.5);

        var grouped = await service.GetForPhotosAsync([photo, other]);

        Assert.Equal(2, grouped.Count);
        Assert.Equal([first.Id, second.Id], grouped[photo].Select(m => m.Id));
        Assert.Equal([elsewhere.Id], grouped[other].Select(m => m.Id));
        Assert.Empty(await service.GetForPhotosAsync([]));
    }

    [Fact]
    public async Task A_shape_this_version_does_not_know_reads_as_none_and_is_left_out_and_untouched()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var id = Guid.NewGuid();
        var json = $$"""{"id":"{{id}}","photoId":"{{photo}}","shape":"Squiggle","x1":0.2,"y1":0.2,"x2":0.4,"y2":0.4,"label":"later","createdAt":"2026-11-01T12:00:00+00:00"}""";

        var markup = JsonSerializer.Deserialize<PhotoMarkup>(json, options)!;
        Assert.Null(markup.Shape);
        Assert.Equal("later", markup.Label);
        repo.Marks[markup.Id] = markup;

        Assert.Empty(await service.GetForPhotosAsync([photo]));
        Assert.Null(await service.MoveAsync(id, 0.5, 0.5, 0.9, 0.9));
        Assert.Null(await service.SetLabelAsync(id, "changed"));
        await service.RemoveAsync(id);

        Assert.Equal((0.2, 0.4, "later"), (repo.Marks[id].X1, repo.Marks[id].X2, repo.Marks[id].Label));
        Assert.Null(repo.Marks[id].DeletedAt);
    }

    [Fact]
    public void Marks_survive_a_backup_and_an_unknown_shape_stays_in_the_file()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var circle = new PhotoMarkup { PhotoId = photo, Shape = MarkupShape.Circle, X1 = 0.5, Y1 = 0.4, X2 = 0.6, Y2 = 0.4, Label = "node", UpdatedAt = Now };
        var arrow = new PhotoMarkup { PhotoId = photo, Shape = MarkupShape.Arrow, X1 = 0.1, Y1 = 0.2, X2 = 0.3, Y2 = 0.4, UpdatedAt = Now };

        var json = JsonSerializer.Serialize(new BackupData { PhotoMarkups = [circle, arrow, new PhotoMarkup { DeletedAt = Now }] }, options);
        Assert.Contains("\"photoMarkups\"", json);
        Assert.Contains("\"Circle\"", json);
        var back = JsonSerializer.Deserialize<BackupData>(json, options)!;

        Assert.Equal(3, back.PhotoMarkups.Count);
        var restored = back.PhotoMarkups.Single(m => m.Id == circle.Id);
        Assert.Equal((photo, MarkupShape.Circle, 0.5, 0.4, 0.6, 0.4, "node"),
            (restored.PhotoId, restored.Shape, restored.X1, restored.Y1, restored.X2, restored.Y2, restored.Label));
        Assert.Equal(2, back.Counts.PhotoMarkups);
        Assert.Equal(3, BackupMerge.Merge<PhotoMarkup>([], back.PhotoMarkups).Added);
        Assert.Equal(0, BackupMerge.Merge([circle], [restored]).Added);
    }

    [Fact]
    public void A_tap_makes_a_circle_with_a_radius_of_eight_percent_of_the_shorter_side()
    {
        // 1000 by 500 drawn: the shorter side is 500, so the radius is 40 pixels, 0.04 across
        var (x1, y1, x2, y2) = StandardMarkup.At(MarkupShape.Circle, 0.5, 0.5, 1000, 500);

        Assert.Equal((0.5, 0.5, 0.54, 0.5), (x1, y1, x2, y2));

        // Near the right edge the edge point goes to the left instead
        var (_, _, left, _) = StandardMarkup.At(MarkupShape.Circle, 0.99, 0.5, 1000, 500);
        Assert.Equal(0.95, left, 4);
    }

    [Fact]
    public void A_tap_makes_an_arrow_with_the_tail_up_and_left_or_on_another_diagonal_when_that_leaves_the_photo()
    {
        var (x1, y1, x2, y2) = StandardMarkup.At(MarkupShape.Arrow, 0.5, 0.5, 1000, 1000);
        var along = 0.18 * Math.Sqrt(0.5);
        Assert.Equal((0.5, 0.5, Math.Round(0.5 - along, 4), Math.Round(0.5 - along, 4)), (x1, y1, x2, y2));

        // Too near the top left corner: the tail goes up and to the right, then down and to the left
        var (_, _, tx, ty) = StandardMarkup.At(MarkupShape.Arrow, 0.05, 0.05, 1000, 1000);
        Assert.True(tx > 0.05 && ty > 0.05);

        var (_, _, ex, ey) = StandardMarkup.At(MarkupShape.Arrow, 0.95, 0.02, 1000, 1000);
        Assert.True(ex < 0.95 && ey > 0.02);
        Assert.True(PhotoMarkup.IsTooSmall(MarkupShape.Arrow, 0.5, 0.5, 0.5, 0.505));
        Assert.False(PhotoMarkup.IsTooSmall(MarkupShape.Arrow, 0.5, 0.5, 0.5 - along, 0.5 - along));
    }

    [Fact]
    public void A_mark_moved_with_the_keys_keeps_its_size_at_the_edge()
    {
        Assert.Equal((0.51, 0.5, 0.61, 0.6), StandardMarkup.Shift(0.5, 0.5, 0.6, 0.6, 0.01, 0).Round());

        // One point is at the edge already: nothing moves that way
        Assert.Equal((0.5, 0.5, 1, 0.6), StandardMarkup.Shift(0.5, 0.5, 1, 0.6, 0.01, 0));
        Assert.Equal((0, 0.5, 0.1, 0.6), StandardMarkup.Shift(0.02, 0.5, 0.12, 0.6, -0.05, 0).Round());
    }
}

internal static class MarkupTupleExtensions
{
    public static (double, double, double, double) Round(this (double A, double B, double C, double D) t) =>
        (Math.Round(t.A, 4), Math.Round(t.B, 4), Math.Round(t.C, 4), Math.Round(t.D, 4));
}
