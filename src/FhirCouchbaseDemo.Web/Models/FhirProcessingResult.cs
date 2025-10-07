using System.Collections.Generic;

namespace FhirCouchbaseDemo.Web.Models;

public class FhirProcessingResult
{
    public bool Succeeded { get; set; }
    public PrescriptionRecord? Record { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
