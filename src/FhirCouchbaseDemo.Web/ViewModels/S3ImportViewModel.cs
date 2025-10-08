using System.ComponentModel.DataAnnotations;
using FhirCouchbaseDemo.Web.Models;

namespace FhirCouchbaseDemo.Web.ViewModels;

public class S3ImportViewModel
{
    [Display(Name = "Prefix filter (optional)")]
    public string? Prefix { get; set; }

    [Display(Name = "Max objects when using prefix")]
    [Range(1, 500, ErrorMessage = "Max objects must be between 1 and 500.")]
    public int MaxKeys { get; set; } = 25;

    public S3ImportOptions ToOptions(S3Settings settings)
    {
        if (settings is null)
        {
            throw new ValidationException("S3 settings are not configured.");
        }

        var options = new S3ImportOptions
        {
            AccessKeyId = settings.AccessKeyId,
            SecretAccessKey = settings.SecretAccessKey,
            Region = settings.Region,
            BucketName = settings.BucketName,
            EndpointUrl = string.IsNullOrWhiteSpace(settings.EndpointUrl) ? null : settings.EndpointUrl,
            ForcePathStyle = settings.ForcePathStyle,
            MaxKeys = MaxKeys,
            Prefix = string.IsNullOrWhiteSpace(Prefix) ? null : Prefix.Trim()
        };

        options.Normalize();
        return options;
    }
}
