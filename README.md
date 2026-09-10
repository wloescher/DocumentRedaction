# Document Redaction

A .NET 10 service that removes PII, HIPAA/PHI, financial, and confidential information from
Word (.docx), PDF, and plain-text documents. Upload a document, choose what to redact, download
the redacted copy. Nothing is persisted and document content is never logged.

Tracking issue: [#1](https://github.com/wloescher/DocumentRedaction/issues/1). Work log: [TASKS.md](TASKS.md).

## Layout

```
DocumentRedaction.slnx
  src/DocumentRedaction.Core        detection model, detectors, text redaction engine (no external deps)
  src/DocumentRedaction.Documents   text / Word (Open XML SDK) / PDF (PdfPig + QuestPDF) processors
  src/DocumentRedaction.Web         Blazor Server page + minimal REST API
  tests/*.Tests                     xUnit v3 test projects, one per source project
```

## Prerequisites

.NET 10 SDK. `global.json` pins the 10.0 feature band. If `dotnet --version` reports 8.x, your
PATH puts `/usr/local/share/dotnet` ahead of `~/.dotnet`; either reorder PATH or call
`~/.dotnet/dotnet` directly.

## Build and test

```bash
dotnet build
```

```bash
dotnet test
```

## Run

```bash
dotnet run --project src/DocumentRedaction.Web
```

Then open the URL printed in the console. The API is documented at `/openapi/v1.json`.
