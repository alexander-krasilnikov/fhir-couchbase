using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
                var format = DetectFormat(file);
                if (file.Content.CanSeek)
                {
                    file.Content.Seek(0, SeekOrigin.Begin);
                }

                var processed = _processor.Process(file.Content, file.FileName, format);
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

    private static FhirDocumentFormat DetectFormat(FileUploadContext file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!string.IsNullOrEmpty(extension))
        {
            if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            {
                return FhirDocumentFormat.Json;
            }

            if (string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase))
            {
                return FhirDocumentFormat.Xml;
            }
        }

        if (file.Content.CanSeek)
        {
            var originalPosition = file.Content.Position;
            try
            {
                file.Content.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(file.Content, Encoding.UTF8, true, 1024, leaveOpen: true);
                int ch;
                do
                {
                    ch = reader.Read();
                } while (ch != -1 && char.IsWhiteSpace((char)ch));

                return ch switch
                {
                    '{' or '[' => FhirDocumentFormat.Json,
                    '<' => FhirDocumentFormat.Xml,
                    _ => FhirDocumentFormat.Xml
                };
            }
            finally
            {
                file.Content.Seek(originalPosition, SeekOrigin.Begin);
            }
        }

        return FhirDocumentFormat.Xml;
    }
}
