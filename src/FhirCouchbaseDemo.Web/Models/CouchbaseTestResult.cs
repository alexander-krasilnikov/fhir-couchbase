using System.Collections.Generic;

namespace FhirCouchbaseDemo.Web.Models;

public class CouchbaseTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<string> Details { get; init; } = new();
}
