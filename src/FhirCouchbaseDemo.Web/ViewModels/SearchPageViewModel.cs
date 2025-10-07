using System;
using System.Collections.Generic;
using FhirCouchbaseDemo.Web.Models;

namespace FhirCouchbaseDemo.Web.ViewModels;

public class SearchPageViewModel
{
    public string? Pzn { get; set; }
    public DateTime? IssueDateFrom { get; set; }
    public DateTime? IssueDateTo { get; set; }

    public IReadOnlyList<PrescriptionRecord> Results { get; set; } = Array.Empty<PrescriptionRecord>();
    public bool HasSearched { get; set; }
}
