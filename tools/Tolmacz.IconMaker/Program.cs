// Makes a Windows .ico file with every standard icon size from an SVG file.
//
// .NET MAUI makes a Windows icon with a single 64 x 64 image, which looks soft in large views.
// The app and its installer use the icon made by this tool instead.
//
// Usage:
//   dotnet run --project tools/Tolmacz.IconMaker -- <input.svg> <output.ico>

using SkiaSharp;
using Svg.Skia;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: Tolmacz.IconMaker <input.svg> <output.ico>");
    return 1;
}

int[] sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

using var svg = new SKSvg();
var picture = svg.Load(args[0]);
if (picture is null)
{
    Console.Error.WriteLine($"Cannot read the SVG file '{args[0]}'.");
    return 1;
}

var images = sizes.Select(size => IconImage.Create(picture, size)).ToList();
using (var output = File.Create(args[1]))
{
    IconFile.Write(output, images);
}

Console.WriteLine($"Wrote {args[1]} with sizes {string.Join(", ", sizes)}.");
return 0;

/// <summary>One image in an icon file: its size and its encoded data.</summary>
internal sealed record IconImage(int Size, byte[] Data)
{
    // Images up to this size are stored as bitmaps, which every Windows tool reads.
    // Larger images are stored as PNG, as Windows expects for 256 x 256.
    private const int LargestBitmapSize = 128;

    /// <summary>Draws the picture at the given size and encodes it for an icon file.</summary>
    public static IconImage Create(SKPicture picture, int size)
    {
        var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(size / picture.CullRect.Width, size / picture.CullRect.Height);
            canvas.DrawPicture(picture);
        }

        var data = size <= LargestBitmapSize ? EncodeBitmap(bitmap) : EncodePng(bitmap);
        return new IconImage(size, data);
    }

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // An icon bitmap is a BITMAPINFOHEADER, the 32-bit pixels from the bottom row up,
    // and a 1-bit mask. The mask is unused because the pixels carry their own transparency.
    private static byte[] EncodeBitmap(SKBitmap bitmap)
    {
        var size = bitmap.Width;
        var maskRowBytes = (size + 31) / 32 * 4;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(40);              // header size
        writer.Write(size);            // width
        writer.Write(size * 2);        // height of pixels and mask together
        writer.Write((short)1);        // planes
        writer.Write((short)32);       // bits per pixel
        writer.Write(0);               // no compression
        writer.Write(size * size * 4 + maskRowBytes * size);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        var pixels = bitmap.GetPixelSpan();
        for (var row = size - 1; row >= 0; row--)
        {
            writer.Write(pixels.Slice(row * bitmap.RowBytes, size * 4));
        }

        writer.Write(new byte[maskRowBytes * size]);
        return stream.ToArray();
    }
}

/// <summary>Writes the ICO container around the icon images.</summary>
internal static class IconFile
{
    private const int HeaderSize = 6;
    private const int EntrySize = 16;

    public static void Write(Stream output, IReadOnlyList<IconImage> images)
    {
        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((short)0);        // reserved
        writer.Write((short)1);        // type: icon
        writer.Write((short)images.Count);

        var offset = HeaderSize + EntrySize * images.Count;
        foreach (var image in images)
        {
            // A size of 256 is written as 0.
            var side = (byte)(image.Size >= 256 ? 0 : image.Size);
            writer.Write(side);
            writer.Write(side);
            writer.Write((byte)0);     // no palette
            writer.Write((byte)0);     // reserved
            writer.Write((short)1);    // planes
            writer.Write((short)32);   // bits per pixel
            writer.Write(image.Data.Length);
            writer.Write(offset);
            offset += image.Data.Length;
        }

        foreach (var image in images)
        {
            writer.Write(image.Data);
        }
    }
}
