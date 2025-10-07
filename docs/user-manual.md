# User manual

This guide explains how to use the FHIR prescriptions web demo as a solutions engineer when showcasing Couchbase.

## 1. Overview

The application accepts FHIR eRezept bundles (XML), converts them to canonical FHIR JSON, and saves the resulting documents in Couchbase together with key metadata (PZN codes, issue timestamp, original XML).

Once stored, the **Search** page lets you retrieve prescriptions using PZN codes and date ranges.

## 2. Upload workflow

1. Open **Upload** in the navigation bar.
2. Drag one or more XML files into the drop zone (or click **Browse files** to pick them manually). The demo accepts bundles directly from the ABDA sample repository.
3. Click **Upload to Couchbase**.

### Upload result panel

After submission the page shows:

- **Documents stored** – number of bundles converted to JSON and persisted.
- **Warnings** – metadata that could not be extracted (for example missing PZN or timestamp). The JSON is still stored.
- **Failed uploads** – files that could not be parsed or converted.
- **Stored document grid** – quick view of filename, extracted PZNs, and issue date.

The JSON payloads and raw XML are persisted in the same Couchbase document. The `PrescriptionRecord` schema is:

```json
{
  "id": "guid",
  "fileName": "bundle.xml",
  "pznCodes": ["04351736"],
  "primaryPzn": "04351736",
  "issueDate": "2025-09-01T08:30:00Z",
  "uploadedAt": "2025-01-12T09:21:43.039Z",
  "jsonPayload": { "resourceType": "Bundle", "type": "document", "entry": [ ... ] },
  "rawXml": "<Bundle ... />"
}
```

## 3. Searching prescriptions

1. Open **Search** in the navigation bar.
2. Fill in one or more filters:
   - **PZN** – exact match against all codes associated with the document.
   - **Issue date (from/to)** – filters the `issueDate` field extracted from the bundle.
3. Click **Search** to run the N1QL query.

### Interpreting results

- Results are sorted by upload time (latest first).
- Expand **Show JSON payloads** to view the converted bundle JSON exactly as stored in Couchbase. This is useful when demonstrating how binary XML can be surfaced to analytics systems.

## 4. Couchbase connection & configuration

Use the **Connection** page to inspect and update the credentials persisted by the demo. Edit the connection string, credentials, or bucket/scope/collection values and click **Save settings** (they are written to `App_Data/couchbase-settings.json`).

Press **Run connectivity test** afterwards to execute a `PING` from the SDK with the saved configuration:

- Success responses show the latency and availability per service.
- Failures surface the underlying SDK error message to speed up troubleshooting during demos.

## 5. Sample demo script

1. Launch the web app and head to **Connection** to prove the Couchbase cluster is reachable.
2. Navigate to **Upload**, select two XML bundles from the ABDA repo, and upload them.
3. Switch to **Search**, filter by the PZN contained in the sample bundle (`04351736`) and limit the issue date to September 2025. Highlight that the query leverages metadata extracted during ingestion.
4. Expand the JSON payloads to illustrate how Couchbase keeps both the raw data and enriched metadata in a single flexible document model.

