using System;
using System.Collections.Generic;

namespace FhirCouchbaseDemo.Web.Models;

public class CouchbaseStructureStatus
{
    private readonly string _scopeName;
    private readonly string _collectionName;

    public CouchbaseStructureStatus(CouchbaseSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        BucketName = settings.BucketName;
        _scopeName = string.IsNullOrWhiteSpace(settings.ScopeName) ? "_default" : settings.ScopeName;
        _collectionName = string.IsNullOrWhiteSpace(settings.CollectionName) ? "_default" : settings.CollectionName;
    }

    public string BucketName { get; }
    public bool ClusterReachable { get; set; }
    public bool BucketExists { get; set; }
    public bool ScopeExists { get; set; }
    public bool CollectionExists { get; set; }
    public List<string> Errors { get; } = new();

    public string ScopeName => _scopeName;
    public string CollectionName => _collectionName;

    public bool HasErrors => Errors.Count > 0;
    public bool IsComplete => ClusterReachable && BucketExists && ScopeExists && CollectionExists;
    public bool NeedsCreation => !HasErrors && (!BucketExists || !ScopeExists || !CollectionExists);
}
