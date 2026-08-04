# PDFPotpis MVP Implementation Plan

## Goal

Windows-only desktop app to open, view (secure/no JS), save, and digitally sign PDF files using certificates from Serbian ID card (Lična karta / MUP RS) via the Windows certificate store. Local-only; Serbian About text; simple install wizard.

## Stack

| Piece | Choice | Why |
|-------|--------|-----|
| Runtime | .NET 9 (net9.0-windows) | Latest stable SDK on machine |
| UI | WPF | Native Windows, simple for desktop PDF UI |
| PDF render | Docnet.Core (PDFium) | Page→bitmap, no JS execution |
| PDF sign/save | iText 8 + BouncyCastle | PAdES-style PKCS#7, appearance stream |
| Certs | `X509Store` + `X509Certificate2UI` | Smart-card keys stay on card; OS PIN dialog |
| Installer | Inno Setup script | 1–2 step wizard, folder choice only |

## Solution layout

```
PdfPotpis.sln
src/PdfPotpis/                 # WPF app
  Services/PdfDocumentService.cs
  Services/PdfRenderService.cs
  Services/PdfSignService.cs
  Services/CertificateService.cs
  Views/MainWindow.xaml
  Views/SignaturePlacementOverlay.xaml
  Views/AboutWindow.xaml
  ViewModels/...
installer/PdfPotpis.iss        # Inno Setup
docs/plans/...
```

## Features (order of build)

1. **Scaffold** — WPF net9.0-windows, menus: Otvori / Sačuvaj / Sačuvaj kao / Potpiši / O aplikaciji
2. **Open + secure view** — load PDF bytes; render pages with Docnet; show in scroll viewer (no JS host)
3. **Save / Save As** — write current PDF bytes to path; track dirty/path state
4. **Certificate pick** — filter CurrentUser My store for digital-signature capable certs; `X509Certificate2UI` picker
5. **Sign + visual stamp** — place appearance (ime, prezime, ID); embed CMS signature; live drag preview on page
6. **About** — Serbian copy: sve lokalno, ništa se ne šalje/čuva na server
7. **Installer** — Inno Setup: welcome → choose folder → progress → finished validation

## Signing flow

1. User clicks Potpiši → enter placement mode (default stamp size, drag on page)
2. Confirm → cert picker (Windows UI) → PIN via CSP/CNG if smart card
3. Sign with non-exportable private key via iText external signature adapter
4. Reload viewer from signed bytes; enable Sačuvaj / Sačuvaj kao

## Out of scope (MVP)

- macOS/Linux
- Cloud/timestamp TSA (optional later)
- Multi-signer workflows
- Editing PDF content beyond signature

## Done when

- Open, view, save, save-as work
- Sign with Windows/smart-card cert adds embedded sig + visible stamp at chosen position
- About in Serbian states local-only
- Installer wizard installs to chosen folder and reports success

## Status (2026-08-04)

Implemented MVP:

- WPF app `src/PdfPotpis` (.NET 9)
- Secure PDFium preview, open/save/save-as, drag stamp + cert picker + CMS sign
- Serbian About dialog
- Two-step WPF installer `src/PdfPotpis.Installer` (+ optional Inno script)
- Build: `scripts/build-installer.ps1`
