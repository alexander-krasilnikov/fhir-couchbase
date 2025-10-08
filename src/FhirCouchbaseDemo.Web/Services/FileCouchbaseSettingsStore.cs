using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FhirCouchbaseDemo.Web.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FhirCouchbaseDemo.Web.Services;

public class FileCouchbaseSettingsStore : ICouchbaseSettingsStore
{
    private const string FileName = "settings.json";
    private const string LegacyFileName = "couchbase-settings.json";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly string _legacyFilePath;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CouchbaseSettings? _cache;

    public FileCouchbaseSettingsStore(IHostEnvironment environment, IConfiguration configuration)
    {
        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _filePath = Path.Combine(environment.ContentRootPath, "App_Data", FileName);
        _legacyFilePath = Path.Combine(environment.ContentRootPath, "App_Data", LegacyFileName);
    }

    public async Task<CouchbaseSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
        {
            return _cache.Clone();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache is not null)
            {
                return _cache.Clone();
            }

            CouchbaseSettings? settings = null;

            if (File.Exists(_filePath))
            {
                await using var stream = File.OpenRead(_filePath);
                settings = await JsonSerializer.DeserializeAsync<CouchbaseSettings>(stream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (File.Exists(_legacyFilePath))
            {
                await using var stream = File.OpenRead(_legacyFilePath);
                settings = await JsonSerializer.DeserializeAsync<CouchbaseSettings>(stream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            settings ??= _configuration.GetSection("Couchbase").Get<CouchbaseSettings>() ?? new CouchbaseSettings();
            settings.S3 ??= new S3Settings();

            _cache = settings;
            return settings.Clone();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CouchbaseSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            settings.S3 ??= new S3Settings();
            _cache = settings.Clone();

            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, _cache, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (!string.Equals(_filePath, _legacyFilePath, StringComparison.OrdinalIgnoreCase) && File.Exists(_legacyFilePath))
            {
                TryDeleteLegacyFile();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void TryDeleteLegacyFile()
    {
        try
        {
            File.Delete(_legacyFilePath);
        }
        catch (IOException)
        {
            // Best effort clean-up only; ignore failures so saving continues.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort clean-up only; ignore failures so saving continues.
        }
    }
}
