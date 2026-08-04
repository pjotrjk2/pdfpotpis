# Website landing page

## Goal

Single-page static HTML landing site in `website/` for PDFPotpis: SEO-friendly, free download CTA to the Windows installer. Minimal or no JavaScript.

## Approach

- One `index.html` + `styles.css` (no build step, no JS frameworks)
- Serbian primary copy (matches app About text); light English meta where useful for search
- Download CTA → configurable installer URL (GitHub Releases or `/downloads/PDFPotpis-Setup.exe`)
- SEO: title/description, Open Graph, canonical, `robots.txt`, `sitemap.xml`, semantic headings, FAQ section
- Host anywhere static (Cloudflare Pages, Netlify, GitHub Pages)

## Layout (one page)

1. Hero — brand PDFPotpis, one headline, one sentence, download CTA
2. Kako radi — 3 short steps (open → place stamp → sign with cert)
3. Privatnost — local-only message
4. FAQ — smart card, Windows, free/AGPL
5. Footer — license note, links

## Out of scope

- Blog, CMS, analytics, multi-page app
- Hosting installer binary in git (use Releases / CDN)

## Done when

- `website/index.html` opens locally and reads as one branded composition
- Download button points at a clear installer URL placeholder
- robots + sitemap present; no required JavaScript

## Status (2026-08-04)

Implemented: `website/index.html` + `styles.css`, favicon/og SVG, robots, sitemap, `downloads/` placeholder. FAQ uses native `<details>`. JSON-LD only (no runtime JS).
