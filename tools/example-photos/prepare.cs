// Prepares the five images the example family comes with: src/Stikling.Web/wwwroot/example.
//   dotnet run tools/example-photos/prepare.cs -- placeholders   writes five obvious stand-ins
//   dotnet run tools/example-photos/prepare.cs -- <folder>       prepares your own photos from a folder
// Run from the repository root. See README.md next to this file.

// Version 3 on purpose: later versions need a paid licence. The advisories on it are about decoding
// crafted files, and this only reads photos you took yourself.
#:package SixLabors.ImageSharp@3.1.12
#:package SixLabors.ImageSharp.Drawing@2.1.7
#:property PublishAot=false
#:property NoWarn=NU1902;NU1903

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

// The file names are what the app loads, so they match ExampleFamily.Images in Stikling.Core.
(string File, string Title, string Background)[] shots =
[
    ("mother", "Mother plant", "#e6f2ea"),
    ("cutting-day-1", "Cutting, day 1", "#f6e6dc"),
    ("first-roots", "First roots", "#e6f2ea"),
    ("potted-up", "Potted up", "#f6e6dc"),
    ("young-plant", "Young plant", "#e6f2ea")
];
string[] extensions = [".jpg", ".jpeg", ".png", ".webp"];
const int longestSide = 1600;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run tools/example-photos/prepare.cs -- placeholders | <folder>");
    return 1;
}

var output = Path.Combine(FindRepositoryRoot(), "src", "Stikling.Web", "wwwroot", "example");
Directory.CreateDirectory(output);

if (args[0] == "placeholders")
{
    foreach (var shot in shots)
        WritePlaceholder(shot.Title, shot.Background, Path.Combine(output, shot.File + ".jpg"));
}
else
{
    var folder = args[0];
    if (!Directory.Exists(folder))
    {
        Console.Error.WriteLine($"There is no folder {folder}.");
        return 1;
    }

    // Check that all five are there before writing any of them
    var sources = new List<(string File, string Path)>();
    foreach (var shot in shots)
    {
        var found = extensions.Select(e => Path.Combine(folder, shot.File + e)).FirstOrDefault(File.Exists);
        if (found is null)
        {
            Console.Error.WriteLine($"Missing {shot.File} ({string.Join(", ", extensions)}) in {folder}.");
            return 1;
        }
        sources.Add((shot.File, found));
    }

    foreach (var (name, path) in sources)
        Prepare(path, Path.Combine(output, name + ".jpg"));
}

// Read every output back, and fail if any location, camera or date data is left in it
var failed = false;
foreach (var shot in shots)
{
    var path = Path.Combine(output, shot.File + ".jpg");
    var info = Image.Identify(path);
    var left = new List<string>();
    if (info.Metadata.ExifProfile is not null) left.Add("EXIF");
    if (info.Metadata.XmpProfile is not null) left.Add("XMP");
    if (info.Metadata.IptcProfile is not null) left.Add("IPTC");
    if (left.Count > 0)
    {
        Console.Error.WriteLine($"{path} still has {string.Join(", ", left)}.");
        failed = true;
    }
    else
    {
        Console.WriteLine($"{shot.File}.jpg  {info.Width} x {info.Height}  {new FileInfo(path).Length / 1024} KB  clean");
    }
}
return failed ? 1 : 0;

static void Prepare(string source, string target)
{
    using var image = Image.Load(source);
    // Turn the pixels the way the camera meant, since the orientation tag goes with the rest of the EXIF
    image.Mutate(x => x.AutoOrient());
    if (Math.Max(image.Width, image.Height) > longestSide)
    {
        var scale = (double)longestSide / Math.Max(image.Width, image.Height);
        image.Mutate(x => x.Resize((int)Math.Round(image.Width * scale), (int)Math.Round(image.Height * scale)));
    }

    image.Metadata.ExifProfile = null;
    image.Metadata.XmpProfile = null;
    image.Metadata.IptcProfile = null;
    image.SaveAsJpeg(target, new JpegEncoder { Quality = 82 });
}

static void WritePlaceholder(string title, string background, string target)
{
    using var image = new Image<Rgba32>(1200, 1600, Color.ParseHex(background));
    var family = SystemFonts.Collection.TryGet("Arial", out var arial) ? arial
        : SystemFonts.Collection.Families.First();
    var big = family.CreateFont(150, FontStyle.Bold);
    var small = family.CreateFont(90, FontStyle.Regular);
    var ink = Color.ParseHex("#2f3a33");

    void Centered(string text, Font font, float y) =>
        image.Mutate(x => x.DrawText(
            new RichTextOptions(font)
            {
                Origin = new PointF(600, y),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            text, ink));

    Centered("PLACEHOLDER", big, 720);
    Centered(title, small, 900);
    image.Metadata.ExifProfile = null;
    image.SaveAsJpeg(target, new JpegEncoder { Quality = 82 });
}

static string FindRepositoryRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Stikling.slnx")))
            return dir.FullName;
    throw new InvalidOperationException("Run this from inside the repository.");
}
