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

Produces single-file artifacts in `installer\output\`:

- `PDFPotpis-Setup.exe` — install wizard (embeds the app)
- `PDFPotpis-Portable.exe` — run without installing

Optional: if Inno Setup 6 is installed, also builds `PDFPotpis-Setup-<version>.exe`.

Installed builds register PDFPotpis under the current user so it appears in **Open with** for `.pdf` files (does not force itself as the default PDF app).

## Release / deploy prep

One product version (app, installer, site) lives in `Directory.Build.props`.

```powershell
# Rebuild, copy both exes → website/downloads, write SHAs onto the site
.\scripts\prepare-release.ps1

# Bump patch (1.0.0 → 1.0.1), build, stage downloads + SHA
.\scripts\prepare-release.ps1 -Bump patch

# Set an exact version and create a GitHub Release (needs `gh` CLI)
.\scripts\prepare-release.ps1 -Version 1.2.0 -CreateRelease
```

This will:

1. Sync the version into the Inno script, manifests, and landing page
2. Publish single-file app + installer (`build-installer.ps1`)
3. Copy `PDFPotpis-Setup.exe` and `PDFPotpis-Portable.exe` into `website/downloads/`
4. Write `.sha256` files and inject both hashes into `index.html`
5. With `-CreateRelease`, create/upload a GitHub Release (`vX.Y.Z`) when `gh` is available

Deploy the `website/` folder (including `downloads/`) to your static host after running the script. For a Linux web root, upload the contents of `website/` (two exes + HTML/CSS/assets).

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
2. Run `.\scripts\prepare-release.ps1` so `website/downloads/` has the installer and the page shows the matching SHA-256.

## License note

**My code:** do whatever the fuck you want with it.

**iText:** PDF signing uses [iText](https://itextpdf.com/) (AGPL). If you distribute a build that includes iText, you must still comply with AGPL for that combined work, or obtain a commercial iText license. That obligation comes from iText, not from my code.
