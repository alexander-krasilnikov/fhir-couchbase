using System.Threading;
using System.Threading.Tasks;
using FhirCouchbaseDemo.Web.Models;

namespace FhirCouchbaseDemo.Web.Services;

public interface ICouchbaseSettingsStore
{
    Task<CouchbaseSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CouchbaseSettings settings, CancellationToken cancellationToken = default);
}
