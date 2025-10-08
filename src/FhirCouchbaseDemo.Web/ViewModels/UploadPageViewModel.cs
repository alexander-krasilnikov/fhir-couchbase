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
    public S3Settings? StoredS3Settings { get; set; }
    public string ActiveTab { get; set; } = "files";
    public S3ImportSummary? S3Summary { get; set; }

    public bool HasS3Configuration =>
        StoredS3Settings is not null &&
        !string.IsNullOrWhiteSpace(StoredS3Settings.AccessKeyId) &&
        !string.IsNullOrWhiteSpace(StoredS3Settings.SecretAccessKey) &&
        !string.IsNullOrWhiteSpace(StoredS3Settings.Region) &&
        !string.IsNullOrWhiteSpace(StoredS3Settings.BucketName);
}
