using Newtonsoft.Json;

namespace APDS.Models
{
    public class ActivityExtractionResult
    {
        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("authors")]
        public List<string>? Authors { get; set; }

        [JsonProperty("journal")]
        public string? Journal { get; set; }

        [JsonProperty("publicationDate")]
        public string? PublicationDate { get; set; }

        [JsonProperty("doi")]
        public string? Doi { get; set; }

        [JsonProperty("confidence")]
        public ExtractionConfidence? Confidence { get; set; }

        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }

    public class ExtractionConfidence
    {
        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("authors")]
        public string? Authors { get; set; }

        [JsonProperty("journal")]
        public string? Journal { get; set; }

        [JsonProperty("publicationDate")]
        public string? PublicationDate { get; set; }

        [JsonProperty("doi")]
        public string? Doi { get; set; }
    }
}