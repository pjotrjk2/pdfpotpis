using System.IO;

namespace PdfPotpis.Services;

/// <summary>
/// Holds the in-memory PDF document and file path state.
/// </summary>
public sealed class PdfDocumentService
{
    public byte[]? PdfBytes { get; private set; }

    public string? FilePath { get; private set; }

    public bool IsDirty { get; private set; }

    public bool HasDocument => PdfBytes is { Length: > 0 };

    public void Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        PdfBytes = File.ReadAllBytes(path);
        FilePath = path;
        IsDirty = false;
    }

    public void LoadBytes(byte[] bytes, string? path = null, bool dirty = false)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        PdfBytes = bytes;
        if (path is not null)
        {
            FilePath = path;
        }

        IsDirty = dirty;
    }

    public void Save()
    {
        if (PdfBytes is null)
        {
            throw new InvalidOperationException("Nema otvorenog dokumenta.");
        }

        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new InvalidOperationException("Putanja fajla nije poznata. Koristite Sačuvaj kao.");
        }

        File.WriteAllBytes(FilePath, PdfBytes);
        IsDirty = false;
    }

    public void SaveAs(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (PdfBytes is null)
        {
            throw new InvalidOperationException("Nema otvorenog dokumenta.");
        }

        File.WriteAllBytes(path, PdfBytes);
        FilePath = path;
        IsDirty = false;
    }

    public void MarkDirty()
    {
        IsDirty = true;
    }
}
