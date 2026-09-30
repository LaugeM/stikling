using System.Text.Json;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class PhotoFrameTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void A_frame_back_in_the_middle_is_stored_as_none()
    {
        Assert.Null(PhotoFrame.From(0.5, 0.5, 1));
        Assert.Null(PhotoFrame.From(0.5001, 0.4999, 1.0001));
    }

    [Fact]
    public void A_moved_or_zoomed_frame_is_kept()
    {
        Assert.Equal(new PhotoFrame(0.2, 0.5, 1), PhotoFrame.From(0.2, 0.5, 1));
        Assert.Equal(new PhotoFrame(0.5, 0.5, 2), PhotoFrame.From(0.5, 0.5, 2));
    }

    [Fact]
    public void Numbers_out_of_range_are_brought_back_in()
    {
        Assert.Equal(new PhotoFrame(0, 1, PhotoFrame.MaxZoom), PhotoFrame.From(-0.3, 1.7, 9));
        Assert.Equal(new PhotoFrame(0.3, 0.5, 1), PhotoFrame.From(0.3, 0.5, 0.2));
    }

    [Fact]
    public void Numbers_that_are_not_numbers_fall_back_to_the_middle()
    {
        Assert.Equal(new PhotoFrame(0.5, 0.5, 2), PhotoFrame.From(double.NaN, double.PositiveInfinity, 2));
        Assert.Null(PhotoFrame.From(0.5, 0.5, double.NaN));
    }

    [Fact]
    public void Numbers_are_rounded_so_the_record_stays_small()
    {
        Assert.Equal(new PhotoFrame(0.123, 0.988, 1.5), PhotoFrame.From(0.12345, 0.98765, 1.50004));
    }

    [Fact]
    public void A_photo_saved_before_frames_existed_still_reads_as_centred()
    {
        const string stored = """{"id":"6f7c1c9e-3c1b-4b8e-9d1f-2a4b6c8d0e1f","subjectType":"Plant","subjectId":"0b1f5a4e-8c3d-4f2a-9e6b-7d5c3a1b2e4f","takenAt":"2026-09-01T10:00:00+00:00","width":1600,"height":1200,"createdAt":"2026-09-01T10:00:00+00:00","updatedAt":"2026-09-01T10:00:00+00:00"}""";

        var photo = JsonSerializer.Deserialize<Photo>(stored, Json)!;

        Assert.Null(photo.Frame);
        Assert.Equal(1600, photo.Width);
    }

    [Fact]
    public void A_frame_survives_being_stored_and_read_back()
    {
        var photo = new Photo { Id = Guid.NewGuid(), Frame = new PhotoFrame(0.25, 0.75, 1.8) };

        var json = JsonSerializer.Serialize(photo, Json);
        var read = JsonSerializer.Deserialize<Photo>(json, Json)!;

        Assert.Contains("\"frame\":{\"x\":0.25,\"y\":0.75,\"zoom\":1.8}", json);
        Assert.Equal(photo.Frame, read.Frame);
    }
}
