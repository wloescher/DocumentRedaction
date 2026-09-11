# TASKS — Document Redaction service

## In progress: Word metadata — [#4](https://github.com/wloescher/DocumentRedaction/issues/4)

Branch: `feature/4-word-metadata`

Redacted .docx files still named people in the file properties, on comments and tracked changes,
and in the reviewer list, and embedded objects passed through silently.

- [x] 1. Core: `InformationKind.DocumentAuthor` (PII and HIPAA) flagged `MetadataOnly`, so it needs no text detector; catalog and registry tests
- [x] 2. Word: `WordMetadataRedactor` replaces creator, last editor, manager and every `w:author` in any package part (body, headers, footnotes, comments, styles, numbering, glossary; initials dropped) and removes the people part when the kind is selected; title, subject, keywords, description, company and string custom properties go through the text detectors
- [x] 3. Warnings: `ProcessedDocument`/`RedactedDocument` carry warnings; embedded objects (anywhere in the package) and imported HTML/RTF chunks or SmartArt are counted and reported; API report and summary include `warnings`; the page lists them
- [x] 4. Tests: every property path, exclusion and category selection, comment/insert/delete/format-change authors, headers, people part, embedded object untouched, placeholder format; existing comment test updated for the counted author
- [x] 5. Docs (README Word metadata section, ENDUSER, CLAUDE.md) and review gate: correctness pass found authors in styles/numbering/glossary unscrubbed and imported chunks/SmartArt unflagged (fixed by scanning every package part and a second warning); cleanup pass applied (author counter instead of synthetic detections, one source of truth for metadata kinds, fixture package-open helper, warning colour token, extra tests)

## Queue

- [#5](https://github.com/wloescher/DocumentRedaction/issues/5) API key auth, rate limiting, QuestPDF license config
- [#6](https://github.com/wloescher/DocumentRedaction/issues/6) Heuristic person-name detection

## Done: position-preserving PDF output — [#8](https://github.com/wloescher/DocumentRedaction/issues/8), PR #9

Every glyph drawn where PdfPig found it, labelled black boxes over redacted spans, rotated and slanted text kept. 522 tests at merge.

## Done: parsing caps — [#3](https://github.com/wloescher/DocumentRedaction/issues/3), PR #7

`DocumentLimits` shared by Word and PDF; PdfPig decoders bounded before they run; settings through validated options; PDF block lines joined with newlines plus a wrapped-token pass. 474 tests at merge.

## Done: initial service — [#1](https://github.com/wloescher/DocumentRedaction/issues/1), PR #2

Blazor Server UI plus REST API in one .NET 10 host; pattern-based detection with a pluggable
detector interface; Word, PDF and text processors; 407 tests at merge.

### Decisions
- Target `net10.0`; SDK lives at `~/.dotnet` (pinned via `global.json`).
- Detection kinds map to one or more categories; selecting HIPAA includes PII kinds.
- Overlap resolution: longest match wins, then kind priority.
- Placeholder default `[REDACTED-<KIND>]`, configurable.
- PDF output is regenerated from extracted text (layout simplified); scanned PDFs rejected.
- Personal names are not detected without NER; custom terms cover them for now.
- Parsing caps are one `DocumentLimits` object shared by Word and PDF; exceeding one is 413. Decoders are bounded before they run wherever the format allows; LZW is held to its ratio.
