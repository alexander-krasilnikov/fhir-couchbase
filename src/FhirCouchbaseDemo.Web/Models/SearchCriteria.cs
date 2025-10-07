using System;

namespace FhirCouchbaseDemo.Web.Models;

public class SearchCriteria
{
    public string? Pzn { get; set; }
    public DateTime? IssueDateFrom { get; set; }
    public DateTime? IssueDateTo { get; set; }
}
