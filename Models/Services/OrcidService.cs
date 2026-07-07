using System.Net.Http.Headers;
using System.Text.Json;

namespace APDS.Services
{
    public record OrcidWorkDto(string Title, string? JournalName, DateOnly? PublicationDate, string? Doi, long PutCode);

    public interface IOrcidService
    {
        Task<List<OrcidWorkDto>> GetWorksAsync(string orcidId);
    }

    public class OrcidService : IOrcidService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public OrcidService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://orcid.org/oauth/token");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _config["Orcid:ClientId"]!,
                ["client_secret"] = _config["Orcid:ClientSecret"]!,
                ["grant_type"] = "client_credentials",
                ["scope"] = "/read-public"
            });
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("access_token").GetString()!;
        }

        public async Task<List<OrcidWorkDto>> GetWorksAsync(string orcidId)
        {
            var token = await GetAccessTokenAsync();

            var request = new HttpRequestMessage(HttpMethod.Get, $"https://pub.orcid.org/v3.0/{orcidId}/works");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
            var results = new List<OrcidWorkDto>();

            foreach (var group in doc!.RootElement.GetProperty("group").EnumerateArray())
            {
                var summary = group.GetProperty("work-summary")[0];

                var title = summary.GetProperty("title").GetProperty("title").GetProperty("value").GetString() ?? "(Başlıksız)";
                long putCode = summary.GetProperty("put-code").GetInt64();

                string? journalName = null;
                if (summary.TryGetProperty("journal-title", out var jt) && jt.ValueKind != JsonValueKind.Null)
                    journalName = jt.GetProperty("value").GetString();

                DateOnly? pubDate = null;
                if (summary.TryGetProperty("publication-date", out var pd) && pd.ValueKind != JsonValueKind.Null)
                {
                    int? year = pd.TryGetProperty("year", out var y) && y.ValueKind != JsonValueKind.Null
                        ? int.Parse(y.GetProperty("value").GetString()!) : null;
                    int month = pd.TryGetProperty("month", out var m) && m.ValueKind != JsonValueKind.Null
                        ? int.Parse(m.GetProperty("value").GetString()!) : 1;
                    int day = pd.TryGetProperty("day", out var d) && d.ValueKind != JsonValueKind.Null
                        ? int.Parse(d.GetProperty("value").GetString()!) : 1;

                    if (year.HasValue) pubDate = new DateOnly(year.Value, month, day);
                }

                string? doi = null;
                if (summary.TryGetProperty("external-ids", out var extIds) && extIds.ValueKind != JsonValueKind.Null)
                {
                    foreach (var ext in extIds.GetProperty("external-id").EnumerateArray())
                    {
                        if (ext.GetProperty("external-id-type").GetString() == "doi")
                        {
                            doi = ext.GetProperty("external-id-value").GetString();
                            break;
                        }
                    }
                }

                results.Add(new OrcidWorkDto(title, journalName, pubDate, doi, putCode));
            }

            return results;
        }
    }
}