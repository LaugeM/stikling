using Stikling.Api.Sharing;
using Stikling.Core.Sharing;

namespace Stikling.Api.Tests;

// A square tile shows the photo's short side across, so that is the width each image is offered at
public class ShareImageTests
{
    private static readonly Guid Id = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void A_portrait_photo_is_offered_at_its_short_side()
    {
        var srcset = ShareImage.SrcSet("/p", new SharePhoto(Id, null, 1200, 1600), hasThumbnail: true);

        Assert.Equal($"/p/photos/{Id}/thumb 270w, /p/photos/{Id}/full 1200w", srcset);
    }

    [Fact]
    public void A_photo_larger_than_the_stored_size_counts_as_resized()
    {
        var srcset = ShareImage.SrcSet("/p", new SharePhoto(Id, null, 4000, 3000), hasThumbnail: true);

        Assert.Equal($"/p/photos/{Id}/thumb 270w, /p/photos/{Id}/full 1200w", srcset);
    }

    [Fact]
    public void A_small_photo_is_not_counted_as_larger_than_it_is()
    {
        var srcset = ShareImage.SrcSet("/p", new SharePhoto(Id, null, 300, 300), hasThumbnail: true);

        Assert.Equal($"/p/photos/{Id}/thumb 300w, /p/photos/{Id}/full 300w", srcset);
    }

    [Fact]
    public void Without_a_thumbnail_only_the_full_photo_is_offered()
    {
        var srcset = ShareImage.SrcSet("/p", new SharePhoto(Id, null), hasThumbnail: false);

        Assert.Equal($"/p/photos/{Id}/full 1200w", srcset);
    }
}
