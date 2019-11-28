using System.Threading;
using System.Threading.Tasks;

namespace ThinkerThings.Services.Account.Api.Infra
{
    public interface IDaprStateClientService
    {
        Task Delete<TValue>(string key, CancellationToken cancellationToken = default);

        Task Save<TValue>(string key, TValue value, CancellationToken cancellationToken = default);

        Task<TValue> Get<TValue>(string key, CancellationToken cancellationToken = default);
    }
}