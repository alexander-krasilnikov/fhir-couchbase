using System;
using System.ComponentModel.DataAnnotations;

namespace FhirCouchbaseDemo.Web.Models;

public class S3Settings : IEquatable<S3Settings?>
{
    [Display(Name = "AWS access key ID")]
    public string AccessKeyId { get; set; } = string.Empty;

    [Display(Name = "AWS secret access key")]
    public string SecretAccessKey { get; set; } = string.Empty;

    [Display(Name = "Region")]
    public string Region { get; set; } = "us-east-1";

    [Display(Name = "Bucket name")]
    public string BucketName { get; set; } = string.Empty;

    [Display(Name = "Endpoint URL")]
    public string? EndpointUrl { get; set; }

    [Display(Name = "Use path-style addressing")]
    public bool ForcePathStyle { get; set; } = true;

    [Display(Name = "Default prefix filter")]
    public string? DefaultPrefix { get; set; }

    public S3Settings Clone() => new()
    {
        AccessKeyId = AccessKeyId,
        SecretAccessKey = SecretAccessKey,
        Region = Region,
        BucketName = BucketName,
        EndpointUrl = EndpointUrl,
        ForcePathStyle = ForcePathStyle,
        DefaultPrefix = DefaultPrefix
    };

    public bool Equals(S3Settings? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(AccessKeyId, other.AccessKeyId, StringComparison.Ordinal) &&
               string.Equals(SecretAccessKey, other.SecretAccessKey, StringComparison.Ordinal) &&
               string.Equals(Region, other.Region, StringComparison.Ordinal) &&
               string.Equals(BucketName, other.BucketName, StringComparison.Ordinal) &&
               string.Equals(EndpointUrl, other.EndpointUrl, StringComparison.Ordinal) &&
               ForcePathStyle == other.ForcePathStyle &&
               string.Equals(DefaultPrefix, other.DefaultPrefix, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is S3Settings other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(AccessKeyId, SecretAccessKey, Region, BucketName, EndpointUrl, ForcePathStyle, DefaultPrefix);
}
