using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FhirCouchbaseDemo.Web.Models;

namespace FhirCouchbaseDemo.Web.Services;

public interface ICouchbaseService : IAsyncDisposable
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
    Task StoreAsync(PrescriptionRecord record, CancellationToken cancellationToken = default);
    Task StoreBatchAsync(IEnumerable<PrescriptionRecord> records, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrescriptionRecord>> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<CouchbaseTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
