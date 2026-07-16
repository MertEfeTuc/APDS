using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APDS.Models.Services
{
    // RSS'i olmayan/çalışmayan haber kaynakları için Gemini grounding (Google Search) ile arama yapar.
    // PlagiarismCheckService'teki gerekçeyle aynı: başlık/link modelin serbest metninden DEĞİL,
    // groundingMetadata.groundingChunks'tan okunuyor (halüsinasyon riskini azaltmak için). Bu yüzden
    // Summary ve PublishedDate burada doldurulmuyor - grounding chunk'ları bu bilgileri vermiyor.
    public class GroundingNewsFetcher : INewsFetcher
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<GroundingNewsFetcher> _logger;

        private const string PromptTemplate = @"Aşağıdaki konuyla ilgili SON 30 GÜN içinde yayınlanmış güncel haber, duyuru veya karar var mı diye Google Search ile ara:

Konu: {0}

Bulduğun kaynakları (en fazla 10 tane) değerlendir. Yanıtında ekstra metin üretmene gerek yok, sadece arama yap.";

        public GroundingNewsFetcher(HttpClient httpClient, IConfiguration config, ILogger<GroundingNewsFetcher> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public NewsFetchMethod FetchMethod => NewsFetchMethod.Grounding;

        public async Task<List<FetchedNewsItem>> FetchAsync(NewsSource source, CancellationToken ct)
        {
            var result = new List<FetchedNewsItem>();

            var apiKey = _config["GeminiApi:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("GeminiApi:ApiKey yapılandırılmamış");
                return result;
            }

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={apiKey}";
            var prompt = string.Format(PromptTemplate, source.Url);

            var requestBody = new
            {
                contents = new object[]
                {
                    new { parts = new object[] { new { text = prompt } } }
                },
                tools = new object[]
                {
                    new { google_search = new { } }
                },
                generationConfig = new
                {
                    temperature = 0,
                    maxOutputTokens = 1024
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API hatası (news grounding, kaynak: {SourceName}): {StatusCode} {Body}", source.Name, response.StatusCode, responseBody);
                return result;
            }

            var responseJson = JObject.Parse(responseBody);
            var candidate = responseJson["candidates"]?[0];
            var groundingChunks = candidate?["groundingMetadata"]?["groundingChunks"] as JArray;

            if (groundingChunks == null)
                return result;

            foreach (var chunk in groundingChunks)
            {
                var web = chunk["web"];
                if (web == null) continue;

                var title = web["title"]?.ToString();
                var link = web["uri"]?.ToString();
                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link) || !UrlSafety.IsHttpUrl(link)) continue;

                result.Add(new FetchedNewsItem(title, link, null, null));
            }

            return result;
        }
    }
}
