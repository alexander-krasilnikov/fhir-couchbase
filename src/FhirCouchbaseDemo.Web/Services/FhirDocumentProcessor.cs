using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

    public FhirProcessingResult Process(Stream stream, string fileName) =>
        Process(stream, fileName, FhirDocumentFormat.Xml);

    public FhirProcessingResult Process(Stream stream, string fileName, FhirDocumentFormat format)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        try
        {
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
            var payload = reader.ReadToEnd();

            return format switch
            {
                FhirDocumentFormat.Json => ProcessJsonContent(payload, fileName),
                _ => ProcessXmlContent(payload, fileName)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read FHIR document {FileName}", fileName);
            return new FhirProcessingResult
            {
                Succeeded = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private FhirProcessingResult ProcessXmlContent(string xmlContent, string fileName)
    {
        try
        {
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

            AppendDefaultWarnings(metadata);

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

    private FhirProcessingResult ProcessJsonContent(string jsonContent, string fileName)
    {
        try
        {
            var jsonPayload = JToken.Parse(jsonContent);
            var metadata = ExtractMetadata(jsonPayload);

            var record = new PrescriptionRecord
            {
                FileName = fileName,
                JsonPayload = jsonPayload,
                RawXml = jsonContent,
                PznCodes = metadata.PznCodes,
                PrimaryPzn = metadata.PrimaryPzn,
                IssueDate = metadata.IssueDate
            };

            AppendDefaultWarnings(metadata);

            return new FhirProcessingResult
            {
                Succeeded = true,
                Record = record,
                Warnings = metadata.Warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process FHIR JSON document {FileName}", fileName);
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

    private static FhirMetadata ExtractMetadata(JToken document)
    {
        var metadata = new FhirMetadata();
        var pznValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Traverse(JToken token)
        {
            switch (token)
            {
                case JObject obj:
                    foreach (var property in obj.Properties())
                    {
                        if (IsCodingProperty(property.Name))
                        {
                            switch (property.Value.Type)
                            {
                                case JTokenType.Object:
                                    TryAddCoding(property.Value as JObject);
                                    break;
                                case JTokenType.Array:
                                    foreach (var codingObj in property.Value.Children<JObject>())
                                    {
                                        TryAddCoding(codingObj);
                                    }

                                    break;
                            }
                        }

                        if (metadata.IssueDate is null &&
                            TimestampElementCandidates.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            if (property.Value.Type == JTokenType.Date)
                            {
                                metadata.IssueDate = property.Value.Value<DateTime?>();
                            }
                            else
                            {
                                var rawValue = property.Value.Type == JTokenType.String
                                    ? property.Value.Value<string>()
                                    : property.Value.ToString();

                                if (TryParseDate(rawValue, out var parsed))
                                {
                                    metadata.IssueDate = parsed;
                                }
                            }
                        }

                        Traverse(property.Value);
                    }

                    break;

                case JArray array:
                    foreach (var child in array)
                    {
                        Traverse(child);
                    }

                    break;
            }
        }

        void TryAddCoding(JObject? codingObj)
        {
            if (codingObj is null)
            {
                return;
            }

            var system = codingObj["system"]?.Value<string>();
            if (!string.Equals(system, PznNamespace, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var code = codingObj["code"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            if (pznValues.Add(code))
            {
                metadata.PznCodes.Add(code);
            }
        }

        Traverse(document);
        metadata.PrimaryPzn = metadata.PznCodes.FirstOrDefault();

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

    private static bool IsCodingProperty(string propertyName) =>
        string.Equals(propertyName, "coding", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(propertyName, "valueCoding", StringComparison.OrdinalIgnoreCase);

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

    private static void AppendDefaultWarnings(FhirMetadata metadata)
    {
        if (metadata.PznCodes.Count == 0)
        {
            metadata.Warnings.Add("No PZN code was found in the document.");
        }

        if (metadata.IssueDate is null)
        {
            metadata.Warnings.Add("No issued/timestamp value was detected in the document.");
        }
    }
}
