# Proof of Concept: FHIR Prescriptions with Couchbase

## Current business case
- A regional prescription processor handles approximately 14,000,000 digital prescriptions per month based on the Gematik eRezept project.
- Incoming prescriptions arrive as bundles of FHIR XML documents, currently stored in a MinIO (S3-compatible) object store.
- Business model data is extracted from the FHIR XML bundles and persisted in MSSQL using a relational schema.

### FHIR bundle composition
1. **Prescription** – issued by the physician, contains the prescribed medication.
2. **Delivery** – issued by the pharmacist, contains delivered medication and delivery details.
3. **Receipt** – issued by the Telematik infrastructure, contains signature data for fraud prevention.

## Proof of concept goal
Evaluate storing FHIR XML bundles directly in Couchbase and querying them, instead of expanding the complex relational MSSQL model.

### Requirement 01 – Query extended FHIR documents
- Documents must be queryable.
- Example query: *Find all documents with Pharma-Zentralnummer (PZN) `04351736` (namespace `http://fhir.de/CodeSystem/ifa/pzn`) and issued between 01.09.2025 and 30.09.2025 (`timestamp`).*
- Challenge: the eRezept prescription is based on the FHIR standard, but many extensions use custom namespaces, so developers must crawl the XML structure and namespaces.

### Requirement 02 – Persist workflow processing information
- FHIR documents are processed by an operational workflow that adds additional content.
- The extra workflow content must be linked back to the originating FHIR prescription.
- Example query: *Return all workflow messages for PZN `04351736` created between 01.09.2025 and 30.09.2025 where the prescription status is "containing Errors".*

#### Workflow metadata to persist
- **Prescription status:** In process, billable, currently billing, billing completed, containing errors, canceled.
- **Processing messages:** capture message text, category, severity.

> Current demo focus: The web UI now concentrates on ingesting FHIR bundles and extracting PZN/issued dates; workflow statuses and messages are not captured.

### Sample data reference
Sample eRezept bundles are published by ABDA: <https://github.com/DAV-ABDA/eRezept-Beispiele>

