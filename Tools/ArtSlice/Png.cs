using System.IO.Compression;

namespace ArtSlice;

/// <summary>
/// A PNG reader and writer, deliberately small.
///
/// The alternative was ImageMagick or Pillow, neither of which is on this machine, and adding a
/// native image dependency to a repo whose whole art pipeline is "no binary tooling" felt worse
/// than 200 lines that only have to handle the files we actually author: 8-bit, non-interlaced,
/// RGB or RGBA. Anything else is rejected loudly rather than half-decoded.
/// </summary>
public sealed class Image
{
    public int Width;
    public int Height;

    /// <summary>RGBA, four bytes per pixel, row-major from the top. Always RGBA regardless of the
    /// source colour type, so callers never branch on it.</summary>
    public byte[] Pixels;

    public Image(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }

    public int Index(int x, int y) => (y * Width + x) * 4;
    public byte Alpha(int x, int y) => Pixels[Index(x, y) + 3];

    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static Image Load(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        for (int i = 0; i < Signature.Length; i++)
            if (data[i] != Signature[i])
                throw new InvalidDataException($"{path} is not a PNG.");

        int width = 0, height = 0, bitDepth = 0, colourType = 0, interlace = 0;
        var idat = new MemoryStream();
        int pos = 8;

        while (pos + 8 <= data.Length)
        {
            int length = ReadInt(data, pos);
            string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            int body = pos + 8;

            switch (type)
            {
                case "IHDR":
                    width = ReadInt(data, body);
                    height = ReadInt(data, body + 4);
                    bitDepth = data[body + 8];
                    colourType = data[body + 9];
                    interlace = data[body + 12];
                    break;
                case "IDAT":
                    idat.Write(data, body, length);
                    break;
                case "IEND":
                    pos = data.Length;
                    break;
            }

            pos = body + length + 4;
        }

        if (bitDepth != 8) throw new NotSupportedException($"{path}: bit depth {bitDepth}, only 8 supported.");
        if (interlace != 0) throw new NotSupportedException($"{path}: interlaced PNGs not supported.");
        if (colourType != 2 && colourType != 6)
            throw new NotSupportedException($"{path}: colour type {colourType}, only 2 (RGB) and 6 (RGBA) supported.");

        int channels = colourType == 6 ? 4 : 3;
        idat.Position = 0;
        using var inflate = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        inflate.CopyTo(raw);
        byte[] scan = raw.ToArray();

        var image = new Image(width, height);
        int stride = width * channels;
        byte[] prior = new byte[stride];
        byte[] line = new byte[stride];
        int read = 0;

        for (int y = 0; y < height; y++)
        {
            byte filter = scan[read++];
            Array.Copy(scan, read, line, 0, stride);
            read += stride;
            Unfilter(filter, line, prior, channels);

            for (int x = 0; x < width; x++)
            {
                int s = x * channels;
                int d = image.Index(x, y);
                image.Pixels[d] = line[s];
                image.Pixels[d + 1] = line[s + 1];
                image.Pixels[d + 2] = line[s + 2];
                image.Pixels[d + 3] = channels == 4 ? line[s + 3] : (byte)255;
            }

            (prior, line) = (line, prior);
        }

        return image;
    }

    /// <summary>
    /// Reverses one scanline filter in place. The four filters differ only in which already-decoded
    /// neighbours they predicted from, so they share this shape.
    /// </summary>
    private static void Unfilter(byte filter, byte[] line, byte[] prior, int bpp)
    {
        switch (filter)
        {
            case 0:
                break;
            case 1:
                for (int i = bpp; i < line.Length; i++) line[i] = (byte)(line[i] + line[i - bpp]);
                break;
            case 2:
                for (int i = 0; i < line.Length; i++) line[i] = (byte)(line[i] + prior[i]);
                break;
            case 3:
                for (int i = 0; i < line.Length; i++)
                {
                    int left = i >= bpp ? line[i - bpp] : 0;
                    line[i] = (byte)(line[i] + ((left + prior[i]) >> 1));
                }
                break;
            case 4:
                for (int i = 0; i < line.Length; i++)
                {
                    int a = i >= bpp ? line[i - bpp] : 0;
                    int b = prior[i];
                    int c = i >= bpp ? prior[i - bpp] : 0;
                    int p = a + b - c;
                    int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                    int pred = pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
                    line[i] = (byte)(line[i] + pred);
                }
                break;
            default:
                throw new InvalidDataException($"Unknown PNG filter {filter}.");
        }
    }

    /// <summary>The rectangle (x, y, w, h) of this image written out as its own RGBA PNG.</summary>
    public void SaveCrop(string path, int x0, int y0, int w, int h)
    {
        var stride = w * 4;
        var raw = new MemoryStream();
        for (int y = 0; y < h; y++)
        {
            raw.WriteByte(0); // filter: none. These are small and already compressed well by zlib.
            raw.Write(Pixels, Index(x0, y0 + y), stride);
        }

        var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.SmallestSize, true))
            deflate.Write(raw.ToArray());

        using var f = File.Create(path);
        f.Write(Signature);

        var ihdr = new byte[13];
        WriteInt(ihdr, 0, w);
        WriteInt(ihdr, 4, h);
        ihdr[8] = 8;   // bit depth
        ihdr[9] = 6;   // colour type: RGBA
        WriteChunk(f, "IHDR", ihdr);
        WriteChunk(f, "IDAT", compressed.ToArray());
        WriteChunk(f, "IEND", Array.Empty<byte>());
    }

    private static void WriteChunk(Stream s, string type, byte[] body)
    {
        var len = new byte[4];
        WriteInt(len, 0, body.Length);
        s.Write(len);

        var typed = new byte[4 + body.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(typed, 0);
        body.CopyTo(typed, 4);
        s.Write(typed);

        var crc = new byte[4];
        WriteInt(crc, 0, unchecked((int)Crc32(typed)));
        s.Write(crc);
    }

    private static int ReadInt(byte[] d, int o) => (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];

    private static void WriteInt(byte[] d, int o, int v)
    {
        d[o] = (byte)(v >> 24); d[o + 1] = (byte)(v >> 16); d[o + 2] = (byte)(v >> 8); d[o + 3] = (byte)v;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    private static uint Crc32(byte[] data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
