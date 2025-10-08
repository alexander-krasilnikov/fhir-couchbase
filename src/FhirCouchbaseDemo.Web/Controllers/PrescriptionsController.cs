using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FhirCouchbaseDemo.Web.Models;
using FhirCouchbaseDemo.Web.Services;
using FhirCouchbaseDemo.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FhirCouchbaseDemo.Web.Controllers;

public class PrescriptionsController : Controller
{
    private readonly PrescriptionIngestionService _ingestionService;
    private readonly ICouchbaseService _couchbaseService;
    private readonly ICouchbaseSettingsStore _settingsStore;
    private readonly IS3DocumentLoader _s3DocumentLoader;

    public PrescriptionsController(
        PrescriptionIngestionService ingestionService,
        ICouchbaseService couchbaseService,
        ICouchbaseSettingsStore settingsStore,
        IS3DocumentLoader s3DocumentLoader)
    {
        _ingestionService = ingestionService;
        _couchbaseService = couchbaseService;
        _settingsStore = settingsStore;
        _s3DocumentLoader = s3DocumentLoader;
    }

    [HttpGet]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.GetAsync(cancellationToken);
        settings.S3 ??= new S3Settings();

        var viewModel = new UploadPageViewModel
        {
            StoredS3Settings = settings.S3.Clone(),
            S3Import = new S3ImportViewModel
            {
                Prefix = settings.S3.DefaultPrefix
            },
            S3Summary = null
        };

        viewModel.ActiveTab = viewModel.HasS3Configuration ? "files" : "s3";

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(UploadPageViewModel viewModel, CancellationToken cancellationToken)
    {
        viewModel.S3Import ??= new S3ImportViewModel();
        var savedSettings = await _settingsStore.GetAsync(cancellationToken);
        savedSettings.S3 ??= new S3Settings();
        viewModel.StoredS3Settings = savedSettings.S3.Clone();
        viewModel.ActiveTab = "files";
        viewModel.S3Import.Prefix = savedSettings.S3.DefaultPrefix;
        viewModel.S3Summary = null;
        var prefixKey = $"{nameof(UploadPageViewModel.S3Import)}.{nameof(S3ImportViewModel.Prefix)}";
        if (ModelState.ContainsKey(prefixKey))
        {
            ModelState.Remove(prefixKey);
        }

        if (viewModel.Files is null || viewModel.Files.Count == 0)
        {
            ModelState.AddModelError(nameof(viewModel.Files), "Select at least one file to upload.");
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        viewModel.Files ??= new List<IFormFile>();
        var uploads = new List<FileUploadContext>();
        foreach (var file in viewModel.Files.Where(f => f.Length > 0))
        {
            var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            uploads.Add(new FileUploadContext
            {
                FileName = file.FileName,
                Content = memoryStream
            });
        }

        var result = await _ingestionService.IngestAsync(uploads, cancellationToken);
        viewModel.Result = result;
        viewModel.Files = new List<IFormFile>();

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportFromS3(S3ImportViewModel s3ViewModel, CancellationToken cancellationToken)
    {
        var savedSettings = await _settingsStore.GetAsync(cancellationToken);
        savedSettings.S3 ??= new S3Settings();

        var trimmedPrefix = string.IsNullOrWhiteSpace(s3ViewModel.Prefix) ? null : s3ViewModel.Prefix.Trim();
        s3ViewModel.Prefix = trimmedPrefix;
        var s3PrefixKey = $"{nameof(UploadPageViewModel.S3Import)}.{nameof(S3ImportViewModel.Prefix)}";
        if (ModelState.ContainsKey(s3PrefixKey))
        {
            ModelState.Remove(s3PrefixKey);
        }

        var pageViewModel = new UploadPageViewModel
        {
            S3Import = s3ViewModel,
            Files = new List<IFormFile>(),
            StoredS3Settings = savedSettings.S3.Clone(),
            ActiveTab = "s3"
        };

        if (!HasS3Configuration(savedSettings.S3))
        {
            ModelState.AddModelError(string.Empty, "Configure S3 credentials on the Connection page before importing.");
        }

        if (!TryValidateModel(s3ViewModel, nameof(UploadPageViewModel.S3Import)) || !ModelState.IsValid)
        {
            return View("Upload", pageViewModel);
        }

        var options = s3ViewModel.ToOptions(savedSettings.S3);
        var downloadResult = await _s3DocumentLoader.LoadAsync(options, cancellationToken).ConfigureAwait(false);

        UploadResult ingestionResult;
        if (downloadResult.Files.Count > 0)
        {
            ingestionResult = await _ingestionService.IngestAsync(downloadResult.Files, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            ingestionResult = new UploadResult();
        }

        var ingestionFailuresBeforeMerge = ingestionResult.Failures.Count;
        if (downloadResult.Failures.Count > 0)
        {
            ingestionResult.Failures.InsertRange(0, downloadResult.Failures);
        }

        if (downloadResult.Warnings.Count > 0)
        {
            ingestionResult.Warnings.AddRange(downloadResult.Warnings);
        }

        pageViewModel.Result = ingestionResult;
        if (!string.Equals(savedSettings.S3.DefaultPrefix, s3ViewModel.Prefix, StringComparison.Ordinal))
        {
            savedSettings.S3.DefaultPrefix = s3ViewModel.Prefix;
            await _settingsStore.SaveAsync(savedSettings, cancellationToken).ConfigureAwait(false);
        }
        pageViewModel.StoredS3Settings = savedSettings.S3.Clone();
        pageViewModel.S3Summary = new S3ImportSummary
        {
            Discovered = downloadResult.Files.Count,
            Imported = ingestionResult.StoredRecords.Count,
            Failed = ingestionResult.Failures.Count
        };

        return View("Upload", pageViewModel);
    }

    [HttpGet]
    public IActionResult Search()
    {
        return View(new SearchPageViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Search(SearchPageViewModel viewModel, CancellationToken cancellationToken)
    {
        var criteria = new SearchCriteria
        {
            Pzn = string.IsNullOrWhiteSpace(viewModel.Pzn) ? null : viewModel.Pzn.Trim(),
            IssueDateFrom = viewModel.IssueDateFrom,
            IssueDateTo = viewModel.IssueDateTo
        };

        var results = await _couchbaseService.SearchAsync(criteria, cancellationToken);
        viewModel.Results = results;
        viewModel.HasSearched = true;

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Connection(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.GetAsync(cancellationToken);
        settings.S3 ??= new S3Settings();
        var viewModel = new ConnectionTestViewModel
        {
            Settings = settings
        };
        MaskSecrets(viewModel.Settings);

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveConnection(ConnectionTestViewModel viewModel, CancellationToken cancellationToken)
    {
        viewModel.Settings ??= new CouchbaseSettings();
        viewModel.Settings.S3 ??= new S3Settings();

        var existingSettings = await _settingsStore.GetAsync(cancellationToken);
        existingSettings.S3 ??= new S3Settings();

        if (viewModel.Settings.Password == ConnectionTestViewModel.SecretPlaceholder)
        {
            viewModel.Settings.Password = existingSettings.Password;
        }

        if (viewModel.Settings.S3.SecretAccessKey == ConnectionTestViewModel.SecretPlaceholder)
        {
            viewModel.Settings.S3.SecretAccessKey = existingSettings.S3.SecretAccessKey;
        }

        if (string.IsNullOrWhiteSpace(viewModel.Settings.S3.SecretAccessKey))
        {
            viewModel.Settings.S3.SecretAccessKey ??= string.Empty;
        }

        if (!TryValidateModel(viewModel.Settings, nameof(ConnectionTestViewModel.Settings)))
        {
            MaskSecrets(viewModel.Settings);
            return View("Connection", viewModel);
        }

        await _settingsStore.SaveAsync(viewModel.Settings, cancellationToken);
        var structureStatus = await _couchbaseService.CheckStructureAsync(cancellationToken);

        ModelState.Clear();
        viewModel.Settings = await _settingsStore.GetAsync(cancellationToken);
        viewModel.StructureStatus = structureStatus;
        MaskSecrets(viewModel.Settings);

        if (structureStatus.HasErrors)
        {
            viewModel.SaveSucceeded = false;
            viewModel.SaveAlertStyle = "danger";
            viewModel.SaveMessage = $"Settings saved, but structure verification failed: {string.Join(" ", structureStatus.Errors)}";
        }
        else if (structureStatus.NeedsCreation)
        {
            viewModel.SaveSucceeded = false;
            viewModel.SaveAlertStyle = "warning";
            viewModel.SaveMessage = $"Settings saved, but the following objects are missing: {DescribeMissingStructures(structureStatus)}.";
        }
        else
        {
            viewModel.SaveSucceeded = true;
            viewModel.SaveAlertStyle = "success";
            viewModel.SaveMessage = "Couchbase settings saved.";
        }

        return View("Connection", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.GetAsync(cancellationToken);
        var result = await _couchbaseService.TestConnectionAsync(cancellationToken);

        var viewModel = new ConnectionTestViewModel
        {
            Settings = settings,
            Result = result
        };
        MaskSecrets(viewModel.Settings);

        return View("Connection", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStructures(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.GetAsync(cancellationToken);
        var status = await _couchbaseService.CreateMissingStructuresAsync(cancellationToken);

        var viewModel = new ConnectionTestViewModel
        {
            Settings = settings,
            StructureStatus = status
        };
        MaskSecrets(viewModel.Settings);

        if (status.HasErrors)
        {
            viewModel.SaveSucceeded = false;
            viewModel.SaveAlertStyle = "danger";
            viewModel.SaveMessage = $"Failed to create Couchbase structures: {string.Join(" ", status.Errors)}";
        }
        else if (status.IsComplete)
        {
            viewModel.SaveSucceeded = true;
            viewModel.SaveAlertStyle = "success";
            viewModel.SaveMessage = "Missing Couchbase structures created successfully.";
        }
        else
        {
            viewModel.SaveSucceeded = false;
            viewModel.SaveAlertStyle = "warning";
            viewModel.SaveMessage = $"Creation completed with warnings. Outstanding objects: {DescribeMissingStructures(status)}.";
        }

        return View("Connection", viewModel);
    }

    private static string DescribeMissingStructures(CouchbaseStructureStatus status)
    {
        var missing = new List<string>();
        if (!status.BucketExists)
        {
            missing.Add($"bucket '{status.BucketName}'");
        }

        if (status.BucketExists && !status.ScopeExists)
        {
            missing.Add($"scope '{status.ScopeName}'");
        }

        if (status.BucketExists && status.ScopeExists && !status.CollectionExists)
        {
            missing.Add($"collection '{status.CollectionName}'");
        }

        return string.Join(", ", missing);
    }

    private static bool HasS3Configuration(S3Settings settings) =>
        !string.IsNullOrWhiteSpace(settings.AccessKeyId) &&
        !string.IsNullOrWhiteSpace(settings.SecretAccessKey) &&
        !string.IsNullOrWhiteSpace(settings.Region) &&
        !string.IsNullOrWhiteSpace(settings.BucketName);

    private static void MaskSecrets(CouchbaseSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.Password))
        {
            settings.Password = ConnectionTestViewModel.SecretPlaceholder;
        }

        if (settings.S3 is not null && !string.IsNullOrEmpty(settings.S3.SecretAccessKey))
        {
            settings.S3.SecretAccessKey = ConnectionTestViewModel.SecretPlaceholder;
        }
    }
}
