using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using APDS.Models;

namespace APDS.Models.Services
{
    public interface IPdfExtractionService
    {
        Task<ActivityExtractionResult> ExtractFromPdfAsync(byte[] pdfBytes, string fileName);
    }

    public class PdfExtractionService : IPdfExtractionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<PdfExtractionService> _logger;

        private const string ExtractionPrompt = @"Bu akademik makaleden aşağıdaki alanları çıkar. Sadece geçerli JSON döndür, başka hiçbir metin, açıklama veya markdown code fence ekleme.

{
  ""title"": string veya null,
  ""authors"": string dizisi veya null,
  ""journal"": string veya null,
  ""publicationDate"": ""YYYY-MM-DD"" formatında veya null,
  ""doi"": string veya null,
  ""confidence"": {
    ""title"": ""high"" | ""medium"" | ""low"",
    ""authors"": ""high"" | ""medium"" | ""low"",
    ""journal"": ""high"" | ""medium"" | ""low"",
    ""publicationDate"": ""high"" | ""medium"" | ""low"",
    ""doi"": ""high"" | ""medium"" | ""low""
  }
}

Emin olmadığın alanlarda null döndür ve confidence'ı ""low"" yap. Tahmin uydurma.";

        public PdfExtractionService(HttpClient httpClient, IConfiguration config, ILogger<PdfExtractionService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<ActivityExtractionResult> ExtractFromPdfAsync(byte[] pdfBytes, string fileName)
        {
            try
            {
                var apiKey = _config["GeminiApi:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("GeminiApi:ApiKey yapılandırılmamış");
                    return new ActivityExtractionResult { Success = false, ErrorMessage = "API anahtarı yapılandırılmamış" };
                }

                var base64Pdf = Convert.ToBase64String(pdfBytes);
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={apiKey}";

                var requestBody = new
                {
                    contents = new object[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = ExtractionPrompt },
                                new { inline_data = new { mime_type = "application/pdf", data = base64Pdf } }
                            }
                        }
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
                    _logger.LogError("Gemini API hatası: {StatusCode} {Body}", response.StatusCode, responseBody);
                    return new ActivityExtractionResult { Success = false, ErrorMessage = "PDF analiz servisi yanıt vermedi" };
                }

                var responseJson = JObject.Parse(responseBody);
                var textBlock = responseJson["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                if (string.IsNullOrWhiteSpace(textBlock))
                {
                    _logger.LogWarning("Gemini boş yanıt döndü: {Body}", responseBody);
                    return new ActivityExtractionResult { Success = false, ErrorMessage = "Boş yanıt alındı" };
                }

                var cleaned = textBlock.Trim();
                if (cleaned.StartsWith("```"))
                {
                    cleaned = cleaned.Replace("```json", "").Replace("```", "").Trim();
                }

                var result = JsonConvert.DeserializeObject<ActivityExtractionResult>(cleaned);
                if (result == null)
                {
                    return new ActivityExtractionResult { Success = false, ErrorMessage = "Yanıt ayrıştırılamadı" };
                }

                result.Success = true;
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "PDF çıkarım JSON ayrıştırma hatası: {FileName}", fileName);
                return new ActivityExtractionResult { Success = false, ErrorMessage = "Yanıt ayrıştırılamadı" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF çıkarım hatası: {FileName}", fileName);
                return new ActivityExtractionResult { Success = false, ErrorMessage = "Beklenmeyen bir hata oluştu" };
            }
        }
    }
}