using System.Windows.Media.Imaging;

namespace PdfPotpis.Models;

public sealed class PdfPageImage
{
    public required int PageIndex { get; init; }

    public required BitmapSource Image { get; init; }

    /// <summary>PDF page width in points (1/72 inch).</summary>
    public required float PageWidthPts { get; init; }

    /// <summary>PDF page height in points (1/72 inch).</summary>
    public required float PageHeightPts { get; init; }
}
