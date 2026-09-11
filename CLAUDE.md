# DocumentRedaction — project notes for Claude

.NET 10 solution (`DocumentRedaction.slnx`). The SDK lives at `~/.dotnet`; use `~/.dotnet/dotnet`
if plain `dotnet` resolves to 8.x. `dotnet test` runs under Microsoft.Testing.Platform (opted in
via `global.json`), so pass project/solution selectors as `--project` / `--solution` and test
filters after `--` (for example `-- --filter-method "*Iban*"`).

## Commands

```bash
~/.dotnet/dotnet build
~/.dotnet/dotnet test
~/.dotnet/dotnet run --project src/DocumentRedaction.Web --launch-profile http
```

## Conventions

- `Directory.Build.props` turns on nullable, warnings-as-errors and `latest-recommended` analyzers;
  fix analyzer findings rather than suppressing them (the few suppressions live in `.editorconfig`).
- Package versions are central in `Directory.Packages.props`.
- Core has no external dependencies. Document-format libraries stay in Documents. DI registration
  is `AddDocumentRedaction()` in Documents.
- Every `InformationKind` needs a catalog entry in `InformationKinds`, a detector registered in
  `DetectorRegistry`, and tests for positives, negatives and checksum edges. A Core test fails
  if a text kind has no detector. A detector that widens to the surrounding sentence must set
  `WidensToSentence` on its catalog entry and stop at line breaks; the PDF processor relies on
  both when it joins block lines to catch wrapped identifiers. Kinds flagged `MetadataOnly`
  (document author) have no detector: processors redact them from document structure.
- `PersonNameDetector` is heuristic: honorific, title, form-label, salutation and given-name-list
  rules. The list is the embedded `Detectors/Builtin/Resources/GivenNames.txt`; put a new name in
  the section after `## ambiguous` when it is also an ordinary word. Words that end a name, and
  words a name can never start with, are the arrays at the top of the detector; street suffixes
  come from `StreetAddressDetector`. Its priority sits after every pattern kind and after custom terms.
- Detections carry matched text for tests only. Never log detection text or document content.
- Test fixtures are built in memory (`tests/DocumentRedaction.Tests.Fixtures`); do not add binary
  .docx/.pdf files to the repo.
- Keyword-anchored detectors report only the `value` capture group so the keyword stays in the
  document.
- Parsing caps live in `DocumentLimits` (Documents) and are bound from `Redaction:Limits`. Web
  reads settings only through `IOptions<RedactionSettings>`; never read `builder.Configuration`
  eagerly in `Program.cs`, because test hosts add configuration after that point.
- The `/api` group carries the API-key endpoint filter and the `api` rate-limit policy; new API
  routes go in that group so they inherit both. The Blazor page calls the service in-process
  and is neither gated nor throttled. Never log or echo API key values.
- Web integration tests that consume rate-limit budget or need their own settings build a
  `ConfiguredFixture` per test; the shared `RedactionApiFixture` sets a very high permit limit.

## Work tracking

Issue #1 tracked the initial build (merged in PR #2). Each follow-up has its own issue, branch
and PR: #3 PDF parsing caps (PR #7), #8 position-preserving PDF output (PR #9), #4 Word
metadata (PR #10), #5 API key and rate limiting (PR #11), #6 person-name detection (PR #12). TASKS.md
holds the checklist for the issue in progress plus the queue. Reference the issue in commit
messages (`Refs #<n>`, `Closes #<n>`).
