using System.Collections.Generic;

namespace FhirCouchbaseDemo.Web.Models;

public class UploadResult
{
    public List<PrescriptionRecord> StoredRecords { get; set; } = new();
    public List<UploadFailure> Failures { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class UploadFailure
{
    public string FileName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
