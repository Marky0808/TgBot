using System.Text.Json;
using WebApplication1.Models;

namespace WebApplication1.Storage;

public class JsonStorage
{
    private readonly string _filePath = "translations_db.json";

    public async Task<List<SavedTranslation>> GetAllAsync()
    {
        if (!File.Exists(_filePath)) return new List<SavedTranslation>();
        
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<SavedTranslation>>(json) ?? new List<SavedTranslation>();
    }

    public async Task SaveAllAsync(List<SavedTranslation> data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }
}