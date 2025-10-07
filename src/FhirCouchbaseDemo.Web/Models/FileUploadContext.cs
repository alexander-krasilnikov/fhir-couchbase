using System.IO;

namespace FhirCouchbaseDemo.Web.Models;

public class FileUploadContext
{
    public string FileName { get; init; } = string.Empty;
    public Stream Content { get; init; } = Stream.Null;
}
