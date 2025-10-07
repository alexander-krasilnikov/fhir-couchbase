using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FhirCouchbaseDemo.Web.Models;

namespace FhirCouchbaseDemo.Web.ViewModels;

public class S3ImportViewModel : IValidatableObject
{
    [Display(Name = "AWS access key ID"), Required]
    public string AccessKeyId { get; set; } = string.Empty;

    [Display(Name = "AWS secret access key"), Required, DataType(DataType.Password)]
    public string SecretAccessKey { get; set; } = string.Empty;

    [Display(Name = "Region"), Required(ErrorMessage = "Specify the AWS region (for MinIO use e.g. 'us-east-1').")]
    public string Region { get; set; } = "us-east-1";

    [Display(Name = "Bucket name"), Required]
    public string BucketName { get; set; } = string.Empty;

    [Display(Name = "Object keys (one per line)")]
    public string? ObjectKeys { get; set; }

    [Display(Name = "Prefix filter")]
    public string? Prefix { get; set; }

    [Display(Name = "Endpoint URL (leave empty for AWS S3)")]
    public string? EndpointUrl { get; set; }

    [Display(Name = "Use path-style addressing (recommended for MinIO)")]
    public bool ForcePathStyle { get; set; } = true;

    [Display(Name = "Max objects when using prefix")]
    [Range(1, 500, ErrorMessage = "Max objects must be between 1 and 500.")]
    public int MaxKeys { get; set; } = 25;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ObjectKeys) && string.IsNullOrWhiteSpace(Prefix))
        {
            yield return new ValidationResult(
                "Provide object keys or a prefix to select documents.",
                new[] { nameof(ObjectKeys), nameof(Prefix) });
        }
    }

    public S3ImportOptions ToOptions()
    {
        var options = new S3ImportOptions
        {
            AccessKeyId = AccessKeyId.Trim(),
            SecretAccessKey = SecretAccessKey.Trim(),
            Region = Region.Trim(),
            BucketName = BucketName.Trim(),
            Prefix = string.IsNullOrWhiteSpace(Prefix) ? null : Prefix.Trim(),
            EndpointUrl = string.IsNullOrWhiteSpace(EndpointUrl) ? null : EndpointUrl.Trim(),
            ForcePathStyle = ForcePathStyle,
            MaxKeys = MaxKeys
        };

        if (!string.IsNullOrWhiteSpace(ObjectKeys))
        {
            foreach (var line in ObjectKeys.Split('\n'))
            {
                var key = line.Trim();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    options.ObjectKeys.Add(key);
                }
            }
        }

        options.Normalize();
        return options;
    }
}
