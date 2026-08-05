# PDFPotpis

Windows desktop app for viewing and digitally signing PDF files using certificates from the Windows certificate store (including Serbian ID card / Lična karta smart-card keys).

Everything runs locally. No document data is uploaded or stored remotely.

## Requirements

- Windows 10/11 x64
- .NET 9 SDK (to build)
- Smart-card middleware / drivers for Lična karta (provided by MUP / card vendor) so the certificate appears in `CurrentUser\My`
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (optional, to build the installer)

## Features

- Open / Save / Save As PDF
- Secure PDF preview via PDFium (no JavaScript execution)
- Digital signature (CMS) with visible stamp (name, surname, certificate serial ID)
- Drag-and-drop stamp placement with live preview
- Certificate picker for local / smart-card certificates
- About dialog in Serbian (privacy / local-only notice)
- Simple install wizard (choose folder → install → done)

## Build & run

```powershell
dotnet build PdfPotpis.sln -c Release
dotnet run --project src\PdfPotpis\PdfPotpis.csproj -c Release
```

## Installer

```powershell
.\scripts\build-installer.ps1
```

Creates a two-step wizard at `installer\output\PDFPotpis-Setup.exe` (choose folder → progress/validation).  
Optional: if Inno Setup 6 is installed, also builds `installer\output\PDFPotpis-Setup-1.0.0.exe`.

## Sign flow

1. Open a PDF
2. Click **Potpiši**
3. Drag the preview stamp to the desired place (click another page to move it there)
4. Click **Potvrdi potpis**
5. Choose a certificate (PIN dialog may appear for the smart card)
6. Save the signed PDF

## Website (landing page)

Static one-page site in `website/` (HTML + CSS, no JavaScript). Open `website/index.html` in a browser, or deploy the `website/` folder to any static host (Cloudflare Pages, Netlify, GitHub Pages).

Before going live:

1. Replace `https://pdfpotpis.example/` in `index.html`, `robots.txt`, and `sitemap.xml` with your real domain.
2. Point the download buttons at your installer (place `PDFPotpis-Setup.exe` in `website/downloads/`, or use a GitHub Releases URL).

## License note

**My code:** do whatever the fuck you want with it.

**iText:** PDF signing uses [iText](https://itextpdf.com/) (AGPL). If you distribute a build that includes iText, you must still comply with AGPL for that combined work, or obtain a commercial iText license. That obligation comes from iText, not from my code.
