using System.Collections.Generic;

namespace FhirCouchbaseDemo.Web.Models;

public class S3DocumentLoaderResult
{
    public List<FileUploadContext> Files { get; } = new();
    public List<UploadFailure> Failures { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool AnySuccess => Files.Count > 0;
}
