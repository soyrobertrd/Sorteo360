# ADR-0024 — Server-side PDFs are built with PDFsharp-MigraDoc

- Status: Accepted
- Date: 2026-09-25
- Follows from: ADR-0022 (the notarial record is generated once by the backend and stored)

## Context

ADR-0022 moved the raffle's notarial record out of the browser, where the mock rebuilt it with jsPDF
every time it was opened, and into events-service, which generates it once at `complete` and stores
it through the file seam. The platform had no .NET PDF library, and package versions are governed at
the root (`Directory.Packages.props`), so choosing one is a platform decision rather than an
events-service one.

The document is a legal record: a results section grouped by round, a chronological draw log, a list
of manual decisions and an annex naming every participant, which can run to several pages for a
400-person drum. It needs tables, repeating headers and footers, page breaks, and "página X de Y".
It has to render Spanish accents correctly inside a Linux container.

## Decision

**PDFsharp with MigraDoc (`PDFsharp-MigraDoc`, MIT), pinned in the root `Directory.Packages.props`.**
MigraDoc supplies the document model: sections, tables, headers and footers, and page fields.
PDFsharp renders it.

- **It is an Infrastructure dependency only.** Core defines the seam that turns a completed raffle into
  bytes, and the library name never reaches Core. This is the same discipline ADR-0014 applies to
  Nextcloud.
- **Fonts ship in the image.** On Linux PDFsharp resolves no system fonts, so the service registers
  a font resolver over one bundled, open-licensed (SIL OFL) family. The font file is part of what the
  record looks like and changes only deliberately.
- **Nothing moves into the chassis yet.** Convocatorias and pagos may want PDFs later. Per ADR-0012
  a shared type is extracted when a second consumer exists, not guessed from the first. Until then a
  second service references the package from the root and writes its own document.

## Alternatives rejected

- **QuestPDF.** It has the best API of the candidates, but its Community licence excludes MITUR by
  name: *"Public-sector entities and government agencies … are not eligible for the Community
  License"* (questpdf.com/license/guide.html, checked 2026-09-25). A paid licence is a procurement
  question this project cannot answer for a single document. Reconsider it if MITUR ever buys one.
- **iText.** It is AGPL or commercial, so it is out for the same reason.
- **Headless Chromium (HTML to PDF).** It gives the nicest output from HTML templates, but it puts a
  browser into the events-service image: hundreds of megabytes of image and a large runtime memory
  footprint, on a shared on-premises host (16 GB for dev, stage, Dokploy and Nextcloud) whose
  memory was already tuned in PGTI-6 and PGTI-24.
- **Keep jsPDF in the browser.** ADR-0022 already rejected this. A document a notary signs must not
  be rebuilt from live data every time it is opened.

## Consequences

- Layout is written as imperative C#, not as a template. A copy or layout change is a code change,
  reviewed like one. A golden test on the extracted text keeps that reviewable.
- Swapping the library later changes how every future record looks. It does not change the records
  already stored, because each one is an immutable stored file.
