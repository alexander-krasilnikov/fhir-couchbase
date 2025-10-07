using System;
using System.ComponentModel.DataAnnotations;

namespace FhirCouchbaseDemo.Web.Models;

public class CouchbaseSettings : IEquatable<CouchbaseSettings?>
{
    [Required]
    [Display(Name = "Connection string")]
    public string ConnectionString { get; set; } = "couchbase://localhost";

    [Required]
    [Display(Name = "Username")]
    public string Username { get; set; } = "Administrator";

    [Required]
    [Display(Name = "Password")]
    public string Password { get; set; } = "password";

    [Required]
    [Display(Name = "Bucket name")]
    public string BucketName { get; set; } = "fhir-prescriptions";

    [Required]
    [Display(Name = "Scope name")]
    public string ScopeName { get; set; } = "_default";

    [Required]
    [Display(Name = "Collection name")]
    public string CollectionName { get; set; } = "prescriptions";

    public CouchbaseSettings Clone() => new()
    {
        ConnectionString = ConnectionString,
        Username = Username,
        Password = Password,
        BucketName = BucketName,
        ScopeName = ScopeName,
        CollectionName = CollectionName
    };

    public bool Equals(CouchbaseSettings? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(ConnectionString, other.ConnectionString, StringComparison.Ordinal) &&
               string.Equals(Username, other.Username, StringComparison.Ordinal) &&
               string.Equals(Password, other.Password, StringComparison.Ordinal) &&
               string.Equals(BucketName, other.BucketName, StringComparison.Ordinal) &&
               string.Equals(ScopeName, other.ScopeName, StringComparison.Ordinal) &&
               string.Equals(CollectionName, other.CollectionName, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is CouchbaseSettings other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(ConnectionString, Username, Password, BucketName, ScopeName, CollectionName);
}
