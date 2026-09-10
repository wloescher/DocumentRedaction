# TASKS — Document Redaction service

Tracking issue: [#1](https://github.com/wloescher/DocumentRedaction/issues/1)
Branch: `feature/1-initial-service`

## Goal
A .NET 10 service where a user uploads a Word, PDF, or text document, selects the categories to
redact (PII, HIPAA, Financial, Confidential), and downloads a redacted copy. Blazor Server UI plus
a REST API in one host. Pattern-based detection with a pluggable detector interface. Open-source
libraries only. Nothing persisted; document content never logged.

## Tasks
- [x] 1. Solution scaffold: `global.json`, `Directory.Build.props`, `.editorconfig`, sln, 3 src + 3 test projects, README, TASKS.md
- [x] 2. Core model: `RedactionCategory` (flags), `InformationKind`, `Detection`, `RedactionOptions`, `RedactionReport`
- [x] 3. Core detectors: `IDetector` + regex/checksum detectors for every kind; validators (Luhn, ABA, IBAN mod-97, NPI, DEA); `DetectorRegistry`
- [x] 4. Core engine: `TextRedactor` — run detectors, resolve overlaps (longest, then priority), splice placeholders, produce report
- [x] 5. Core tests: positive/negative/edge cases per kind, checksum boundaries, overlap resolution, custom terms, placeholder formatting
- [x] 6. Documents: `IDocumentProcessor` + `DocumentProcessorResolver`; `TextDocumentProcessor`
- [x] 7. Documents: `WordDocumentProcessor` (Open XML) — match across runs, splice runs preserving formatting; body, tables, headers, footers, footnotes, endnotes
- [x] 8. Documents: `PdfDocumentProcessor` — PdfPig extract → redact → QuestPDF regenerate; reject no-text-layer PDFs
- [x] 9. Documents tests: fixtures; round-trip assertions; error paths (corrupt file, unsupported type, empty PDF)
- [x] 10. Web: minimal API `POST /api/redact`, `GET /api/categories`; size limit; ProblemDetails errors; OpenAPI
- [x] 11. Web: Blazor Server page — upload, category/kind checkboxes, custom terms, redact, download, summary; ETA/total time via shared `FormatDuration`
- [x] 12. Web tests: `WebApplicationFactory` integration tests
- [ ] 13. Docs: README, ENDUSER.md, TASKS.md final state; PR with `Closes #1`

## Decisions
- Target `net10.0`; SDK lives at `~/.dotnet` (pinned via `global.json`).
- Detection kinds map to one or more categories; selecting HIPAA includes PII kinds.
- Overlap resolution: longest match wins, then kind priority.
- Placeholder default `[REDACTED-<KIND>]`, configurable.
- PDF output is regenerated from extracted text (layout simplified); scanned PDFs rejected.
- Personal names are not detected without NER; custom terms cover them for now.

## Follow-ups (out of scope for #1)
- NER-based name/address detection (e.g. Azure AI Language) behind `IDetector`.
- Layout-preserving PDF redaction.
- Authentication / rate limiting for the API.
