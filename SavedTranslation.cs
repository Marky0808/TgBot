namespace WebApplication1.Models;

public class SavedTranslation
{
    public Guid Id { get; set; }
    public string OriginalText { get; set; }
    public string TranslatedText { get; set; }
    public string SourceLanguage { get; set; }
    public string TargetLanguage { get; set; }
    public DateTime CreatedAt { get; set; }
}