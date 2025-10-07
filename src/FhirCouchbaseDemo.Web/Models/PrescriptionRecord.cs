using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace FhirCouchbaseDemo.Web.Models;

public class PrescriptionRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("pznCodes")]
    public List<string> PznCodes { get; set; } = new();

    [JsonPropertyName("primaryPzn")]
    public string? PrimaryPzn { get; set; }

    [JsonPropertyName("issueDate")]
    public DateTime? IssueDate { get; set; }

    [JsonPropertyName("uploadedAt")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("jsonPayload")]
    public JToken? JsonPayload { get; set; }

    [JsonPropertyName("rawXml")]
    public string RawXml { get; set; } = string.Empty;
}
