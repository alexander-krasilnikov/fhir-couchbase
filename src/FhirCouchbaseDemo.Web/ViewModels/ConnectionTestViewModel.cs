using FhirCouchbaseDemo.Web.Models;

namespace FhirCouchbaseDemo.Web.ViewModels;

public class ConnectionTestViewModel
{
    public CouchbaseSettings Settings { get; set; } = new();
    public CouchbaseTestResult? Result { get; set; }
    public string? SaveMessage { get; set; }
    public bool SaveSucceeded { get; set; }
    public string SaveAlertStyle { get; set; } = "success";
    public CouchbaseStructureStatus? StructureStatus { get; set; }
}
