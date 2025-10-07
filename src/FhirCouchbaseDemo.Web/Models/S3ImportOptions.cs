using System;
using System.Collections.Generic;

namespace FhirCouchbaseDemo.Web.Models;

public class S3ImportOptions
{
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public string BucketName { get; set; } = string.Empty;
    public List<string> ObjectKeys { get; set; } = new();
    public string? Prefix { get; set; }
    public string? EndpointUrl { get; set; }
    public bool ForcePathStyle { get; set; } = true;
    public int MaxKeys { get; set; } = 25;

    public void Normalize()
    {
        ObjectKeys = ObjectKeys ?? new List<string>();
        ObjectKeys.RemoveAll(key => string.IsNullOrWhiteSpace(key));
        for (var i = 0; i < ObjectKeys.Count; i++)
        {
            ObjectKeys[i] = ObjectKeys[i].Trim();
        }

        if (MaxKeys <= 0)
        {
            MaxKeys = 25;
        }
        else if (MaxKeys > 500)
        {
            MaxKeys = 500;
        }
    }

    public bool HasExplicitKeys => ObjectKeys.Count > 0;

    public Uri? TryGetEndpointUri()
    {
        if (string.IsNullOrWhiteSpace(EndpointUrl))
        {
            return null;
        }

        if (Uri.TryCreate(EndpointUrl, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        throw new FormatException($"Endpoint URL '{EndpointUrl}' is not a valid absolute URI.");
    }
}
