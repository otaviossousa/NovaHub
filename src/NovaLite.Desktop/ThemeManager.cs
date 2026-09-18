using System.Windows;
using System.Windows.Media;

namespace NovaLite.Desktop;

public sealed record AppTheme(string Key, string Label, IReadOnlyDictionary<string, string> Colors);

public static class ThemeManager
{
    public static readonly AppTheme[] Themes =
    [
        Create("stellar-white", " Stellar White", "#E8EDEF", "#F7F9FA", "#FFFFFF", "#297D78", "#1E5964", "#56C7AE", "#D7ECE8", "#86CDBD",
            textPrimary: "#18262D", textSecondary: "#51646D", textMuted: "#71858E", inactive: "#667C84",
            controllerBody: "#F5F6F4", controllerSurface: "#E2E7E7", controllerDetail: "#8FA1A5", activeInk: "#10272B",
            circularityWithin: "#258566", circularityBeyond: "#7357B5"),
        Create("space-purple", " Space Purple", "#14121C", "#201C2B", "#292337", "#C9A7FF", "#E1D1FF", "#A88BE8", "#403455", "#614A82",
            controllerBody: "#4A3B60", controllerSurface: "#261E34", controllerDetail: "#8F79AB",
            circularityBeyond: "#F2B85B"),
        Create("black", " Black", "#050505", "#0D0D0F", "#171719", "#FFFFFF", "#FFFFFF", "#F2F2F2", "#252527", "#4A4A4E",
            textPrimary: "#FFFFFF", textSecondary: "#C7C7CB", textMuted: "#929296", inactive: "#A8A8AC",
            controllerBody: "#111113", controllerSurface: "#080809", controllerDetail: "#58585D", activeInk: "#050505",
            circularityBeyond: "#69A8FF"),
        Create("gray", " Gray", "#15191D", "#20262B", "#2B3339", "#BCC8CC", "#F2F4F5", "#92A5AB", "#3A454B", "#56666E",
            controllerBody: "#5A6064", controllerSurface: "#2C3236", controllerDetail: "#A2ABB0"),
        Create("pink", " Pink", "#240B18", "#3B1028", "#58163C", "#FF79B0", "#FFF0F6", "#FF91BF", "#6E204A", "#A92E6B",
            textPrimary: "#FFF7FA", textSecondary: "#F2BED3", textMuted: "#C985A2", inactive: "#D99AB5",
            controllerBody: "#F3A3C4", controllerSurface: "#B84F7B", controllerDetail: "#FFD6E6", activeInk: "#34101F",
            circularityWithin: "#66D6A0", circularityBeyond: "#FFD166"),
        Create("light-blue", " Light Blue", "#0E1821", "#172735", "#20384A", "#86D7FF", "#E2F8FF", "#62C3EE", "#254A61", "#34779B",
            controllerBody: "#79BDD6", controllerSurface: "#326E86", controllerDetail: "#D3F3FF", activeInk: "#0B2634"),
        Create("golden-yellow", " Golden Yellow", "#17130E", "#251F17", "#30281E", "#FFD45F", "#FFF0B2", "#E9B832", "#4C3B25", "#76552A",
            controllerBody: "#D8AA38", controllerSurface: "#7B5415", controllerDetail: "#FFE89A", activeInk: "#322207"),
        Create("midnight-blue", " Midnight Blue", "#050A14", "#091426", "#0E2038", "#6E98D4", "#D1E3FF", "#527FBD", "#18345B", "#254B7D",
            controllerBody: "#142542", controllerSurface: "#071225", controllerDetail: "#4A6795"),
        Create("light-green", " Light Green", "#0D1B16", "#172B22", "#214033", "#86E3AF", "#E4FFF0", "#66D895", "#285B43", "#377E59",
            controllerBody: "#91D7AC", controllerSurface: "#3F8B61", controllerDetail: "#D8F7E4", activeInk: "#0C291A",
            circularityWithin: "#67C9FF", circularityBeyond: "#E5A0FF")
    ];

    public static string CurrentKey { get; private set; } = "light-blue";

    public static void Apply(string? key)
    {
        key = MigrateLegacyKey(key);
        var theme = Themes.FirstOrDefault(item => item.Key == key) ?? Themes[0];
        CurrentKey = theme.Key;
        foreach (var pair in theme.Colors)
            Application.Current.Resources[pair.Key] = MakeBrush(pair.Value);
    }

    public static Brush Brush(FrameworkElement element, string key, string fallback)
        => element.TryFindResource(key) as Brush ?? MakeBrush(fallback);

    public static Brush Brush(string key, string fallback)
        => Application.Current.TryFindResource(key) as Brush ?? MakeBrush(fallback);

    public static string LoadPreference() => MigrateLegacyKey(AppSettingsStore.Load().Theme);

    public static void SavePreference(string key) => AppSettingsStore.UpdateTheme(key);

    private static AppTheme Create(string key, string label, string background, string panel,
        string surface, string accent, string controllerAccent, string active,
        string selected, string activeSurface, string? textPrimary = null,
        string? textSecondary = null, string? textMuted = null, string? inactive = null,
        string? controllerBody = null, string? controllerSurface = null,
        string? controllerDetail = null, string? activeInk = null,
        string? circularityWithin = null, string? circularityBeyond = null)
        => new(key, label, new Dictionary<string, string>
        {
            ["WindowBackgroundBrush"] = background,
            ["PanelBrush"] = panel,
            ["SurfaceBrush"] = surface,
            ["InputBrush"] = Mix(surface, accent, .12),
            ["ButtonBrush"] = Mix(surface, accent, .18),
            ["ToggleBrush"] = Mix(surface, accent, .14),
            ["BorderBrush"] = Mix(surface, accent, .22),
            ["BorderStrongBrush"] = Mix(surface, accent, .34),
            ["ButtonBorderBrush"] = Mix(surface, accent, .30),
            ["AccentBrush"] = accent,
            ["ControllerAccentBrush"] = controllerAccent,
            ["ActiveBrush"] = active,
            ["ActiveInkBrush"] = activeInk ?? background,
            ["SelectedSurfaceBrush"] = selected,
            ["ActiveSurfaceBrush"] = activeSurface,
            ["HoverSurfaceBrush"] = Mix(selected, accent, .18),
            ["ControllerBodyBrush"] = controllerBody ?? Mix(background, controllerAccent, .13),
            ["ControllerSurfaceBrush"] = controllerSurface ?? Mix(background, controllerAccent, .05),
            ["ControllerDetailBrush"] = controllerDetail ?? Mix(background, controllerAccent, .32),
            ["CircularityWithinBrush"] = circularityWithin ?? "#68DDA3",
            ["CircularityBeyondBrush"] = circularityBeyond ?? "#A991FF",
            ["TextPrimaryBrush"] = textPrimary ?? "#E8F0F4",
            ["TextSecondaryBrush"] = textSecondary ?? "#AEBFCB",
            ["TextMutedBrush"] = textMuted ?? "#819CA9",
            ["InactiveBrush"] = inactive ?? "#93ADA9"
        });

    private static string MigrateLegacyKey(string? key) => key switch
    {
        "cyan" => "light-blue",
        "violet" => "space-purple",
        "amber" => "golden-yellow",
        "dark-blue" => "midnight-blue",
        null or "" => "light-blue",
        _ => key
    };

    private static SolidColorBrush MakeBrush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }

    private static string Mix(string first, string second, double amount)
    {
        var a = (Color)ColorConverter.ConvertFromString(first);
        var b = (Color)ColorConverter.ConvertFromString(second);
        byte Blend(byte x, byte y) => (byte)Math.Round(x + (y - x) * amount);
        return $"#{Blend(a.R, b.R):X2}{Blend(a.G, b.G):X2}{Blend(a.B, b.B):X2}";
    }

}
