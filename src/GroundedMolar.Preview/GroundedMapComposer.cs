using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GroundedMolar.Core;

internal static class GroundedMapComposer
{
    public const int OutputSize = 4096;

    public static void Build(string mapIconsDirectory, string outputPath)
    {
        var background = Layer.Load(Path.Combine(mapIconsDirectory, "T_UI_Worldmap_BG.png"));
        var baseMap = Layer.Load(Path.Combine(mapIconsDirectory, "T_UI_Worldmap_Base.png"));
        var water = Layer.Load(Path.Combine(mapIconsDirectory, "T_UI_Worldmap_Water.png"));
        var stride = OutputSize * 4;
        var pixels = new byte[stride * OutputSize];

        Parallel.For(0, OutputSize, y =>
        {
            // FModel exports these textures 90 degrees clockwise from the in-game map.
            var sourceU = (OutputSize - 0.5 - y) / OutputSize;
            for (var x = 0; x < OutputSize; x++)
            {
                var sourceV = (x + 0.5) / OutputSize;
                var bg = background.Sample(sourceU, sourceV);
                var land = baseMap.Sample(sourceU, sourceV);
                var pond = water.Sample(sourceU, sourceV);
                var baseLuminance = ((land.R + land.G + land.B) / (3.0 * 255.0)) * (land.A / 255.0);
                var color = GroundedMapPalette.Compose(bg.A / 255.0, baseLuminance, pond.A / 255.0);
                var offset = y * stride + x * 4;
                pixels[offset] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = 255;
            }
        });

        var bitmap = BitmapSource.Create(OutputSize, OutputSize, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    private sealed class Layer(byte[] pixels, int width, int height, int stride)
    {
        public static Layer Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Required exported map layer was not found.", path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            var stride = converted.PixelWidth * 4;
            var pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);
            return new Layer(pixels, converted.PixelWidth, converted.PixelHeight, stride);
        }

        public (double R, double G, double B, double A) Sample(double u, double v)
        {
            var px = Math.Clamp(u * width - 0.5, 0, width - 1.0);
            var py = Math.Clamp(v * height - 0.5, 0, height - 1.0);
            var x0 = (int)px; var y0 = (int)py;
            var x1 = Math.Min(x0 + 1, width - 1); var y1 = Math.Min(y0 + 1, height - 1);
            var tx = px - x0; var ty = py - y0;
            return (
                Bilinear(x0, y0, x1, y1, tx, ty, 2),
                Bilinear(x0, y0, x1, y1, tx, ty, 1),
                Bilinear(x0, y0, x1, y1, tx, ty, 0),
                Bilinear(x0, y0, x1, y1, tx, ty, 3));
        }

        private double Bilinear(int x0, int y0, int x1, int y1, double tx, double ty, int channel)
        {
            var top = pixels[y0 * stride + x0 * 4 + channel] * (1 - tx) + pixels[y0 * stride + x1 * 4 + channel] * tx;
            var bottom = pixels[y1 * stride + x0 * 4 + channel] * (1 - tx) + pixels[y1 * stride + x1 * 4 + channel] * tx;
            return top * (1 - ty) + bottom * ty;
        }
    }
}
