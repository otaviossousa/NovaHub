using System.IO;
using System.Text.Json;

namespace NovaLite.Desktop;

internal sealed record AppSettings(string Theme = "light-blue", string Language = "pt-BR");

internal static class AppSettingsStore
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NovaHub", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(Path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new()
                : new();
        }
        catch (Exception)
        {
            return new();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(settings));
        }
        catch (Exception) { }
    }

    public static void UpdateTheme(string theme) => Save(Load() with { Theme = theme });
    public static void UpdateLanguage(string language) => Save(Load() with { Language = language });
}
