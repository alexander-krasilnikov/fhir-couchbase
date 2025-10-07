using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FhirCouchbaseDemo.Web.Models;
using Microsoft.Extensions.Logging;

namespace FhirCouchbaseDemo.Web.Services;

public class PrescriptionIngestionService
{
    private readonly FhirDocumentProcessor _processor;
    private readonly ICouchbaseService _couchbaseService;
    private readonly ILogger<PrescriptionIngestionService> _logger;

    public PrescriptionIngestionService(
        FhirDocumentProcessor processor,
        ICouchbaseService couchbaseService,
        ILogger<PrescriptionIngestionService> logger)
    {
        _processor = processor;
        _couchbaseService = couchbaseService;
        _logger = logger;
    }

    public async Task<UploadResult> IngestAsync(
        IEnumerable<FileUploadContext> files,
        CancellationToken cancellationToken = default)
    {
        var result = new UploadResult();
        var recordsToPersist = new List<PrescriptionRecord>();

        foreach (var file in files)
        {
            try
            {
                if (file.Content.CanSeek)
                {
                    file.Content.Seek(0, SeekOrigin.Begin);
                }

                var processed = _processor.Process(file.Content, file.FileName);
                if (!processed.Succeeded || processed.Record is null)
                {
                    result.Failures.Add(new UploadFailure
                    {
                        FileName = file.FileName,
                        ErrorMessage = processed.ErrorMessage ?? "Unknown processing error"
                    });
                    continue;
                }

                recordsToPersist.Add(processed.Record);

                foreach (var warning in processed.Warnings)
                {
                    result.Warnings.Add($"{file.FileName}: {warning}");
                }
            }
            finally
            {
                await file.Content.DisposeAsync();
            }
        }

        if (recordsToPersist.Count > 0)
        {
            await _couchbaseService.StoreBatchAsync(recordsToPersist, cancellationToken);
            result.StoredRecords.AddRange(recordsToPersist);
        }

        return result;
    }
}
