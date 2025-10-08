SELECT b.id,
       b.fileName,
       b.primaryPzn,
       b.pznCodes,
       b.jsonPayload.timestamp
FROM bundles AS b
WHERE ANY v IN b.pznCodes SATISFIES v = "06313728" END
    AND STR_TO_MILLIS(b.jsonPayload.timestamp) >= STR_TO_MILLIS("2025-09-01T00:00:00+02:00")
    AND STR_TO_MILLIS(b.jsonPayload.timestamp) < STR_TO_MILLIS("2025-11-01T00:00:00+02:00");