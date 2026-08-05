using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using iText.Kernel.Pdf;
using PdfPotpis.Models;

namespace PdfPotpis.Services;

/// <summary>
/// Renders PDF pages to bitmaps via PDFium. Scripts and JavaScript are never executed.
/// </summary>
public sealed class PdfRenderService
{
    private readonly IDocLib _docLib = DocLib.Instance;
    private const int TargetWidthPx = 1200;

    public IReadOnlyList<PdfPageImage> RenderAllPages(byte[] pdfBytes)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        var pageSizes = ReadPageSizesPts(pdfBytes);

        using var docReader = _docLib.GetDocReader(pdfBytes, new PageDimensions(TargetWidthPx, TargetWidthPx * 2));
        int pageCount = docReader.GetPageCount();
        var pages = new List<PdfPageImage>(pageCount);

        for (int i = 0; i < pageCount; i++)
        {
            using var pageReader = docReader.GetPageReader(i);
            int width = pageReader.GetPageWidth();
            int height = pageReader.GetPageHeight();
            // Signature widgets are annotations/forms; default GetImage() skips them.
            byte[] rawBgra = pageReader.GetImage(RenderFlags.RenderAnnotations);
            BitmapSource bitmap = CreateBitmap(rawBgra, width, height);

            float pageWidthPts = i < pageSizes.Count ? pageSizes[i].Width : 595f;
            float pageHeightPts = i < pageSizes.Count ? pageSizes[i].Height : pageWidthPts * height / Math.Max(width, 1);

            pages.Add(new PdfPageImage
            {
                PageIndex = i,
                Image = bitmap,
                PageWidthPts = pageWidthPts,
                PageHeightPts = pageHeightPts
            });
        }

        return pages;
    }

    private static List<(float Width, float Height)> ReadPageSizesPts(byte[] pdfBytes)
    {
        var sizes = new List<(float Width, float Height)>();
        using var reader = new PdfReader(new MemoryStream(pdfBytes));
        using var pdfDoc = new PdfDocument(reader);
        for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
        {
            var pageSize = pdfDoc.GetPage(i).GetPageSize();
            sizes.Add((pageSize.GetWidth(), pageSize.GetHeight()));
        }

        return sizes;
    }

    private static BitmapSource CreateBitmap(byte[] bgra, int width, int height)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), bgra, width * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }
}
