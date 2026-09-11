# TASKS — Document Redaction service

## In progress: position-preserving PDF output — [#8](https://github.com/wloescher/DocumentRedaction/issues/8)

Branch: `feature/8-pdf-layout`

Regenerated PDFs reflowed every block as prose: bullets stacked apart from their items, headings
merged with the next line, and dense pages spilled onto extra pages. Every word is now drawn where
PdfPig found it, and redacted spans become labelled black boxes.

- [x] 1. Page model: words with baseline, box, point size, bold/italic and font family class per page; blocks and lines from Docstrum for detection
- [x] 2. Letter-level redaction mapping: detection spans → covered letters (ink plus advance); uncovered runs stay as text with a trailing space glyph so the text layer keeps word boundaries; covered runs become one box per line with the placeholder label shrunk to fit, falling back to the kind label, then no label
- [x] 3. SVG rendering per page at the original size (QuestPDF `Svg`), one text element per glyph at its source position, text escaped via XML, generic font families, every glyph rotated along its own baseline so rotated pages and slanted lines keep their direction
- [x] 4. Tests: positions/sizes/styles preserved within tolerance, page count never grows, bullets stay beside items, keyword survives when its value shares the word, redaction box covers the original span, rotated page, font family mapping; existing text-based tests still pass
- [x] 5. Docs: README limitations and how-it-works, ENDUSER output description, TASKS.md
- [x] 6. Review gate: cleanup pass applied (dead code, block text computed once, shared label style, extractor tests for crop and slant); line scan hardened rendering (XML-safe glyph text, non-finite geometry skipped, render failures mapped to 422); removed-behaviour pass clean, `MaxTextCharacters` default lowered to 5 M because glyph geometry is now held per character

## Queue

- [#4](https://github.com/wloescher/DocumentRedaction/issues/4) Redact Word document properties and change-tracking authors
- [#5](https://github.com/wloescher/DocumentRedaction/issues/5) API key auth, rate limiting, QuestPDF license config
- [#6](https://github.com/wloescher/DocumentRedaction/issues/6) Heuristic person-name detection

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
