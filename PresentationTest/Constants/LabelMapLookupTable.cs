using Kitware.VTK;

namespace PresentationTest.Constants;

public static class LabelMapLookupTable
{
    public const double Opacity = 0.3;

    public static vtkLookupTable NewTable()
        => CreateGoldenRatioHueShuffle();

    private static vtkLookupTable CreateGoldenRatioHueShuffle()
    {
        vtkLookupTable? lut = vtkLookupTable.New();
        lut.SetNumberOfTableValues(256);
        lut.SetRange(0, 255);

        const double φ = 0.618033988749895; // 1/ϕ
        lut.SetTableValue(0, 0, 0, 0, 0); // background

        for (int i = 1; i < 256; ++i)
        {
            double hue = i * φ % 1.0; // hops around the wheel
            HsvColor hsv = new(hue * 360, 0.8, 1.0); // slightly desat → less neon
            Color rgb = hsv.ToColor();

            lut.SetTableValue(i,
                rgb.R / 255.0,
                rgb.G / 255.0,
                rgb.B / 255.0,
                Opacity); // 30 % opacity
        }

        lut.Build();
        return lut;
    }
}

/// <summary>
///     Represents a color in the HSV color space.
///     Hue in [0,360), Saturation and Value in [0,1].
/// </summary>
public readonly record struct HsvColor(double H, double S, double V)
{
    public override string ToString() => $"H={H:F1}°, S={S:P0}, V={V:P0}";
}

public static class ColorExtensions
{
    /// <summary>
    ///     Convert a WPF Color to its HSV equivalent.
    /// </summary>
    public static HsvColor ToHsv(this Color color)
    {
        // Normalize RGB to [0,1]
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        // Hue calculation
        double h = 0;
        if (delta > 0)
        {
            if (max == r)
                h = 60 * ((g - b) / delta % 6);
            else if (max == g)
                h = 60 * ((b - r) / delta + 2);
            else // max == b
                h = 60 * ((r - g) / delta + 4);
        }

        // make sure hue is positive
        if (h < 0) h += 360;

        // Saturation
        double s = max == 0 ? 0 : delta / max;

        // Value
        double v = max;

        return new HsvColor(h, s, v);
    }

    /// <summary>
    ///     Create a WPF Color from HSV values, preserving alpha.
    ///     Hue in [0,360), S and V in [0,1].
    /// </summary>
    public static Color ToColor(this HsvColor hsv, byte alpha = 255)
    {
        double hue = hsv.H;
        double saturation = hsv.S;
        double value = hsv.V;

        // Clamp inputs
        hue = (hue % 360 + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        double c = value * saturation; // chroma
        double x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        double m = value - c;

        double r1 = 0, g1 = 0, b1 = 0;
        if (hue < 60)
        {
            r1 = c;
            g1 = x;
            b1 = 0;
        }
        else if (hue < 120)
        {
            r1 = x;
            g1 = c;
            b1 = 0;
        }
        else if (hue < 180)
        {
            r1 = 0;
            g1 = c;
            b1 = x;
        }
        else if (hue < 240)
        {
            r1 = 0;
            g1 = x;
            b1 = c;
        }
        else if (hue < 300)
        {
            r1 = x;
            g1 = 0;
            b1 = c;
        }
        else
        {
            r1 = c;
            g1 = 0;
            b1 = x;
        }

        byte r = (byte)Math.Round((r1 + m) * 255);
        byte g = (byte)Math.Round((g1 + m) * 255);
        byte b = (byte)Math.Round((b1 + m) * 255);

        return Color.FromArgb(alpha, r, g, b);
    }
}