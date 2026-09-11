# TASKS — Document Redaction service

## In progress: parsing caps — [#3](https://github.com/wloescher/DocumentRedaction/issues/3)

Branch: `feature/3-pdf-limits`

Guard against decompression bombs and oversized documents. Word already capped part size;
PDF had no guard at all, and the host read its settings before test configuration applied.

- [x] 1. `DocumentLimits` (max decoded bytes per stream/part, total decoded bytes per PDF, text characters, PDF pages) with validation; DI via `AddDocumentRedaction(factory)`
- [x] 2. PDF: `BoundedFilterProvider` bounds every PdfPig decoder before it runs (Flate 1032:1 fast path then a counting trial inflate that mirrors PdfPig's two-byte skip; LZW and RunLength by ratio; CCITT and predictor rows from the dictionary), keeps a per-document budget, fails fast after the first rejection; rejections are checked after `Open`, per page and mapped to 413
- [x] 3. Word: zip directory checked against the same per-part cap before opening; XML reader cap kept as backstop
- [x] 4. Web: settings bound through `IOptions` with `ValidateOnStart` (upload limit bounded above by the array ceiling); Kestrel and form limits configured through options; `Redaction:Limits` section
- [x] 5. Tests: limits validation, filter provider (zlib, PdfPig-style header skip, raw deflate, fast path accounting, predictor, LZW, RunLength, CCITT, total budget, fail-fast), PDF caps at and over the limit, multi-page and xref-stream bombs, Word part cap, startup validation, config binding
- [x] 6. Docs: README configuration table, ENDUSER messages, CLAUDE.md conventions
- [x] 7. Review gate: 10 findings (Flate measure not mirroring PdfPig, LZW/predictor/CCITT unbounded, no total budget, swallowed rejection after `Open`, cancellation rewrapped, `MaxCharacters` semantics, upload ceiling, fast path, fail-fast, dead page guard) all fixed

## Queue

- [#4](https://github.com/wloescher/DocumentRedaction/issues/4) Redact Word document properties and change-tracking authors
- [#5](https://github.com/wloescher/DocumentRedaction/issues/5) API key auth, rate limiting, QuestPDF license config
- [#6](https://github.com/wloescher/DocumentRedaction/issues/6) Heuristic person-name detection
- Layout-preserving PDF redaction (no issue yet; deferred)

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
