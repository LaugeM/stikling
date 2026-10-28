# Example photos

The five images in `src/Stikling.Web/wwwroot/example` are the photos of the example family that Today can load for someone with an empty app. They are only downloaded when someone taps the button, and they are not part of the offline cache.

The shots, by file name:

- `mother.jpg`: the mother plant
- `cutting-day-1.jpg`: the cutting on the day it was put in water
- `first-roots.jpg`: the same cutting when the first root showed
- `potted-up.jpg`: the young plant on the day it was potted up
- `young-plant.jpg`: the young plant a few weeks later

Until there are real photos these are placeholders with the shot's name on them. To write them again, from the repository root:

```bash
dotnet run tools/example-photos/prepare.cs -- placeholders
```

To use your own photos, put five files with the names above in a folder (`.jpg`, `.jpeg`, `.png` or `.webp`) and run:

```bash
dotnet run tools/example-photos/prepare.cs -- path/to/folder
```

It turns each photo the way the camera meant, makes the longest side at most 1600 pixels, saves it as a JPEG, and writes it to `wwwroot/example`. It removes all EXIF, XMP and IPTC data, which is where the location, the camera and the date are kept, then reads every file back and fails if any of it is left. Only commit images that came out of this tool.

The tool uses ImageSharp version 3, because later versions need a paid licence.
