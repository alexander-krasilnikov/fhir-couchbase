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
    public IActionResult Upload()
    {
        return View(new UploadPageViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(UploadPageViewModel viewModel, CancellationToken cancellationToken)
    {
        viewModel.S3Import ??= new S3ImportViewModel();

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
        var pageViewModel = new UploadPageViewModel
        {
            S3Import = s3ViewModel,
            Files = new List<IFormFile>()
        };

        if (!TryValidateModel(s3ViewModel, nameof(UploadPageViewModel.S3Import)))
        {
            return View("Upload", pageViewModel);
        }

        var options = s3ViewModel.ToOptions();
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

        if (downloadResult.Failures.Count > 0)
        {
            ingestionResult.Failures.InsertRange(0, downloadResult.Failures);
        }

        if (downloadResult.Warnings.Count > 0)
        {
            ingestionResult.Warnings.AddRange(downloadResult.Warnings);
        }

        pageViewModel.Result = ingestionResult;

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
        var viewModel = new ConnectionTestViewModel
        {
            Settings = settings
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveConnection(ConnectionTestViewModel viewModel, CancellationToken cancellationToken)
    {
        viewModel.Settings ??= new CouchbaseSettings();

        if (!TryValidateModel(viewModel.Settings, nameof(ConnectionTestViewModel.Settings)))
        {
            viewModel.SaveSucceeded = false;
            viewModel.SaveAlertStyle = "danger";
            viewModel.SaveMessage = "Please fix the highlighted errors.";
            return View("Connection", viewModel);
        }

        await _settingsStore.SaveAsync(viewModel.Settings, cancellationToken);
        var structureStatus = await _couchbaseService.CheckStructureAsync(cancellationToken);

        ModelState.Clear();
        viewModel.Settings = await _settingsStore.GetAsync(cancellationToken);
        viewModel.StructureStatus = structureStatus;

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
}
