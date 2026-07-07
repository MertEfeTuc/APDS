using System.Text.Json;

namespace APDS.Services
{
    public record SemanticScholarAuthorDto(int? CitationCount, int? HIndex);

    public interface ISemanticScholarService
    {
        Task<SemanticScholarAuthorDto?> GetAuthorMetricsAsync(string orcidId);
    }

    public class SemanticScholarService : ISemanticScholarService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public SemanticScholarService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<SemanticScholarAuthorDto?> GetAuthorMetricsAsync(string orcidId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.semanticscholar.org/graph/v1/author/ORCID:{orcidId}?fields=citationCount,hIndex");

            var apiKey = _config["SemanticScholar:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
                request.Headers.Add("x-api-key", apiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            int? citationCount = json.TryGetProperty("citationCount", out var cc) && cc.ValueKind != JsonValueKind.Null ? cc.GetInt32() : null;
            int? hIndex = json.TryGetProperty("hIndex", out var hi) && hi.ValueKind != JsonValueKind.Null ? hi.GetInt32() : null;

            return new SemanticScholarAuthorDto(citationCount, hIndex);
        }
    }
}