namespace PdfPotpis.Models;

/// <summary>
/// Placement of a visible signature stamp on a PDF page.
/// Coordinates use PDF space (origin bottom-left, units in points).
/// </summary>
public sealed class SignaturePlacement
{
    public int PageIndex { get; set; }

    public float PdfX { get; set; }

    public float PdfY { get; set; }

    public float WidthPts { get; set; } = 180f;

    public float HeightPts { get; set; } = 60f;
}
