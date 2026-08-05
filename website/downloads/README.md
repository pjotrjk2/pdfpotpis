# downloads/

Filled by `scripts\prepare-release.ps1`:

- `PDFPotpis-Setup.exe` — single-file installer
- `PDFPotpis-Setup.exe.sha256`
- `PDFPotpis-Portable.exe` — single-file app (no install)
- `PDFPotpis-Portable.exe.sha256`

Checksums are also injected into `index.html`. Binaries are gitignored; stage them locally before deploying `website/`.
