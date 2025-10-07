using System;
using System.Collections.Generic;

namespace FhirCouchbaseDemo.Web.Models;

public class FhirMetadata
{
    public List<string> PznCodes { get; set; } = new();
    public string? PrimaryPzn { get; set; }
    public DateTime? IssueDate { get; set; }
    public List<string> Warnings { get; set; } = new();
}
