using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using FhirCouchbaseDemo.Web.Models;

namespace FhirCouchbaseDemo.Web.ViewModels;

public class UploadPageViewModel
{
    [Display(Name = "FHIR bundle files")]
    public List<IFormFile> Files { get; set; } = new();

    public UploadResult? Result { get; set; }
    public S3ImportViewModel S3Import { get; set; } = new();
}
