# Deployment guide

This document describes how to run the FHIR prescriptions demo locally and inside a Podman container. It also covers the Couchbase configuration required for the proof of concept.

## 1. Prerequisites

- .NET SDK 9.0+
- Couchbase Server 7.6 or newer (Enterprise or Community)
- Podman 4.7+ (or Docker as an alternative engine)
- Access to the sample FHIR bundles published by ABDA (optional): <https://github.com/DAV-ABDA/eRezept-Beispiele>

## 2. Couchbase bootstrap

1. Create a bucket dedicated to the demo:
   ```sql
   CREATE BUCKET `fhir-prescriptions` WITH {"ramQuotaMB": 256};
   ```
2. Create a collection to hold the uploaded prescriptions. The application defaults to `_default` scope/collection, but you can customise the names:
   ```sql
   CREATE SCOPE `fhir-prescriptions`.`_default`;
   CREATE COLLECTION `fhir-prescriptions`.`_default`.`prescriptions`;
   ```
3. Add the indexes that power the search UI:
   ```sql
   CREATE PRIMARY INDEX `idx_prescriptions_primary`
   ON `fhir-prescriptions`.`_default`.`prescriptions`;

   CREATE INDEX `idx_prescriptions_search`
   ON `fhir-prescriptions`.`_default`.`prescriptions`(
       issueDate,
       uploadedAt,
       DISTINCT ARRAY pzn FOR pzn IN pznCodes END
   );
   ```

> **Tip**: if you customise the scope or collection name, update `appsettings.json` or pass environment variables (see below) so the application uses the same location.

## 3. Local development run

1. Configure Couchbase credentials in `src/FhirCouchbaseDemo.Web/appsettings.json`:
   ```json
   "Couchbase": {
     "ConnectionString": "couchbase://<host>",
     "Username": "Administrator",
     "Password": "password",
     "BucketName": "fhir-prescriptions",
     "ScopeName": "_default",
     "CollectionName": "prescriptions"
   }
   ```
2. Start the ASP.NET Core app:
   ```bash
   dotnet run --project src/FhirCouchbaseDemo.Web
   ```
3. Browse to `https://localhost:5001` (or the URL shown in the console) and use the **Upload**, **Search**, and **Connection** pages.

## 4. Containerising with Podman

A multi-stage `Dockerfile` tuned for Podman is available at the repository root.

### Build the image
```bash
podman build -t couchbase-fhir-demo .
```

### Run the container

Expose the application on port 8080 and pass the Couchbase settings via environment variables (any `:` becomes `__` for ASP.NET Core configuration keys):

```bash
podman run \
  --name couchbase-fhir-demo \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Couchbase__ConnectionString=couchbase://host.docker.internal \
  -e Couchbase__Username=Administrator \
  -e Couchbase__Password=yourSecret \
  -e Couchbase__BucketName=fhir-prescriptions \
  -e Couchbase__ScopeName=_default \
  -e Couchbase__CollectionName=prescriptions \
  couchbase-fhir-demo
```

The container listens on port 8080. Adjust the connection string so that the container can reach your Couchbase cluster (for example, use the Podman machine IP or a bridge network).

### Optional: run Couchbase with Podman

To keep the demo self-contained, you can also start Couchbase Server with Podman:

```bash
podman run -d --name couchbase-server \
  -p 8091-8096:8091-8096 -p 11210-11211:11210-11211 \
  -e CB_USERNAME=Administrator \
  -e CB_PASSWORD=yourSecret \
  -e CLUSTER_NAME=fhir-demo \
  couchbase/server:community-7.6.2
```

> Remember to initialise the cluster using the Couchbase Web Console (`http://localhost:8091`) and create the bucket/collection/indexes before launching the application container.

## 5. Environment variable reference

| Variable | Purpose |
| --- | --- |
| `Couchbase__ConnectionString` | Location of the Couchbase cluster (`couchbase://` or `couchbases://`). |
| `Couchbase__Username` / `Couchbase__Password` | Administrator credentials or an RBAC user with KV + Query permissions for the target collection. |
| `Couchbase__BucketName` | Target bucket where documents are stored. |
| `Couchbase__ScopeName` / `Couchbase__CollectionName` | Scope and collection names (defaults to `_default`). |

## 6. Diagnostics & logging

- Use the **Connection** page to run a ping test from the application to Couchbase.
- Application logs default to `Information` level. Override with `Logging__LogLevel__Default` environment variable for more verbosity when debugging containers.
- For query troubleshooting, enable the Couchbase Query monitor (`Developer Tools > Query Workbench`) and inspect the generated N1QL statements.

