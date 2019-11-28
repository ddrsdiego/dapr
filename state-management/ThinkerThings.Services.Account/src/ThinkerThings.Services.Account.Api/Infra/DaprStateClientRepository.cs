using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ThinkerThings.Services.Account.Api.Infra
{
    public interface IDaprStateClientRepository
    {
        Task Delete<TValue>(string key, CancellationToken cancellationToken = default);

        Task Save<TValue>(string key, TValue value, CancellationToken cancellationToken = default);

        Task<TValue> Get<TValue>(string key, CancellationToken cancellationToken = default);
    }

    public class DaprStateClientRepository : IDaprStateClientRepository
    {
        private readonly IOptions<DaprOptions> _daprOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DaprStateClientRepository> _logger;

        public DaprStateClientRepository(IHttpClientFactory httpClientFactory, IOptions<DaprOptions> daprOptions, ILogger<DaprStateClientRepository> logger)
        {
            _logger = logger;
            _daprOptions = daprOptions;
            _httpClientFactory = httpClientFactory;
        }

        private string DaprEndpointStateManagement => $"{_daprOptions.Value.EndPoint}/state";

        public async Task<TValue> Get<TValue>(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));

            var stateUrl = $"{DaprEndpointStateManagement}/{typeof(TValue).Name.ToLowerInvariant()}-{key}";

            var httpClient = _httpClientFactory.CreateClient();

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var httpResponseMessage = await httpClient.GetAsync(stateUrl, CancellationToken.None).ConfigureAwait(false);

            return await ValidaStatusIsNotSuccess<TValue>(httpResponseMessage).ConfigureAwait(false);
        }

        private async Task<TValue> ValidaStatusIsNotSuccess<TValue>(HttpResponseMessage httpResponseMessage)
        {
            if (httpResponseMessage.IsSuccessStatusCode && httpResponseMessage.Content?.Headers?.ContentLength == 0)
            {
                var error = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning($"Failed to get state with status code '{httpResponseMessage.StatusCode}': {error}.");

                return default;
            }
            else if (httpResponseMessage.StatusCode == HttpStatusCode.NoContent)
            {
                var error = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Failed to get state with status code '{httpResponseMessage.StatusCode}': {error}.");
            }
            else if (httpResponseMessage.StatusCode == HttpStatusCode.BadRequest)
            {
                var error = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Failed to get state with status code '{httpResponseMessage.StatusCode}': {error}.");
            }
            else if (httpResponseMessage.StatusCode == HttpStatusCode.InternalServerError)
            {
                _logger.LogError(new HttpRequestException($"Failed to get state with status code '{httpResponseMessage.StatusCode}'."), $"Failed to get state with status code '{httpResponseMessage.StatusCode}'");
            }

            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TValue>(content);
        }

        public async Task Save<TValue>(string key, TValue value, CancellationToken cancellationToken = default)
        {
            var httpResponseMessage = await ExecuteSendAsync(HttpMethod.Post, new StateStoreEntry<TValue>(key, value));

            //Failed to save state
            if (httpResponseMessage.StatusCode == HttpStatusCode.InternalServerError)
            {
                throw new HttpRequestException($"Failed to get state with status code '{httpResponseMessage.StatusCode}'.");
            }
            //State store is missing or misconfigured
            else if (httpResponseMessage.StatusCode == HttpStatusCode.BadRequest)
            {
                var error = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Failed to get state with status code '{httpResponseMessage.StatusCode}': {error}.");
            }
            //State saved
            else if (httpResponseMessage.StatusCode == HttpStatusCode.Created)
            {
                return;
            }
        }

        public async Task Delete<TValue>(string key, CancellationToken cancellationToken = default)
        {
            var entry = await Get<TValue>(key);

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));
            }

            var stateUrl = $"{DaprEndpointStateManagement}/{typeof(TValue).Name.ToLowerInvariant()}-{key}";

            var httpClient = _httpClientFactory.CreateClient();

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var httpResponseMessage = await httpClient.DeleteAsync(stateUrl, CancellationToken.None).ConfigureAwait(false);

            await ValidaStatusIsNotSuccess<TValue>(httpResponseMessage).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> ExecuteSendAsync<TValue>(HttpMethod httpMethod, StateStoreEntry<TValue> stateStore)
        {
            using var request = new HttpRequestMessage(httpMethod, DaprEndpointStateManagement)
            {
                Content = new StringContent(CreateContent(stateStore), Encoding.UTF8, "application/json")
            };

            var httpClient = _httpClientFactory.CreateClient();
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None).ConfigureAwait(false);
        }

        private static string CreateContent<TValue>(StateStoreEntry<TValue> stateStore)
            => JsonSerializer.Serialize(new List<StateStoreEntry<TValue>>() { stateStore }.ToArray());
    }

    public class StateStoreEntry<TValue>
    {
        public StateStoreEntry(TValue value)
            : this(Guid.NewGuid().ToString(), value)
        {
        }

        public StateStoreEntry(string key, TValue value)
        {
            Value = value;
            Key = CreateKey(key, value);
        }

        public string Key { get; }
        public TValue Value { get; }

        private static string CreateKey(string key, TValue value) => $"{value.GetType().Name.ToLowerInvariant()}-{key}";
    }
}