SELECT DISTINCT META(b).id AS docId, b.fileName, b.jsonPayload.identifier.`value` AS prescriptionId
FROM bundles AS b
UNNEST b.jsonPayload.entry AS e
LET r = e.resource
WHERE r.resourceType = "Medication"
  AND ANY c IN r.code.coding
      SATISFIES c.`system` = "http://fhir.de/CodeSystem/ifa/pzn" AND c.code = "06313728" END
  AND STR_TO_MILLIS(b.jsonPayload.timestamp) >= STR_TO_MILLIS("2025-09-01T00:00:00+02:00")
  AND STR_TO_MILLIS(b.jsonPayload.timestamp) <  STR_TO_MILLIS("2025-11-01T00:00:00+02:00");