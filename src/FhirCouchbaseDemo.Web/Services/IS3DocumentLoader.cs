using System.Threading;
using System.Threading.Tasks;
using FhirCouchbaseDemo.Web.Models;

namespace FhirCouchbaseDemo.Web.Services;

public interface IS3DocumentLoader
{
    Task<S3DocumentLoaderResult> LoadAsync(S3ImportOptions options, CancellationToken cancellationToken = default);
}
