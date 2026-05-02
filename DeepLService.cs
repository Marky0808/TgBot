using System.Text;
using System.Text.Json;
using WebApplication1.Models;

namespace WebApplication1.Services;

public class DeepLService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "https://api-free.deepl.com/v2";

    public DeepLService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        string apiKey = config["DeepL:ApiKey"] ?? throw new Exception("API Key is missing in appsettings.json");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {apiKey}");
    }

    public async Task<List<DeepLModels.Language>> GetSupportedLanguagesAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/languages?type=target");
        return await ProcessResponseAsync<List<DeepLModels.Language>>(response);
    }

    public async Task<DeepLModels.TranslationResponse> TranslateTextAsync(string text, string targetLang)
    {
        var requestBody = new DeepLModels.TranslationRequest
        {
            Text = new List<string> { text },
            TargetLang = targetLang.ToUpper()
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}/translate", jsonContent);
            
        return await ProcessResponseAsync<DeepLModels.TranslationResponse>(response);
    }

    private async Task<T> ProcessResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"DeepL API Error: {response.StatusCode} - {content}");
        }
        return JsonSerializer.Deserialize<T>(content);
    }
}