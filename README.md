# Document Redaction

A .NET 10 service that removes PII, HIPAA/PHI, financial, and confidential information from
Word (.docx), PDF, and plain-text documents. Upload a document, choose what to redact, download
the redacted copy. Nothing is persisted and document content is never logged.

Tracking issue: [#1](https://github.com/wloescher/DocumentRedaction/issues/1). Work log: [TASKS.md](TASKS.md).
End-user guide: [ENDUSER.md](ENDUSER.md).

## How it works

1. The upload is buffered in memory and routed to a processor by extension (then content type).
2. The processor extracts text in units that keep formatting intact: the whole file for text,
   one paragraph at a time for Word (joined across runs), one layout block at a time for PDF.
3. The Core engine runs one detector per enabled kind, resolves overlaps (longest span wins,
   then kind priority), and substitutes placeholders such as `[REDACTED-SSN]`.
4. The processor writes the same format back and the response carries a per-kind count report.

### Categories and kinds

| Category | Kinds |
|---|---|
| PII | SSN, email, phone, IP address, passport, driver's license, street address |
| HIPAA / PHI | everything in PII plus MRN, health plan / Medicare beneficiary ID, NPI, DEA number, dates |
| Financial | credit card (Luhn), ABA routing number, IBAN (mod-97), SWIFT/BIC, account number |
| Confidential | whole sentences containing markers such as "confidential", "proprietary", "internal use only", "trade secret" |

Custom terms (names, project code words) can be added to any run and apply regardless of
category selection. Detection is pattern-based with checksum validation where a checksum
exists; kinds that would otherwise be ambiguous (passport, driver's license, MRN, account
number, SWIFT) require an introducing keyword such as "Passport:".

### Known limitations

- Personal names and free-form addresses are not detected without a named-entity model.
  Use custom terms for names. The `IDetector` interface is the seam for adding an NER provider.
- Dates under HIPAA are noisy (invoice dates get redacted too); untick the Date kind if needed.
- PDF output is regenerated from extracted text: page count and sizes survive, fonts, images and
  multi-column layout do not. Scanned PDFs with no text layer are rejected with a clear error.
- Bare 10-digit and 9-digit numbers are only reported as NPI or routing numbers when their
  checksum passes, so a small share of unrelated numbers can still be over-redacted.

## Layout

```
DocumentRedaction.slnx
  src/DocumentRedaction.Core          detection model, detectors, text redaction engine (no external deps)
  src/DocumentRedaction.Documents     text / Word (Open XML SDK) / PDF (PdfPig + QuestPDF) processors, service facade
  src/DocumentRedaction.Web           Blazor Server page + minimal REST API + OpenAPI
  tests/DocumentRedaction.Core.Tests
  tests/DocumentRedaction.Documents.Tests
  tests/DocumentRedaction.Web.Tests   WebApplicationFactory integration tests
  tests/DocumentRedaction.Tests.Fixtures  in-memory .docx / .pdf builders shared by the test projects
```

## Prerequisites

.NET 10 SDK. `global.json` pins the 10.0 feature band and opts `dotnet test` into
Microsoft.Testing.Platform. If `dotnet --version` reports 8.x, your PATH puts
`/usr/local/share/dotnet` ahead of `~/.dotnet`; either reorder PATH or call `~/.dotnet/dotnet`.

QuestPDF is used under its Community license, which is free for organisations under the
revenue threshold published at questpdf.com; review that before commercial deployment.

## Build, test, run

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet run --project src/DocumentRedaction.Web --launch-profile http
```

Then open http://localhost:5039. The OpenAPI document is at `/openapi/v1.json`.

## API

`GET /api/categories` lists categories and the kinds each covers.

`POST /api/redact` takes `multipart/form-data` and returns the redacted file as an attachment.
Counts by kind are in the `X-Redaction-Report` header (JSON) and `X-Redaction-Total`.

| Field | Meaning |
|---|---|
| `file` | The document (required). `.txt .md .csv .log .text .docx .pdf` |
| `categories` | `pii`, `hipaa`, `financial`, `confidential`, or `all`. Repeat the field or comma-separate. Default: none |
| `excludedKinds` | Kind names to skip, e.g. `Date`. Repeat or comma-separate |
| `customTerms` | Literal terms, one per line or one per repeated field |
| `placeholderFormat` | Composite format; `{0}` becomes the kind label. Default `[REDACTED-{0}]` |
| `customTermsAreCaseSensitive` | `true` / `false` |

`POST /api/redact/summary` takes the same form and returns only `{ fileName, contentType, sizeBytes, report }`.

```bash
curl -sS -o report-redacted.docx -D - \
  -F "file=@report.docx" -F "categories=pii,financial" -F "excludedKinds=Date" -F "customTerms=Jane Doe" \
  http://localhost:5039/api/redact
```

Errors use RFC 9457 problem details: 400 validation, 413 too large, 415 unsupported type,
422 corrupt, encrypted, or text-free document.

## Configuration

`Redaction:MaxUploadBytes` (default 25 MB) bounds the in-memory buffer and is enforced by the
API, the page, Kestrel and the form reader.

## Extending detection

Implement `IDetector` (or derive from `RegexDetector`), add the kind to `InformationKind` and
`InformationKinds`, and register the detector in `DetectorRegistry`. A test in Core fails if a
kind has no detector.
