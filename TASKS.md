# TASKS — Document Redaction service

## In progress: API key auth, rate limiting, QuestPDF license — [#5](https://github.com/wloescher/DocumentRedaction/issues/5)

Branch: `feature/5-api-key-rate-limit`

The `/api` endpoints are anonymous and unthrottled, and the QuestPDF license is hard-coded to Community.

- [x] 1. Settings: `Redaction:ApiKeys` (optional list), `Redaction:RateLimit` (`Enabled`, `PermitLimit`, `WindowSeconds`) and `Redaction:QuestPdfLicense`, validated at startup
- [x] 2. API key: endpoint filter on the `/api` group checks `X-Api-Key` against the configured keys (constant-time); no keys configured means open mode with a startup warning; missing or wrong key is a 401 problem response
- [x] 3. Rate limiting: fixed-window limiter on the `/api` group partitioned by valid API key, otherwise by client IP; 429 problem response with `Retry-After`
- [x] 4. QuestPDF license: Documents takes the license through `AddDocumentRedaction`; Web binds it from settings
- [x] 5. Tests: validator cases, open mode, missing/wrong/valid key, categories gated, 429 after the limit with per-key partitions, disabled limiter, license mapping, startup warning
- [x] 6. Docs (README configuration and API sections, ENDUSER, CLAUDE.md) and review gate: correctness pass found an undefined numeric `QuestPdfLicense` and an oversized window passing startup validation (both now rejected), a scalar `ApiKeys` value silently binding nothing (now fails startup), and non-ASCII or duplicate keys accepted; cleanup pass bound the limiter through options, moved QuestPDF globals into one helper, removed a duplicate "open" predicate and shared the problem-details test helpers

## Queue

- [#6](https://github.com/wloescher/DocumentRedaction/issues/6) Heuristic person-name detection

## Done: Word metadata — [#4](https://github.com/wloescher/DocumentRedaction/issues/4), PR #10

Creator, editors and every tracked-change or comment author redacted from the package; properties through the detectors; embedded objects and imported content reported as warnings. 545 tests at merge.

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
