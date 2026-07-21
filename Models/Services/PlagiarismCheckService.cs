using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using APDS.Models;

namespace APDS.Models.Services
{
    public class PlagiarismCheckResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public double? SimilarityScore { get; set; }
        public List<PlagiarismFoundSource>? FoundSources { get; set; }
    }

    public class PlagiarismFoundSource
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
    }

    // Gemini'nin kendi JSON çıktısını parse etmek için ara model (yalnızca similarityScore ve
    // kısa gerekçe için kullanılıyor - kaynak URL'leri buradan DEĞİL, groundingMetadata'dan alınıyor,
    // çünkü modelin serbest metinde ürettiği URL'ler halüsinasyon olabilir; grounding chunk'ları
    // gerçekten yapılan aramadan gelen doğrulanmış kaynaklardır)
    internal class GeminiSimilarityJson
    {
        public double? SimilarityScore { get; set; }
        public string? Reasoning { get; set; }
    }

    public interface IPlagiarismCheckService
    {
        Task<PlagiarismCheckResult> CheckAsync(string title, string description);
    }

    public class PlagiarismCheckService : IPlagiarismCheckService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<PlagiarismCheckService> _logger;

        private const string CheckPromptTemplate = @"Aşağıdaki akademik aktivite başlığı ve açıklamasını, web'de bulabildiğin benzer/aynı içerikli kaynaklarla karşılaştır. Google Search ile ara ve gerçekten benzer bulduğun kaynaklara dayanarak değerlendir.

Başlık: {0}
Açıklama: {1}

Değerlendirmenin sonunda SADECE aşağıdaki formatta bir JSON bloğu döndür (başka metin, açıklama veya markdown code fence EKLEME, sadece JSON):

{{
  ""similarityScore"": 0.0 ile 1.0 arası sayı (0 = tamamen özgün, 1 = birebir aynı/intihal),
  ""reasoning"": ""kısa Türkçe gerekçe, en fazla 2 cümle""
}}

Emin değilsen düşük bir skor ver ve gerekçede belirt. Skor uydurma, sadece bulduğun kaynaklara dayan.";

        public PlagiarismCheckService(HttpClient httpClient, IConfiguration config, ILogger<PlagiarismCheckService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<PlagiarismCheckResult> CheckAsync(string title, string description)
        {
            try
            {
                var apiKey = _config["GeminiApi:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("GeminiApi:ApiKey yapılandırılmamış");
                    return new PlagiarismCheckResult { Success = false, ErrorMessage = "API anahtarı yapılandırılmamış" };
                }

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={apiKey}";
                var prompt = string.Format(CheckPromptTemplate, title, description ?? string.Empty);

                var requestBody = new
                {
                    contents = new object[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = prompt }
                            }
                        }
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
                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Gemini API hatası (plagiarism check): {StatusCode} {Body}", response.StatusCode, responseBody);
                    return new PlagiarismCheckResult { Success = false, ErrorMessage = "Plagiarism check servisi yanıt vermedi" };
                }

                var responseJson = JObject.Parse(responseBody);
                var candidate = responseJson["candidates"]?[0];
                var textBlock = candidate?["content"]?["parts"]?[0]?["text"]?.ToString();

                if (string.IsNullOrWhiteSpace(textBlock))
                {
                    _logger.LogWarning("Gemini plagiarism check boş yanıt döndü: {Body}", responseBody);
                    return new PlagiarismCheckResult { Success = false, ErrorMessage = "Boş yanıt alındı" };
                }

                var cleaned = textBlock.Trim();
                if (cleaned.StartsWith("```"))
                {
                    cleaned = cleaned.Replace("```json", "").Replace("```", "").Trim();
                }

                GeminiSimilarityJson? similarity;
                try
                {
                    similarity = JsonConvert.DeserializeObject<GeminiSimilarityJson>(cleaned);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Plagiarism check yanıtı JSON olarak ayrıştırılamadı: {Text}", cleaned);
                    return new PlagiarismCheckResult { Success = false, ErrorMessage = "Yanıt ayrıştırılamadı" };
                }

                if (similarity == null)
                {
                    return new PlagiarismCheckResult { Success = false, ErrorMessage = "Yanıt ayrıştırılamadı" };
                }

                // Gerçek kaynaklar modelin ürettiği metinden değil, grounding metadata'dan alınıyor -
                // bunlar Google Search'ün gerçekten döndürdüğü, model tarafından uydurulamayan sonuçlar.
                var foundSources = new List<PlagiarismFoundSource>();
                var groundingChunks = candidate?["groundingMetadata"]?["groundingChunks"] as JArray;
                if (groundingChunks != null)
                {
                    foreach (var chunk in groundingChunks)
                    {
                        var web = chunk["web"];
                        if (web == null) continue;

                        var sourceUrl = web["uri"]?.ToString();
                        if (!UrlSafety.IsHttpUrl(sourceUrl)) continue;

                        foundSources.Add(new PlagiarismFoundSource
                        {
                            Title = web["title"]?.ToString(),
                            Url = sourceUrl
                        });
                    }
                }

                return new PlagiarismCheckResult
                {
                    Success = true,
                    SimilarityScore = similarity.SimilarityScore,
                    FoundSources = foundSources
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plagiarism check hatası. Title: {Title}", title);
                return new PlagiarismCheckResult { Success = false, ErrorMessage = "Beklenmeyen bir hata oluştu" };
            }
        }
    }
}