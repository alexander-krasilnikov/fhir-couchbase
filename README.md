# FHIR Couchbase Demo

FHIR e-prescription proof of concept that ingests Gematik eRezept bundles, enriches them with metadata, and stores the resulting documents in Couchbase for fast querying.

## Key capabilities
- Upload raw FHIR XML bundles and convert them to canonical JSON while keeping the original payload alongside extracted metadata (PZN codes, issue timestamps, file name).
- Persist documents in Couchbase and expose search over PZNs and date ranges backed by N1QL indexes tailored for the demo.
- Manage Couchbase credentials at runtime and verify connectivity via the built-in diagnostics page.
- Run locally with the .NET SDK or containerise with Podman/Docker for demos and experiments.

## Repository layout
- `src/FhirCouchbaseDemo.Web` – ASP.NET Core web app with Upload, Search, and Connection pages.
- `docs/` – supplementary documentation:
  - `requirements.md` – business background and proof-of-concept goals.
  - `deployment.md` – prerequisites, Couchbase bootstrap, local run, and container instructions.
  - `user-manual.md` – step-by-step guide for demo operators.
- `Dockerfile` – multi-stage image suitable for Podman or Docker.

## Getting started
1. Ensure the prerequisites are installed: .NET SDK 9.0+, Couchbase Server 7.6+, and optionally Podman/Docker if containerising (see `docs/deployment.md`).
2. Configure Couchbase credentials in `src/FhirCouchbaseDemo.Web/appsettings.json` or via environment variables.
3. Launch the application with `dotnet run --project src/FhirCouchbaseDemo.Web` and browse the surfaced URL (typically `https://localhost:5001`).
4. Follow the workflow in `docs/user-manual.md` to upload sample bundles (for example from the ABDA repository) and demonstrate search.

For infrastructure automation, container usage, and environment variable reference, consult `docs/deployment.md`.

## Additional resources
- Sample eRezept bundles: <https://github.com/DAV-ABDA/eRezept-Beispiele>
- High-level requirements and query scenarios: `docs/requirements.md`
