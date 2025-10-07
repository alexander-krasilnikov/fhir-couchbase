using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FhirCouchbaseDemo.Web.Models;
using FhirCouchbaseDemo.Web.Services;
using FhirCouchbaseDemo.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace FhirCouchbaseDemo.Web.Controllers;

public class PrescriptionsController : Controller
{
    private readonly PrescriptionIngestionService _ingestionService;
    private readonly ICouchbaseService _couchbaseService;
    private readonly ICouchbaseSettingsStore _settingsStore;

    public PrescriptionsController(
        PrescriptionIngestionService ingestionService,
        ICouchbaseService couchbaseService,
        ICouchbaseSettingsStore settingsStore)
    {
        _ingestionService = ingestionService;
        _couchbaseService = couchbaseService;
        _settingsStore = settingsStore;
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
        if (viewModel.Files is null || viewModel.Files.Count == 0)
        {
            ModelState.AddModelError(nameof(viewModel.Files), "Select at least one XML file to upload.");
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        viewModel.Files ??= new List<Microsoft.AspNetCore.Http.IFormFile>();
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
        viewModel.Files = new List<Microsoft.AspNetCore.Http.IFormFile>();

        return View(viewModel);
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
            viewModel.SaveMessage = "Please fix the highlighted errors.";
            return View("Connection", viewModel);
        }

        await _settingsStore.SaveAsync(viewModel.Settings, cancellationToken);
        ModelState.Clear();
        viewModel.Settings = await _settingsStore.GetAsync(cancellationToken);
        viewModel.SaveSucceeded = true;
        viewModel.SaveMessage = "Couchbase settings saved.";

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
}
