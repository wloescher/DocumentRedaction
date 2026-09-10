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
  if a kind has no detector.
- Detections carry matched text for tests only. Never log detection text or document content.
- Test fixtures are built in memory (`tests/DocumentRedaction.Tests.Fixtures`); do not add binary
  .docx/.pdf files to the repo.
- Keyword-anchored detectors report only the `value` capture group so the keyword stays in the
  document.

## Work tracking

Issue #1 on GitHub tracks the initial build; TASKS.md holds the checklist. Reference the issue
in commit messages (`Refs #1`, `Closes #1`).
