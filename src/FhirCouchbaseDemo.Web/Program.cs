using FhirCouchbaseDemo.Web.Models;
using FhirCouchbaseDemo.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<ICouchbaseSettingsStore, FileCouchbaseSettingsStore>();
builder.Services.AddSingleton<ICouchbaseService, CouchbaseService>();
builder.Services.AddSingleton<FhirDocumentProcessor>();
builder.Services.AddSingleton<IS3DocumentLoader, S3DocumentLoader>();
builder.Services.AddScoped<PrescriptionIngestionService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
