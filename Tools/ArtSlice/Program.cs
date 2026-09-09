namespace ArtSlice;

/// <summary>
/// Cuts a sheet of UI elements into one transparent PNG per element.
///
/// The art arrives as sheets — a dozen buttons and icons laid out on one transparent canvas — and
/// the game wants them as individual named sprites. Doing that by hand is tedious and has to be
/// redone every time a sheet is re-exported, so it is a tool instead.
///
///   dotnet run -- info  &lt;sheet.png&gt;
///   dotnet run -- slice &lt;sheet.png&gt; &lt;outdir&gt; [--gap N] [--min N] [--alpha N] [--pad N]
///
/// `info` reports what is actually in the file — the check that caught the first delivery of these
/// sheets, which had a checkerboard painted into the pixels and no alpha channel at all.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 2) return Usage();

        string command = args[0];
        string path = args[1];

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"No such file: {path}");
            return 1;
        }

        Image image = Image.Load(path);

        switch (command)
        {
            case "info": return Info(image, path);
            case "slice": return Slice(image, args);
            case "sample": return Sample(image, args);
            case "key": return Key(image, args);
            default: return Usage();
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: artslice info <sheet.png>");
        Console.Error.WriteLine("       artslice slice <sheet.png> <outdir> [--gap N] [--min N] [--alpha N] [--pad N]");
        return 2;
    }

    /// <summary>
    /// What the file really contains. An alpha channel that exists but is entirely opaque is the
    /// failure worth naming out loud: the PNG header says RGBA, the art looks right in a viewer,
    /// and every sprite still arrives in the game as a rectangle with a background.
    /// </summary>
    private static int Info(Image image, string path)
    {
        long transparent = 0, partial = 0, opaque = 0;
        byte min = 255, max = 0;

        for (int i = 3; i < image.Pixels.Length; i += 4)
        {
            byte a = image.Pixels[i];
            if (a == 0) transparent++;
            else if (a == 255) opaque++;
            else partial++;
            if (a < min) min = a;
            if (a > max) max = a;
        }

        long total = (long)image.Width * image.Height;
        Console.WriteLine($"{Path.GetFileName(path)}");
        Console.WriteLine($"  {image.Width} x {image.Height}   ({total:N0} pixels)");
        Console.WriteLine($"  alpha range      {min} .. {max}");
        Console.WriteLine($"  fully transparent {transparent,12:N0}  {100.0 * transparent / total,6:F2} %");
        Console.WriteLine($"  partial           {partial,12:N0}  {100.0 * partial / total,6:F2} %");
        Console.WriteLine($"  fully opaque      {opaque,12:N0}  {100.0 * opaque / total,6:F2} %");

        // Where the visible pixels actually sit. A logo whose letter bodies never reach full opacity
        // has had its alpha scaled down somewhere in export, and will look washed out composited
        // over a busy background — a failure that is invisible against the dark canvas of a viewer.
        var buckets = new (int Lo, int Hi, string Label)[]
        {
            (1, 63, "  1- 63  barely there"),
            (64, 127, " 64-127  faint"),
            (128, 191, "128-191  half"),
            (192, 239, "192-239  nearly solid"),
            (240, 254, "240-254  almost opaque"),
            (255, 255, "    255  solid"),
        };
        var counts = new long[buckets.Length];
        for (int i = 3; i < image.Pixels.Length; i += 4)
        {
            byte a = image.Pixels[i];
            if (a == 0) continue;
            for (int b = 0; b < buckets.Length; b++)
                if (a >= buckets[b].Lo && a <= buckets[b].Hi) { counts[b]++; break; }
        }

        long visible = total - transparent;
        if (visible > 0)
        {
            Console.WriteLine("  visible pixels by alpha:");
            for (int b = 0; b < buckets.Length; b++)
                Console.WriteLine($"    {buckets[b].Label}  {counts[b],12:N0}  {100.0 * counts[b] / visible,6:F2} % of visible");
        }

        if (transparent == 0)
            Console.WriteLine("  VERDICT: no transparent pixels. This sheet cannot be sliced into free-standing sprites.");
        else if (partial == 0)
            Console.WriteLine("  VERDICT: hard-edged alpha only — no soft edges. Usable, but edges will look aliased.");
        else
            Console.WriteLine("  VERDICT: real transparency with soft edges. Good to slice.");

        return 0;
    }

    private static int Slice(Image image, string[] args)
    {
        if (args.Length < 3) return Usage();
        string outDir = args[2];

        int gap = IntArg(args, "--gap", 12);
        int minSize = IntArg(args, "--min", 24);
        int alphaThreshold = IntArg(args, "--alpha", 12);
        int pad = IntArg(args, "--pad", 2);

        var boxes = FindElements(image, alphaThreshold, gap, minSize);
        Directory.CreateDirectory(outDir);

        Console.WriteLine($"{image.Width}x{image.Height}  ->  {boxes.Count} elements " +
                          $"(gap {gap}, min {minSize}, alpha {alphaThreshold})");
        Console.WriteLine();

        int n = 0;
        foreach (var b in boxes)
        {
            int x0 = Math.Max(0, b.X0 - pad);
            int y0 = Math.Max(0, b.Y0 - pad);
            int x1 = Math.Min(image.Width - 1, b.X1 + pad);
            int y1 = Math.Min(image.Height - 1, b.Y1 + pad);
            int w = x1 - x0 + 1, h = y1 - y0 + 1;

            string name = $"{n:00}_{w}x{h}.png";
            image.SaveCrop(Path.Combine(outDir, name), x0, y0, w, h);
            Console.WriteLine($"  {name,-18} at ({x0,4},{y0,4})  {w,4} x {h,-4}");
            n++;
        }

        Console.WriteLine();
        Console.WriteLine($"Wrote {n} files to {outDir}");
        Console.WriteLine("Rename them to their part names; the numbers are only a stable ordering.");
        return 0;
    }

    /// <summary>
    /// The average colour of a square patch, for answering "is this the same colour as that" from
    /// the files rather than from memory of how they looked on screen.
    ///
    ///   artslice sample &lt;file.png&gt; &lt;x&gt; &lt;y&gt; [--size N]
    ///
    /// Averaged rather than read as a single pixel because everything here is glossy: any one pixel
    /// might land on a specular highlight and report near-white for a mid-blue block.
    /// </summary>
    private static int Sample(Image image, string[] args)
    {
        if (args.Length < 4) return Usage();
        if (!int.TryParse(args[2], out int cx) || !int.TryParse(args[3], out int cy)) return Usage();
        int size = IntArg(args, "--size", 16);

        long r = 0, g = 0, b = 0, a = 0, n = 0;
        for (int y = cy - size / 2; y <= cy + size / 2; y++)
        for (int x = cx - size / 2; x <= cx + size / 2; x++)
        {
            if (x < 0 || y < 0 || x >= image.Width || y >= image.Height) continue;
            int i = image.Index(x, y);
            r += image.Pixels[i]; g += image.Pixels[i + 1]; b += image.Pixels[i + 2]; a += image.Pixels[i + 3];
            n++;
        }

        if (n == 0) { Console.Error.WriteLine("Sample fell entirely outside the image."); return 1; }

        int ar = (int)(r / n), ag = (int)(g / n), ab = (int)(b / n), aa = (int)(a / n);
        Console.WriteLine($"({cx},{cy}) {size}x{size}  rgb({ar,3},{ag,3},{ab,3})  #{ar:X2}{ag:X2}{ab:X2}  alpha {aa}");
        return 0;
    }

    /// <summary>
    /// Removes a flat background colour, turning an opaque sheet into a transparent one.
    ///
    ///   artslice key &lt;in.png&gt; &lt;out.png&gt; [--tolerance N] [--soft N]
    ///
    /// This exists because art keeps arriving on a solid colour instead of on transparency. A flat
    /// background is recoverable; a checkerboard painted into the pixels is not, because it varies
    /// per pixel and there is nothing to subtract.
    ///
    /// The background colour is read from the image's own corners rather than passed in, so a sheet
    /// on a slightly different blue than the last one still works.
    ///
    /// **It floods inward from the border rather than keying every matching pixel.** That distinction
    /// is the whole reason this is usable here: these sheets are pale blue buttons on a pale blue
    /// background, and a global colour test would punch holes straight through the middle of every
    /// blue element. Only background connected to the edge is removed.
    /// </summary>
    private static int Key(Image image, string[] args)
    {
        if (args.Length < 3) return Usage();
        string outPath = args[2];

        int tolerance = IntArg(args, "--tolerance", 26);
        int soft = IntArg(args, "--soft", 34);

        // The background colour, averaged over all four corners so one stray pixel cannot define it.
        var corners = new[] { (4, 4), (image.Width - 5, 4), (4, image.Height - 5), (image.Width - 5, image.Height - 5) };
        int br = 0, bg = 0, bb = 0;
        foreach ((int x, int y) in corners)
        {
            int i = image.Index(x, y);
            br += image.Pixels[i]; bg += image.Pixels[i + 1]; bb += image.Pixels[i + 2];
        }
        br /= 4; bg /= 4; bb /= 4;

        Console.WriteLine($"background  rgb({br},{bg},{bb})  #{br:X2}{bg:X2}{bb:X2}   tolerance {tolerance}, soft {soft}");

        int w = image.Width, h = image.Height;
        var reached = new bool[w * h];
        var stack = new Stack<int>();

        void Consider(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int p = y * w + x;
            if (reached[p]) return;
            if (Distance(image, p, br, bg, bb) > soft) return;
            reached[p] = true;
            stack.Push(p);
        }

        for (int x = 0; x < w; x++) { Consider(x, 0); Consider(x, h - 1); }
        for (int y = 0; y < h; y++) { Consider(0, y); Consider(w - 1, y); }

        while (stack.Count > 0)
        {
            int p = stack.Pop();
            int px = p % w, py = p / w;
            Consider(px - 1, py); Consider(px + 1, py);
            Consider(px, py - 1); Consider(px, py + 1);
        }

        long cleared = 0, feathered = 0;
        for (int p = 0; p < w * h; p++)
        {
            if (!reached[p]) continue;

            double d = Distance(image, p, br, bg, bb);
            int i = p * 4;

            if (d <= tolerance)
            {
                image.Pixels[i + 3] = 0;
                cleared++;
                continue;
            }

            // Between the two thresholds is the anti-aliased rim, where the pixel is a blend of the
            // element and the background. Recover both how much of it is element, and what colour the
            // element was before the background was mixed in — without the unmultiply, every edge
            // keeps a halo of the colour that was supposed to be removed.
            double alpha = Math.Clamp((d - tolerance) / (double)Math.Max(1, soft - tolerance), 0.0, 1.0);
            image.Pixels[i + 3] = (byte)Math.Round(alpha * 255.0);
            image.Pixels[i] = Unmix(image.Pixels[i], br, alpha);
            image.Pixels[i + 1] = Unmix(image.Pixels[i + 1], bg, alpha);
            image.Pixels[i + 2] = Unmix(image.Pixels[i + 2], bb, alpha);
            feathered++;
        }

        image.SaveCrop(outPath, 0, 0, w, h);

        long total = (long)w * h;
        Console.WriteLine($"  cleared   {cleared,12:N0}  {100.0 * cleared / total,6:F2} %");
        Console.WriteLine($"  feathered {feathered,12:N0}  {100.0 * feathered / total,6:F2} %");
        Console.WriteLine($"  kept      {total - cleared - feathered,12:N0}");
        Console.WriteLine($"Wrote {outPath}");
        return 0;
    }

    private static double Distance(Image img, int pixel, int br, int bg, int bb)
    {
        int i = pixel * 4;
        int dr = img.Pixels[i] - br, dg = img.Pixels[i + 1] - bg, db = img.Pixels[i + 2] - bb;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>Recovers a straight colour from one composited over a known background.</summary>
    private static byte Unmix(byte composited, int background, double alpha)
    {
        if (alpha <= 0.004) return composited;
        double v = (composited - (1.0 - alpha) * background) / alpha;
        return (byte)Math.Clamp(v, 0, 255);
    }

    private sealed class Box
    {
        public int X0, Y0, X1, Y1;
        public bool Touches(Box o, int gap) =>
            X0 - gap <= o.X1 && o.X0 - gap <= X1 && Y0 - gap <= o.Y1 && o.Y0 - gap <= Y1;
        public void Absorb(Box o)
        {
            X0 = Math.Min(X0, o.X0); Y0 = Math.Min(Y0, o.Y0);
            X1 = Math.Max(X1, o.X1); Y1 = Math.Max(Y1, o.Y1);
        }
        public int Area => (X1 - X0 + 1) * (Y1 - Y0 + 1);
    }

    /// <summary>
    /// Connected components of visible pixels, then merged by proximity.
    ///
    /// The merge is the part that matters. Plenty of single elements are several disconnected
    /// islands — a sparkle burst is a dozen separate dots, the calendar icon's rings float above
    /// its body — so raw connected components shatter them. Anything within <paramref name="gap"/>
    /// pixels is treated as one element, which holds because the sheets space their elements much
    /// further apart than the internal gaps of any one of them.
    /// </summary>
    private static List<Box> FindElements(Image image, int alphaThreshold, int gap, int minSize)
    {
        int w = image.Width, h = image.Height;
        var seen = new bool[w * h];
        var boxes = new List<Box>();
        var stack = new Stack<int>();

        for (int start = 0; start < w * h; start++)
        {
            if (seen[start]) continue;
            seen[start] = true;
            if (image.Pixels[start * 4 + 3] <= alphaThreshold) continue;

            var box = new Box { X0 = start % w, Y0 = start / w, X1 = start % w, Y1 = start / w };
            stack.Push(start);

            while (stack.Count > 0)
            {
                int p = stack.Pop();
                int px = p % w, py = p / w;
                if (px < box.X0) box.X0 = px;
                if (px > box.X1) box.X1 = px;
                if (py < box.Y0) box.Y0 = py;
                if (py > box.Y1) box.Y1 = py;

                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = px + dx, ny = py + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int q = ny * w + nx;
                    if (seen[q]) continue;
                    seen[q] = true;
                    if (image.Pixels[q * 4 + 3] > alphaThreshold) stack.Push(q);
                }
            }

            boxes.Add(box);
        }

        // Merge repeatedly: absorbing two boxes can bring a third within reach of the union.
        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
            {
                if (!boxes[i].Touches(boxes[j], gap)) continue;
                boxes[i].Absorb(boxes[j]);
                boxes.RemoveAt(j);
                merged = true;
                j--;
            }
        }

        return boxes
            .Where(b => b.X1 - b.X0 + 1 >= minSize && b.Y1 - b.Y0 + 1 >= minSize)
            .OrderBy(b => b.Y0 / 40)   // group into visual rows before ordering left to right
            .ThenBy(b => b.X0)
            .ToList();
    }

    private static int IntArg(string[] args, string name, int fallback)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int v) ? v : fallback;
    }
}
