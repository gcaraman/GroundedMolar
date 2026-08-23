using System.Globalization;
using System.Text;

namespace GroundedMolar.Core;

public sealed class ExportedUiBoundsProjector
{
    public const double TextureSize = 4096.0;
    public const double MinimumWorldCoordinate = -100000.0;
    public const double MaximumWorldCoordinate = 100000.0;

    // Candidate transform inferred from UI_MapPanel defaults. The native MapPanelWidget
    // implementation is unavailable, so this must remain distinct from CoordinateProjector.
    public PixelPoint WorldToTexture(double worldX, double worldY)
    {
        const double worldSpan = MaximumWorldCoordinate - MinimumWorldCoordinate;
        return new(
            (worldY - MinimumWorldCoordinate) * TextureSize / worldSpan,
            (MaximumWorldCoordinate - worldX) * TextureSize / worldSpan);
    }
}

public sealed class MapPreviewRenderer
{
    public const int Size = 4096;

    public string RenderSvg(MolarAnalysis analysis, ReadOnlySpan<byte> mapPng)
    {
        if (analysis.Confidence != SaveConfidence.Validated)
            throw new InvalidOperationException("Preview rendering requires Validated save confidence.");
        if (analysis.Uncollected.Any(marker => marker.State != MolarState.Uncollected))
            throw new InvalidOperationException("The uncollected set contains a marker with a different state.");

        var accepted = new CoordinateProjector();
        var candidate = new ExportedUiBoundsProjector();
        var svg = new StringBuilder(8192);
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"4096\" height=\"4096\" viewBox=\"0 0 4096 4096\">\n")
            .Append("<rect width=\"4096\" height=\"4096\" fill=\"#11151b\"/>\n")
            .Append("<image width=\"4096\" height=\"4096\" href=\"data:image/png;base64,")
            .Append(Convert.ToBase64String(mapPng)).Append("\"/>\n")
            .Append("<g font-family=\"Segoe UI, sans-serif\" font-size=\"30\" font-weight=\"700\">\n");

        foreach (var marker in analysis.Uncollected)
        {
            var normalized = accepted.WorldToNormalizedMap(marker.WorldX, marker.WorldY);
            var calibratedPoint = new PixelPoint(normalized.U * Size, normalized.V * Size);
            var candidatePoint = candidate.WorldToTexture(marker.WorldX, marker.WorldY);
            var label = marker.SpawnGuid.ToString()[..8];
            svg.AppendFormat(CultureInfo.InvariantCulture,
                "<line x1=\"{0:F2}\" y1=\"{1:F2}\" x2=\"{2:F2}\" y2=\"{3:F2}\" stroke=\"#ffffff\" stroke-width=\"5\" opacity=\"0.75\"/>\n",
                calibratedPoint.X, calibratedPoint.Y, candidatePoint.X, candidatePoint.Y);
            AppendMarker(svg, calibratedPoint, "#00e5ff", label + " calibrated", -42);
            AppendMarker(svg, candidatePoint, "#ff3bd4", label + " UI-bounds", 54);
        }

        svg.Append("</g>\n<g transform=\"translate(48 48)\" font-family=\"Segoe UI, sans-serif\">\n")
            .Append("<rect width=\"780\" height=\"154\" rx=\"18\" fill=\"#080b10\" opacity=\"0.9\"/>\n")
            .Append("<circle cx=\"34\" cy=\"43\" r=\"15\" fill=\"#00e5ff\"/><text x=\"65\" y=\"53\" fill=\"white\" font-size=\"30\">Accepted screenshot calibration</text>\n")
            .Append("<circle cx=\"34\" cy=\"105\" r=\"15\" fill=\"#ff3bd4\"/><text x=\"65\" y=\"115\" fill=\"white\" font-size=\"30\">Candidate exported UI bounds</text>\n")
            .Append("</g>\n</svg>\n");
        return svg.ToString();
    }

    private static void AppendMarker(StringBuilder svg, PixelPoint point, string color, string label, double labelOffset)
    {
        svg.AppendFormat(CultureInfo.InvariantCulture,
            "<circle cx=\"{0:F2}\" cy=\"{1:F2}\" r=\"25\" fill=\"{2}\" stroke=\"#05070a\" stroke-width=\"10\"/>\n<text x=\"{0:F2}\" y=\"{3:F2}\" text-anchor=\"middle\" fill=\"white\" stroke=\"#05070a\" stroke-width=\"8\" paint-order=\"stroke\">{4}</text>\n",
            point.X, point.Y, color, point.Y + labelOffset, label);
    }
}
