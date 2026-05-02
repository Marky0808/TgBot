namespace WebApplication1.Models;
using System.Text.Json.Serialization;

public class DeepLModels
{
    public class Language
    {
        [JsonPropertyName("language")] public string Code { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("supports_formality")] public bool SupportsFormality { get; set; }
    }

    public class TranslationRequest
    {
        [JsonPropertyName("text")] public List<string> Text { get; set; }
        [JsonPropertyName("target_lang")] public string TargetLang { get; set; }
    }

    public class TranslationResponse
    {
        [JsonPropertyName("translations")] public List<Translation> Translations { get; set; }
    }

    public class Translation
    {
        [JsonPropertyName("detected_source_language")] public string DetectedSourceLanguage { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; }
    }
    
    public class CreateTranslationDto
    {
        public string Text { get; set; }
        public string TargetLang { get; set; }
    }
}