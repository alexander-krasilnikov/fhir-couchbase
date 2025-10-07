using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using FhirCouchbaseDemo.Web.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FhirCouchbaseDemo.Web.Services;

public class FhirDocumentProcessor
{
    private const string PznNamespace = "http://fhir.de/CodeSystem/ifa/pzn";
    private static readonly string[] TimestampElementCandidates =
    {
        "timestamp",
        "authoredOn",
        "time",
        "whenHandedOver",
        "whenPrepared",
        "issued",
        "recordedDate"
    };

    private readonly ILogger<FhirDocumentProcessor> _logger;

    public FhirDocumentProcessor(ILogger<FhirDocumentProcessor> logger)
    {
        _logger = logger;
    }

    public FhirProcessingResult Process(Stream xmlStream, string fileName)
    {
        try
        {
            xmlStream.Position = 0;
            using var reader = new StreamReader(xmlStream);
            var xmlContent = reader.ReadToEnd();
            var xmlDocument = LoadXmlDocument(xmlContent);

            var jsonPayloadNode = ConvertToFhirJson(xmlContent, fileName) ??
                                  ConvertToGenericJson(xmlDocument, fileName);

            var xDocument = XDocument.Parse(xmlContent, LoadOptions.None);

            var metadata = ExtractMetadata(xDocument);

            var record = new PrescriptionRecord
            {
                FileName = fileName,
                JsonPayload = jsonPayloadNode,
                RawXml = xmlContent,
                PznCodes = metadata.PznCodes,
                PrimaryPzn = metadata.PrimaryPzn,
                IssueDate = metadata.IssueDate
            };

            if (metadata.PznCodes.Count == 0)
            {
                metadata.Warnings.Add("No PZN code was found in the document.");
            }

            if (metadata.IssueDate is null)
            {
                metadata.Warnings.Add("No issued/timestamp value was detected in the document.");
            }

            return new FhirProcessingResult
            {
                Succeeded = true,
                Record = record,
                Warnings = metadata.Warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process FHIR XML document {FileName}", fileName);
            return new FhirProcessingResult
            {
                Succeeded = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static FhirMetadata ExtractMetadata(XDocument document)
    {
        var metadata = new FhirMetadata();
        var pznValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var coding in document.Descendants().Where(IsCodingElement))
        {
            var system = GetElementValue(coding, "system");
            if (!string.Equals(system, PznNamespace, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var code = GetElementValue(coding, "code");
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            if (pznValues.Add(code))
            {
                metadata.PznCodes.Add(code);
            }
        }

        metadata.PrimaryPzn = metadata.PznCodes.FirstOrDefault();

        foreach (var element in document.Descendants())
        {
            if (TimestampElementCandidates.Contains(element.Name.LocalName, StringComparer.OrdinalIgnoreCase))
            {
                var rawValue = element.Attribute("value")?.Value ?? element.Value;
                if (TryParseDate(rawValue, out var parsedDate))
                {
                    metadata.IssueDate = parsedDate;
                    break;
                }
            }
        }

        return metadata;
    }

    private static XmlDocument LoadXmlDocument(string xmlContent)
    {
        var settings = new XmlReaderSettings { IgnoreWhitespace = true };
        var xmlDocument = new XmlDocument();
        using var stringReader = new StringReader(xmlContent);
        using var xmlReader = XmlReader.Create(stringReader, settings);
        xmlDocument.Load(xmlReader);
        return xmlDocument;
    }

    private JToken? ConvertToFhirJson(string xmlContent, string fileName)
    {
        try
        {
            var parser = new FhirXmlParser(new ParserSettings { PermissiveParsing = true });
            Resource resource = parser.Parse<Resource>(xmlContent);
            var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = false });
            var jsonText = serializer.SerializeToString(resource);
            return JToken.Parse(jsonText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FHIR XML to JSON conversion failed for {FileName}, falling back to generic XML serializer.", fileName);
            return null;
        }
    }

    private JToken? ConvertToGenericJson(XmlDocument document, string fileName)
    {
        try
        {
            var jsonPayloadText = JsonConvert.SerializeXmlNode(document, Newtonsoft.Json.Formatting.None, true);
            return JToken.Parse(jsonPayloadText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse generated JSON payload for {FileName}; storing XML only.", fileName);
            return null;
        }
    }

    private static bool IsCodingElement(XElement element) =>
        string.Equals(element.Name.LocalName, "coding", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(element.Name.LocalName, "valueCoding", StringComparison.OrdinalIgnoreCase);

    private static string? GetElementValue(XElement parent, string localName)
    {
        var child = parent.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

        if (child is null)
        {
            return null;
        }

        return child.Attribute("value")?.Value ?? child.Value;
    }

    private static bool TryParseDate(string? rawValue, out DateTime? parsedDate)
    {
        parsedDate = null;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            parsedDate = parsed;
            return true;
        }

        if (DateTime.TryParse(rawValue, CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.AssumeUniversal, out parsed))
        {
            parsedDate = parsed;
            return true;
        }

        return false;
    }
}
