namespace AgentFrameworkSolution.Domain.ValueObjects;

/// <summary>
/// Represents the supported languages for image analysis.
/// </summary>
public enum SupportedLanguage
{
    English = 0,
    Spanish = 1,
    Italian = 2,
    French = 3,
    German = 4
}

public static class SupportedLanguageExtensions
{
    /// <summary>
    /// Gets the language name for use in Ollama prompts.
    /// </summary>
    public static string GetLanguageName(this SupportedLanguage language) =>
        language switch
        {
            SupportedLanguage.English => "English",
            SupportedLanguage.Spanish => "Spanish",
            SupportedLanguage.Italian => "Italian",
            SupportedLanguage.French => "French",
            SupportedLanguage.German => "German",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Invalid language")
        };

    /// <summary>
    /// Tries to parse a language string to a SupportedLanguage enum value.
    /// </summary>
    public static bool TryParse(string? value, out SupportedLanguage language)
    {
        language = SupportedLanguage.English;

        if (string.IsNullOrWhiteSpace(value))
            return true; // Default to English

        return Enum.TryParse<SupportedLanguage>(value, ignoreCase: true, out language);
    }
}
