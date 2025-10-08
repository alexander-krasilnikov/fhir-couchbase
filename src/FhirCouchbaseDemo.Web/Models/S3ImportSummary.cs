namespace FhirCouchbaseDemo.Web.Models;

public class S3ImportSummary
{
    public int Discovered { get; set; }
    public int Imported { get; set; }
    public int Failed { get; set; }
}
