using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ThinkerThings.Services.Account.Api.Infra
{
    public sealed class DaprStateClientService : IDaprStateClientService
    {
        private readonly IOptions<DaprOptions> _daprOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DaprStateClientService> _logger;

        private const string CONTENT_TYPE = "application/json";

        public DaprStateClientService(IHttpClientFactory httpClientFactory, IOptions<DaprOptions> daprOptions, ILogger<DaprStateClientService> logger)
        {
            _logger = logger;
            _daprOptions = daprOptions;
            _httpClientFactory = httpClientFactory;
        }

        private string DaprEndpointStateManagement => $"{_daprOptions.Value.EndPoint}/state";

        public async Task Save<TValue>(string key, TValue value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));

            if (value == null)
                throw new ArgumentException("The value cannot be null or empty.", nameof(value));

            var httpResponseMessage = await ExecuteSendAsync(HttpMethod.Post, key, value, cancellationToken).ConfigureAwait(false);

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

        public async Task<TValue> Get<TValue>(string key, CancellationToken cancellationToken = default)
        {
            var httpResponseMessage = await ExecuteSendAsync<TValue>(HttpMethod.Get, key, cancellationToken).ConfigureAwait(false);

            return await ValidaStatusIsNotSuccess<TValue>(httpResponseMessage).ConfigureAwait(false);
        }

        public async Task Delete<TValue>(string key, CancellationToken cancellationToken = default)
        {
            var httpResponseMessage = await ExecuteSendAsync<TValue>(HttpMethod.Delete, key, cancellationToken).ConfigureAwait(false);

            await ValidaStatusIsNotSuccess<TValue>(httpResponseMessage).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> ExecuteSendAsync<TValue>(HttpMethod httpMethod, string key, CancellationToken cancellationToken)
            => await ExecuteSendAsync(httpMethod, key, default(TValue), cancellationToken).ConfigureAwait(false);

        private async Task<HttpResponseMessage> ExecuteSendAsync<TValue>(HttpMethod httpMethod, string key, TValue value, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));

            var httpClient = _httpClientFactory.CreateClient();

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(CONTENT_TYPE));

            return await httpClient.SendAsync(CreateHttpRequestMessage(httpMethod, key, value), cancellationToken).ConfigureAwait(false);
        }

        private HttpRequestMessage CreateHttpRequestMessage<TValue>(HttpMethod httpMethod, string key, TValue value)
        {
            var request = new HttpRequestMessage { Method = httpMethod };

            if (httpMethod.Equals(HttpMethod.Post))
            {
                request.RequestUri = new Uri(DaprEndpointStateManagement);
                request.Content = new StringContent(CreateContent(new StateEntry<TValue>(key, value)), Encoding.UTF8, CONTENT_TYPE);
            }
            else
            {
                request.RequestUri = new Uri(CreateStateManagamentKey<TValue>(key));
            }

            return request;
        }

        private string CreateStateManagamentKey<TValue>(string key)
            => $"{DaprEndpointStateManagement}/{typeof(TValue).Name.ToLowerInvariant()}-{key}";

        private static string CreateContent<TValue>(StateEntry<TValue> stateStore)
            => CreateContent(new List<StateEntry<TValue>>() { stateStore });

        private static string CreateContent<TValue>(IEnumerable<StateEntry<TValue>> stateStoreEntries) => JsonSerializer.Serialize(stateStoreEntries.ToArray());

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
    }

    internal class StateEntry<TValue>
    {
        public StateEntry(TValue value)
            : this(Guid.NewGuid().ToString(), value)
        {
        }

        public StateEntry(string key, TValue value)
        {
            Value = value;
            Key = CreateKey(key, value);
        }

        public string Key { get; }
        public TValue Value { get; }

        private static string CreateKey(string key, TValue value) => $"{value.GetType().Name.ToLowerInvariant()}-{key}";
    }
}