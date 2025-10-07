using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FhirCouchbaseDemo.Web.Models;
using Microsoft.Extensions.Logging;

namespace FhirCouchbaseDemo.Web.Services;

public class S3DocumentLoader : IS3DocumentLoader
{
    private readonly ILogger<S3DocumentLoader> _logger;

    public S3DocumentLoader(ILogger<S3DocumentLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<S3DocumentLoaderResult> LoadAsync(S3ImportOptions options, CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.Normalize();

        var result = new S3DocumentLoaderResult();
        AmazonS3Config config;

        try
        {
            config = BuildConfig(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build Amazon S3 configuration.");
            result.Failures.Add(new UploadFailure
            {
                FileName = "(S3 import)",
                ErrorMessage = ex.Message
            });
            return result;
        }

        AWSCredentials credentials = new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey);

        try
        {
            using var client = new AmazonS3Client(credentials, config);

            if (options.HasExplicitKeys)
            {
                await LoadExplicitKeysAsync(client, options, result, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await LoadByPrefixAsync(client, options, result, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 request failed when accessing bucket {Bucket}.", options.BucketName);
            result.Failures.Add(new UploadFailure
            {
                FileName = "(S3 import)",
                ErrorMessage = $"S3 error: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure when importing from S3 bucket {Bucket}.", options.BucketName);
            result.Failures.Add(new UploadFailure
            {
                FileName = "(S3 import)",
                ErrorMessage = ex.Message
            });
        }

        if (!result.AnySuccess && result.Failures.Count == 0)
        {
            result.Failures.Add(new UploadFailure
            {
                FileName = "(S3 import)",
                ErrorMessage = "No objects were downloaded."
            });
        }

        return result;
    }

    private static AmazonS3Config BuildConfig(S3ImportOptions options)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle
        };

        var endpointUri = options.TryGetEndpointUri();
        if (endpointUri is not null)
        {
            config.ServiceURL = endpointUri.ToString();
            config.UseHttp = string.Equals(endpointUri.Scheme, "http", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(options.Region))
            {
                config.AuthenticationRegion = options.Region;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(options.Region))
            {
                options.Region = "us-east-1";
            }

            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }

        return config;
    }

    private async Task LoadExplicitKeysAsync(
        IAmazonS3 client,
        S3ImportOptions options,
        S3DocumentLoaderResult result,
        CancellationToken cancellationToken)
    {
        foreach (var key in options.ObjectKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var file = await DownloadObjectAsync(client, options.BucketName, key, cancellationToken).ConfigureAwait(false);
                if (file is not null)
                {
                    result.Files.Add(file);
                }
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning(ex, "S3 object {Key} not found in bucket {Bucket}.", key, options.BucketName);
                result.Failures.Add(new UploadFailure
                {
                    FileName = key,
                    ErrorMessage = "Object not found."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download S3 object {Key} from bucket {Bucket}.", key, options.BucketName);
                result.Failures.Add(new UploadFailure
                {
                    FileName = key,
                    ErrorMessage = ex.Message
                });
            }
        }
    }

    private async Task LoadByPrefixAsync(
        IAmazonS3 client,
        S3ImportOptions options,
        S3DocumentLoaderResult result,
        CancellationToken cancellationToken)
    {
        var request = new ListObjectsV2Request
        {
            BucketName = options.BucketName,
            Prefix = options.Prefix,
            MaxKeys = options.MaxKeys
        };

        ListObjectsV2Response response;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            response = await client.ListObjectsV2Async(request, cancellationToken).ConfigureAwait(false);

            foreach (var s3Object in response.S3Objects.Where(o => !o.Key.EndsWith("/", StringComparison.Ordinal)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var file = await DownloadObjectAsync(client, options.BucketName, s3Object.Key, cancellationToken).ConfigureAwait(false);
                    if (file is not null)
                    {
                        result.Files.Add(file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download S3 object {Key} from bucket {Bucket}.", s3Object.Key, options.BucketName);
                    result.Failures.Add(new UploadFailure
                    {
                        FileName = s3Object.Key,
                        ErrorMessage = ex.Message
                    });
                }

                if (result.Files.Count >= options.MaxKeys)
                {
                    break;
                }
            }

            request.ContinuationToken = response.IsTruncated && result.Files.Count < options.MaxKeys
                ? response.NextContinuationToken
                : null;
        } while (!string.IsNullOrEmpty(request.ContinuationToken));

        if (response.IsTruncated && result.Files.Count >= options.MaxKeys)
        {
            result.Warnings.Add($"Reached the maximum of {options.MaxKeys} objects. Additional objects were not downloaded.");
        }

        if (result.Files.Count == 0 && result.Failures.Count == 0)
        {
            result.Failures.Add(new UploadFailure
            {
                FileName = "(S3 import)",
                ErrorMessage = string.IsNullOrWhiteSpace(options.Prefix)
                    ? "Bucket is empty."
                    : $"No objects found with prefix '{options.Prefix}'."
            });
        }
    }

    private static async Task<FileUploadContext?> DownloadObjectAsync(
        IAmazonS3 client,
        string bucketName,
        string key,
        CancellationToken cancellationToken)
    {
        var request = new GetObjectRequest
        {
            BucketName = bucketName,
            Key = key
        };

        using var response = await client.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);
        await using var responseStream = response.ResponseStream;
        var memoryStream = new MemoryStream();
        await responseStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var fileName = Path.GetFileName(key);
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = key.Replace('/', '_');
        }

        return new FileUploadContext
        {
            FileName = fileName,
            Content = memoryStream
        };
    }
}
