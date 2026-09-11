# Using Document Redaction

## Redact a document in the browser

1. Open the site and choose a file under **1. Document**. Word (.docx), PDF and plain-text files
   are accepted, up to the size shown next to the file picker.
2. Under **2. What to redact**, tick the categories you need. Expand a ticked category to
   untick individual kinds, for example leave dates alone under HIPAA.
3. Optionally type **custom terms**, one per line, such as a person's name or a project code
   word. They are removed even if no category is ticked. Tick the case-sensitive box if
   "Falcon" must not also match "falcon".
4. Optionally change the **placeholder format**. `{0}` is replaced by the kind label, so
   `<{0}>` produces `<SSN>`; `████` produces a plain black bar for everything.
5. Click **Redact document**. While it runs you see elapsed time and an estimate of the time
   remaining. When it finishes you see how many items were redacted, by kind.
6. Click **Download** to save the redacted copy. The file keeps the original name with
   `-redacted` added.

Nothing you upload is stored on the server or written to logs. If you refresh the page the
result is gone; run the redaction again.

## What gets redacted

| Category | Examples |
|---|---|
| PII | 123-45-6789, jane@example.com, (555) 123-4567, 192.168.1.1, "Passport: X12345678", "DL# S123-456-789", 123 Main Street |
| HIPAA / PHI | everything in PII plus "MRN: 00123456", "Member ID: XYZ123456", NPI 1234567893, DEA AB1234563, 01/02/1980, Jan 2, 1980 |
| Financial | 4111 1111 1111 1111, routing 021000021, GB82 WEST 1234 5698 7654 32, "SWIFT: DEUTDEFF", "Account No: 123456789012" |
| Confidential | the whole sentence containing "confidential", "proprietary", "internal use only", "trade secret", "do not distribute" |

Things the service cannot find on its own: people's names, free-form addresses in unusual
formats, and text inside images or scanned pages. Add names as custom terms, and run scanned
PDFs through OCR first.

## What the output looks like

- **Text files** keep their line endings and encoding; only the redacted spans change.
- **Word files** keep their formatting, tables, headers, footers, footnotes and comments. Links
  whose address contained sensitive data now point to `about:blank`.
- **PDF files** are redrawn word by word in the original positions, so the layout, bullets and
  page count stay as they were; fonts are replaced with a similar generic font, text is black,
  and images are dropped. Each redacted span is covered by a black box labelled with the
  placeholder, or just the kind (for example EMAIL) when the span is too short for the full
  label, or left unlabelled when it is shorter still. A confidentiality
  sentence is removed up to the line breaks around it, so in a bullet list only that bullet
  goes. A card or ID number inside such a sentence counts once, as the confidential statement.

## Common messages

| Message | What to do |
|---|---|
| Unsupported file type | Save the document as .docx, .pdf or .txt and try again. Legacy .doc is not supported. |
| The file is not a valid Word / PDF document | The file is corrupt or has the wrong extension. Re-export it from the original application. |
| The PDF is password-protected | Remove the password first. |
| The PDF contains no extractable text | The PDF is a scan. Run OCR (for example in Acrobat) to add a text layer, then upload the result. |
| The file exceeds the maximum upload size | Split the document or ask the administrator to raise the limit. |
| The PDF has N pages; the limit is M | Split the PDF into smaller files, or ask the administrator to raise the page cap. |
| The text in the PDF exceeds the limit of N characters | Split the PDF, or ask the administrator to raise the text cap. |
| A stream in the PDF exceeds (or could exceed) the decoded-size limit of N bytes / The PDF exceeds the total decoded-size limit | The file expands far beyond its size when opened. Re-export it from the original application, or ask the administrator to raise the cap. |
| The part '...' in the Word document is N bytes uncompressed; the limit is M | The document embeds something very large (usually an image or object). Compress or remove it, or ask the administrator to raise the cap. |
| Select at least one category or supply a custom term | Tick a category or add a custom term. |

## Using the API instead

Automations can post the same options to `/api/redact` and receive the file back; see the
README for the field list and a `curl` example.
