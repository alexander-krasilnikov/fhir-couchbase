using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using FhirCouchbaseDemo.Web.Models;

namespace FhirCouchbaseDemo.Web.ViewModels;

public class UploadPageViewModel
{
    [Display(Name = "FHIR XML files"), Required]
    public List<IFormFile> Files { get; set; } = new();

    public UploadResult? Result { get; set; }
}
