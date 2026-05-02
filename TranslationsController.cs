using WebApplication1.Models;
using WebApplication1.Services;
using WebApplication1.Storage;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;
using WebApplication1.Storage;

namespace Lab7.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranslationsController : ControllerBase
{
    private readonly DeepLService _deepLService;
    private readonly JsonStorage _storage;

    public TranslationsController(DeepLService deepLService, JsonStorage storage)
    {
        _deepLService = deepLService;
        _storage = storage;
    }
    
    [HttpGet("languages")]
    public async Task<IActionResult> GetLanguages()
    {
        try
        {
            var languages = await _deepLService.GetSupportedLanguagesAsync();
            return Ok(languages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Помилка при зверненні до зовнішнього API", details = ex.Message });
        }
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAllSavedTranslations()
    {
        var translations = await _storage.GetAllAsync();
        return Ok(translations);
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTranslationById(Guid id)
    {
        var translations = await _storage.GetAllAsync();
        var translation = translations.FirstOrDefault(t => t.Id == id);

        if (translation == null)
            return NotFound(new { message = $"Переклад з ID {id} не знайдено." });

        return Ok(translation);
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateTranslation([FromBody] DeepLModels.CreateTranslationDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || string.IsNullOrWhiteSpace(request.TargetLang))
            return BadRequest(new { message = "Текст та цільова мова є обов'язковими." });

        try
        {
            var deepLResult = await _deepLService.TranslateTextAsync(request.Text, request.TargetLang);
            var translatedData = deepLResult.Translations.FirstOrDefault();

            if (translatedData == null) return StatusCode(500, "Не вдалося отримати переклад.");
            
            var newRecord = new SavedTranslation
            {
                Id = Guid.NewGuid(),
                OriginalText = request.Text,
                TranslatedText = translatedData.Text,
                SourceLanguage = translatedData.DetectedSourceLanguage,
                TargetLanguage = request.TargetLang.ToUpper(),
                CreatedAt = DateTime.UtcNow
            };
            
            var translations = await _storage.GetAllAsync();
            translations.Add(newRecord);
            await _storage.SaveAllAsync(translations);
            
            return CreatedAtAction(nameof(GetTranslationById), new { id = newRecord.Id }, newRecord);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Внутрішня помилка сервера", details = ex.Message });
        }
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTranslation(Guid id, [FromBody] SavedTranslation updatedRecord)
    {
        var translations = await _storage.GetAllAsync();
        var index = translations.FindIndex(t => t.Id == id);

        if (index == -1)
            return NotFound(new { message = "Переклад не знайдено." });
        
        translations[index].TranslatedText = updatedRecord.TranslatedText;
        translations[index].OriginalText = updatedRecord.OriginalText;
        
        await _storage.SaveAllAsync(translations);

        return NoContent();
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTranslation(Guid id)
    {
        var translations = await _storage.GetAllAsync();
        var translationToRemove = translations.FirstOrDefault(t => t.Id == id);

        if (translationToRemove == null)
            return NotFound(new { message = "Переклад не знайдено." });

        translations.Remove(translationToRemove);
        await _storage.SaveAllAsync(translations);

        return NoContent();
    }
}