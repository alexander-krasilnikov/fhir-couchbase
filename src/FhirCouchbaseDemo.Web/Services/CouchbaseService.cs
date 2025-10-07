using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FhirCouchbaseDemo.Web.Models;
using Couchbase;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Query;
using Microsoft.Extensions.Logging;

namespace FhirCouchbaseDemo.Web.Services;

public class CouchbaseService : ICouchbaseService
{
    private readonly ICouchbaseSettingsStore _settingsStore;
    private readonly ILogger<CouchbaseService> _logger;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);

    private CouchbaseSettings? _currentSettings;
    private ICluster? _cluster;
    private IBucket? _bucket;
    private ICouchbaseCollection? _collection;
    private bool _disposed;

    public CouchbaseService(ICouchbaseSettingsStore settingsStore, ILogger<CouchbaseService> logger)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CouchbaseService));
            }

            var desiredSettings = await _settingsStore.GetAsync(cancellationToken).ConfigureAwait(false);

            if (_collection is not null && _currentSettings is not null && _currentSettings.Equals(desiredSettings))
            {
                return;
            }

            await DisposeClusterAsync().ConfigureAwait(false);

            _currentSettings = desiredSettings.Clone();

            _logger.LogInformation("Connecting to Couchbase cluster {ConnectionString}...", _currentSettings.ConnectionString);
            _cluster = await Cluster.ConnectAsync(_currentSettings.ConnectionString, options =>
            {
                options.UserName = _currentSettings.Username;
                options.Password = _currentSettings.Password;
            }).ConfigureAwait(false);

            _bucket = await _cluster.BucketAsync(_currentSettings.BucketName).ConfigureAwait(false);

            var scopeName = ResolveScopeName(_currentSettings);
            var collectionName = ResolveCollectionName(_currentSettings);

            if (scopeName.Equals("_default", StringComparison.Ordinal))
            {
                _collection = collectionName.Equals("_default", StringComparison.Ordinal)
                    ? _bucket.DefaultCollection()
                    : _bucket.Scope(scopeName).Collection(collectionName);
            }
            else
            {
                var scope = _bucket.Scope(scopeName);
                _collection = scope.Collection(collectionName);
            }

            _logger.LogInformation(
                "Couchbase connection initialized against bucket {Bucket}, scope {Scope}, collection {Collection}.",
                _currentSettings.BucketName,
                scopeName,
                collectionName);
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public async Task StoreAsync(PrescriptionRecord record, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (_collection is null)
        {
            throw new InvalidOperationException("Couchbase collection is not initialized.");
        }

        await _collection.UpsertAsync(record.Id, record, new UpsertOptions().CancellationToken(cancellationToken)).ConfigureAwait(false);
    }

    public async Task StoreBatchAsync(IEnumerable<PrescriptionRecord> records, CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            await StoreAsync(record, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<PrescriptionRecord>> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (_cluster is null || _currentSettings is null)
        {
            throw new InvalidOperationException("Couchbase cluster is not initialized.");
        }

        var scopeName = ResolveScopeName(_currentSettings);
        var collectionName = ResolveCollectionName(_currentSettings);
        var builder = new StringBuilder();
        builder.AppendFormat(
            "SELECT RAW p FROM `{0}`.`{1}`.`{2}` p WHERE 1=1",
            _currentSettings.BucketName,
            scopeName,
            collectionName);

        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(criteria.Pzn))
        {
            builder.Append(" AND ANY pzn IN p.pznCodes SATISFIES pzn = $pzn END");
            parameters["pzn"] = criteria.Pzn;
        }

        if (criteria.IssueDateFrom.HasValue)
        {
            builder.Append(" AND p.issueDate >= $issueDateFrom");
            parameters["issueDateFrom"] = criteria.IssueDateFrom.Value;
        }

        if (criteria.IssueDateTo.HasValue)
        {
            builder.Append(" AND p.issueDate <= $issueDateTo");
            parameters["issueDateTo"] = criteria.IssueDateTo.Value;
        }

        builder.Append(" ORDER BY p.uploadedAt DESC");

        var query = builder.ToString();
        _logger.LogDebug("Executing Couchbase query: {Query}", query);

        var options = new QueryOptions()
            .ScanConsistency(QueryScanConsistency.RequestPlus);

        foreach (var kvp in parameters)
        {
            if (kvp.Value is not null)
            {
                options.Parameter(kvp.Key, kvp.Value);
            }
        }

        var records = new List<PrescriptionRecord>();
        var queryResult = await _cluster.QueryAsync<PrescriptionRecord>(query, options).ConfigureAwait(false);
        await foreach (var row in queryResult.Rows.ConfigureAwait(false))
        {
            records.Add(row);
        }

        return records;
    }

    public async Task<CouchbaseStructureStatus> CheckStructureAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsStore.GetAsync(cancellationToken).ConfigureAwait(false);
        var status = new CouchbaseStructureStatus(settings);
        ICluster? cluster = null;

        try
        {
            cluster = await Cluster.ConnectAsync(settings.ConnectionString, options =>
            {
                options.UserName = settings.Username;
                options.Password = settings.Password;
            }).ConfigureAwait(false);

            status.ClusterReachable = true;

            IDictionary<string, BucketSettings> buckets;
            try
            {
                buckets = await cluster.Buckets.GetAllBucketsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to enumerate Couchbase buckets.");
                status.Errors.Add($"Failed to enumerate buckets: {ex.Message}");
                return status;
            }

            if (!buckets.ContainsKey(settings.BucketName))
            {
                status.BucketExists = false;
                return status;
            }

            status.BucketExists = true;
            var bucket = await cluster.BucketAsync(settings.BucketName).ConfigureAwait(false);
            var scopeName = ResolveScopeName(settings);
            var collectionName = ResolveCollectionName(settings);

            List<ScopeSpec> scopes;
            try
            {
                scopes = (await bucket.Collections.GetAllScopesAsync().ConfigureAwait(false)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to enumerate scopes for bucket {Bucket}.", settings.BucketName);
                status.Errors.Add($"Failed to enumerate scopes: {ex.Message}");
                return status;
            }

            ScopeSpec? scopeSpec = null;
            if (scopeName.Equals("_default", StringComparison.Ordinal))
            {
                status.ScopeExists = true;
                scopeSpec = scopes.FirstOrDefault(s => s.Name.Equals(scopeName, StringComparison.Ordinal));
            }
            else
            {
                scopeSpec = scopes.FirstOrDefault(s => s.Name.Equals(scopeName, StringComparison.Ordinal));
                status.ScopeExists = scopeSpec is not null;
                if (!status.ScopeExists)
                {
                    return status;
                }
            }

            if (collectionName.Equals("_default", StringComparison.Ordinal) && scopeName.Equals("_default", StringComparison.Ordinal))
            {
                status.CollectionExists = true;
            }
            else if (scopeSpec is not null)
            {
                status.CollectionExists = scopeSpec.Collections.Any(c => c.Name.Equals(collectionName, StringComparison.Ordinal));
            }
            else
            {
                var defaultScope = scopes.FirstOrDefault(s => s.Name.Equals(scopeName, StringComparison.Ordinal));
                status.CollectionExists = defaultScope?.Collections.Any(c => c.Name.Equals(collectionName, StringComparison.Ordinal)) ?? false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify Couchbase structures.");
            status.Errors.Add(ex.Message);
        }
        finally
        {
            if (cluster is not null)
            {
                await cluster.DisposeAsync().ConfigureAwait(false);
            }
        }

        return status;
    }

    public async Task<CouchbaseStructureStatus> CreateMissingStructuresAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsStore.GetAsync(cancellationToken).ConfigureAwait(false);
        var status = await CheckStructureAsync(cancellationToken).ConfigureAwait(false);
        if (status.HasErrors)
        {
            return status;
        }

        ICluster? cluster = null;

        try
        {
            cluster = await Cluster.ConnectAsync(settings.ConnectionString, options =>
            {
                options.UserName = settings.Username;
                options.Password = settings.Password;
            }).ConfigureAwait(false);

            status.ClusterReachable = true;
            var scopeName = ResolveScopeName(settings);
            var collectionName = ResolveCollectionName(settings);

            if (!status.BucketExists)
            {
                _logger.LogInformation("Creating Couchbase bucket {BucketName}...", settings.BucketName);
                var bucketSettings = new BucketSettings
                {
                    Name = settings.BucketName,
                    BucketType = BucketType.Couchbase,
                    RamQuotaMB = 256,
                    NumReplicas = 0,
                    FlushEnabled = false
                };

                await cluster.Buckets.CreateBucketAsync(bucketSettings, options => options.CancellationToken(cancellationToken)).ConfigureAwait(false);
                await WaitForBucketReadyAsync(cluster, settings.BucketName, cancellationToken).ConfigureAwait(false);
            }

            var bucket = await cluster.BucketAsync(settings.BucketName).ConfigureAwait(false);
            var collectionManager = bucket.Collections;

            if (!status.ScopeExists && !scopeName.Equals("_default", StringComparison.Ordinal))
            {
                _logger.LogInformation("Creating Couchbase scope {ScopeName} in bucket {BucketName}...", scopeName, settings.BucketName);
                await collectionManager.CreateScopeAsync(scopeName, options => options.CancellationToken(cancellationToken)).ConfigureAwait(false);
                status.ScopeExists = true;
            }

            if (!status.CollectionExists && !collectionName.Equals("_default", StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Creating Couchbase collection {CollectionName} in scope {ScopeName} ({BucketName}).",
                    collectionName,
                    scopeName,
                    settings.BucketName);

                var collectionSpec = new CollectionSpec(scopeName, collectionName);
#pragma warning disable CS0618 // Use legacy overload until SDK consumers adopt CreateCollectionSettings signatures
                await collectionManager.CreateCollectionAsync(collectionSpec, options => options.CancellationToken(cancellationToken)).ConfigureAwait(false);
#pragma warning restore CS0618
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Couchbase structures.");
            status.Errors.Add(ex.Message);
            return status;
        }
        finally
        {
            if (cluster is not null)
            {
                await cluster.DisposeAsync().ConfigureAwait(false);
            }
        }

        var refreshedStatus = await CheckStructureAsync(cancellationToken).ConfigureAwait(false);
        if (!refreshedStatus.HasErrors && refreshedStatus.IsComplete)
        {
            await DisposeClusterAsync().ConfigureAwait(false);
        }

        return refreshedStatus;
    }

    private static async Task WaitForBucketReadyAsync(ICluster cluster, string bucketName, CancellationToken cancellationToken)
    {
        var attempts = 0;
        while (attempts < 10)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await cluster.BucketAsync(bucketName).ConfigureAwait(false);
                return;
            }
            catch (CouchbaseException)
            {
                attempts++;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new System.TimeoutException($"Bucket '{bucketName}' was not ready after waiting for 10 seconds.");
    }

    public async Task<CouchbaseTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            if (_cluster is null)
            {
                return new CouchbaseTestResult
                {
                    Success = false,
                    Message = "Cluster was not initialized."
                };
            }

            var report = await _cluster.PingAsync().ConfigureAwait(false);
            var details = new List<string>();
            foreach (var service in report.Services)
            {
                var serviceEntries = service.Value
                    .Select(endpoint => $"{endpoint.State} ({endpoint.Latency})")
                    .ToArray();
                details.Add($"{service.Key}: {string.Join(", ", serviceEntries)}");
            }

            return new CouchbaseTestResult
            {
                Success = true,
                Message = "Successfully connected to Couchbase.",
                Details = details
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couchbase connectivity test failed.");
            return new CouchbaseTestResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisposeClusterAsync().ConfigureAwait(false);
        _initializationGate.Dispose();
    }

    private async Task DisposeClusterAsync()
    {
        if (_cluster is not null)
        {
            await _cluster.DisposeAsync().ConfigureAwait(false);
        }

        _cluster = null;
        _bucket = null;
        _collection = null;
    }

    private static string ResolveScopeName(CouchbaseSettings settings) =>
        string.IsNullOrWhiteSpace(settings.ScopeName) ? "_default" : settings.ScopeName;

    private static string ResolveCollectionName(CouchbaseSettings settings) =>
        string.IsNullOrWhiteSpace(settings.CollectionName) ? "_default" : settings.CollectionName;
}
