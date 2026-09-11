# TASKS — Document Redaction service

## In progress

Nothing. Every follow-up from the initial build is merged; open a new issue for the next piece of work.

## Done: heuristic person-name detection — [#6](https://github.com/wloescher/DocumentRedaction/issues/6), PR #12

Honorific, title, form-label, salutation and given-name-list rules; Unicode words; stop words shared with the address detector. 705 tests at merge. Checked against a real resume: no false positives, one miss (a given name not on the list).

## Done: post-review performance & correctness fixes — [#14](https://github.com/wloescher/DocumentRedaction/issues/14)

Three fixes from a whole-repo `/code-review`, no behaviour change (705 tests pass):

- [x] Core: `TextRedactor` caches the resolved detector set per `RedactionOptions` (was recomputing `EffectiveKinds()`/`NormalizedCustomTerms` once or twice per PDF layout block)
- [x] Web: API-key authentication memoized in `HttpContext.Items` (was running the `FixedTimeEquals` sweep twice per `/api` request — endpoint filter plus rate-limit partition)
- [x] Web: Blazor `Home.razor` progress-timer tick guarded against a disposal race (`volatile _disposed` flag, awaited `try/catch (ObjectDisposedException)`)

## Done: API key auth, rate limiting, QuestPDF license — [#5](https://github.com/wloescher/DocumentRedaction/issues/5), PR #11

Optional `X-Api-Key` gate and fixed-window throttle on `/api`, configurable QuestPDF tier; startup validation for every setting. 609 tests at merge.

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
- Personal names are found by heuristics (#6); custom terms cover the misses and an NER provider can plug into `IDetector`.
- Parsing caps are one `DocumentLimits` object shared by Word and PDF; exceeding one is 413. Decoders are bounded before they run wherever the format allows; LZW is held to its ratio.
