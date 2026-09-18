using System.Globalization;
using System.Windows;

namespace NovaLite.Desktop;

public sealed record AppLanguage(string Key, string Label);

public static class LanguageManager
{
    public static readonly AppLanguage[] Languages =
    [
        new("pt-BR", "Português (Brasil)"),
        new("en-US", "English")
    ];

    private static readonly IReadOnlyDictionary<string, string> Portuguese = new Dictionary<string, string>
    {
        ["Minimize"] = "Minimizar",
        ["MaximizeRestore"] = "Maximizar ou restaurar",
        ["Close"] = "Fechar",
        ["OpenSettings"] = "Abrir configurações",
        ["ConnectController"] = "Nenhum controle conectado. Conecte um controle e pressione qualquer botão para começar.",
        ["PrecisionTest"] = "Teste de Precisão",
        ["CircularityTest"] = "Teste de Circularidade",
        ["StopTest"] = "Parar Teste",
        ["Vibration"] = "Vibração",
        ["OneSecond"] = "1 segundo",
        ["VibrationIntensityA11y"] = "Intensidade de vibração",
        ["Left"] = "Esquerdo",
        ["Right"] = "Direito",
        ["Both"] = "Ambos",
        ["Continuous"] = "Contínua",
        ["Stop"] = "Parar",
        ["Connection"] = "Conexão:",
        ["ConnectionTypeA11y"] = "Tipo de conexão",
        ["Battery"] = "Bateria:",
        ["ButtonCombinations"] = "Combinações de botões",
        ["VibrationIntensity"] = "Intensidade da Vibração",
        ["VibrationIntensityHelp"] = "Aumenta ou reduz o nível.",
        ["ConnectionMode"] = "Modo de Conexão",
        ["HoldTwoSeconds"] = "segure por 2 s",
        ["ConnectionModeHelp"] = "Alterna o modo do controle.",
        ["StickDeadzone"] = "Zona Morta dos Sticks",
        ["StickDeadzoneHelp"] = "Alterna a zona morta zero.",
        ["SwapLayout"] = "Alternar Layout ABXY",
        ["SwapLayoutHelp"] = "Troca A↔B e X↔Y.",
        ["TurboHelp"] = "Liga ou desliga a repetição.",
        ["Back"] = "←  Voltar",
        ["Settings"] = "Configurações",
        ["Appearance"] = "Aparência",
        ["ApplicationTheme"] = "Tema da aplicação",
        ["ApplicationThemeHelp"] = "Escolha as cores usadas na interface e no controle.",
        ["Language"] = "Idioma",
        ["LanguageHelp"] = "Escolha o idioma usado no NovaHub.",
        ["LanguageA11y"] = "Idioma da aplicação",
        ["UsefulLinks"] = "Links úteis",
        ["OfficialManual"] = "Manual oficial do controle",
        ["OfficialManualHelp"] = "Instruções, conexões, calibração e funções do GameSir Nova Lite.",
        ["OpenManual"] = "Abrir manual  ↗",
        ["Repository"] = "Repositório do NovaHub",
        ["RepositoryHelp"] = "Código-fonte, versões, problemas conhecidos e novidades do projeto.",
        ["OpenRepository"] = "Ver repositório  ↗",
        ["Developer"] = "Desenvolvedor",
        ["DeveloperName"] = "Otavio Sousa · @otaviossousa",
        ["OpenProfile"] = "Ver perfil  ↗",
        ["Contribution"] = "Contribuição",
        ["ContributionTitle"] = "Ajude a manter o NovaHub",
        ["ContributionHelp"] = "Se o projeto foi útil para você e quiser me ajudar com algum valor, fique à vontade. A contribuição é totalmente opcional e o NovaHub continuará gratuito.",
        ["SupportProject"] = "Apoiar o projeto  ↗",
        ["Controller"] = "Controle",
        ["Consulting"] = "consultando",
        ["Active"] = "ativo",
        ["NoController"] = "sem controle",
        ["ApiError"] = "erro de API",
        ["NoControllersConnected"] = "Nenhum controle conectado",
        ["OneControllerConnected"] = "1 controle conectado",
        ["ManyControllersConnected"] = "{0} controles conectados",
        ["OpenNovaHub"] = "Abrir NovaHub",
        ["Exit"] = "Sair",
        ["UsbCable"] = "Cabo USB",
        ["ConnectionIdentification"] = "Identificação da conexão",
        ["NoActiveSession"] = "O controle não está em uma sessão ativa.",
        ["UnableOpenBrowser"] = "Não foi possível abrir o navegador: {0}",
        ["Theme.stellar-white"] = " Branco Estelar",
        ["Theme.space-purple"] = " Roxo Espacial",
        ["Theme.black"] = " Preto",
        ["Theme.gray"] = " Cinza",
        ["Theme.pink"] = " Rosa",
        ["Theme.light-blue"] = " Azul Claro",
        ["Theme.golden-yellow"] = " Amarelo Dourado",
        ["Theme.midnight-blue"] = " Azul Meia-noite",
        ["Theme.light-green"] = " Verde Claro"
    };

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["Minimize"] = "Minimize",
        ["MaximizeRestore"] = "Maximize or restore",
        ["Close"] = "Close",
        ["OpenSettings"] = "Open settings",
        ["ConnectController"] = "No controller connected. Connect a controller and press any button to get started.",
        ["PrecisionTest"] = "Precision Test",
        ["CircularityTest"] = "Circularity Test",
        ["StopTest"] = "Stop Test",
        ["Vibration"] = "Vibration",
        ["OneSecond"] = "1 second",
        ["VibrationIntensityA11y"] = "Vibration intensity",
        ["Left"] = "Left",
        ["Right"] = "Right",
        ["Both"] = "Both",
        ["Continuous"] = "Continuous",
        ["Stop"] = "Stop",
        ["Connection"] = "Connection:",
        ["ConnectionTypeA11y"] = "Connection type",
        ["Battery"] = "Battery:",
        ["ButtonCombinations"] = "Button combinations",
        ["VibrationIntensity"] = "Vibration Intensity",
        ["VibrationIntensityHelp"] = "Increases or decreases the level.",
        ["ConnectionMode"] = "Connection Mode",
        ["HoldTwoSeconds"] = "hold for 2 s",
        ["ConnectionModeHelp"] = "Switches the controller mode.",
        ["StickDeadzone"] = "Stick Deadzone",
        ["StickDeadzoneHelp"] = "Toggles zero deadzone.",
        ["SwapLayout"] = "Switch ABXY Layout",
        ["SwapLayoutHelp"] = "Swaps A↔B and X↔Y.",
        ["TurboHelp"] = "Turns button repeat on or off.",
        ["Back"] = "←  Back",
        ["Settings"] = "Settings",
        ["Appearance"] = "Appearance",
        ["ApplicationTheme"] = "Application theme",
        ["ApplicationThemeHelp"] = "Choose the colors used by the interface and controller.",
        ["Language"] = "Language",
        ["LanguageHelp"] = "Choose the language used by NovaHub.",
        ["LanguageA11y"] = "Application language",
        ["UsefulLinks"] = "Useful links",
        ["OfficialManual"] = "Official controller manual",
        ["OfficialManualHelp"] = "Instructions, connections, calibration, and features of the GameSir Nova Lite.",
        ["OpenManual"] = "Open manual  ↗",
        ["Repository"] = "NovaHub repository",
        ["RepositoryHelp"] = "Source code, releases, known issues, and project updates.",
        ["OpenRepository"] = "View repository  ↗",
        ["Developer"] = "Developer",
        ["DeveloperName"] = "Otavio Sousa · @otaviossousa",
        ["OpenProfile"] = "View profile  ↗",
        ["Contribution"] = "Contribution",
        ["ContributionTitle"] = "Help maintain NovaHub",
        ["ContributionHelp"] = "If this project was useful to you and you would like to help with any amount, feel free. Contributions are entirely optional and NovaHub will remain free.",
        ["SupportProject"] = "Support the project  ↗",
        ["Controller"] = "Controller",
        ["Consulting"] = "checking",
        ["Active"] = "active",
        ["NoController"] = "no controller",
        ["ApiError"] = "API error",
        ["NoControllersConnected"] = "No controllers connected",
        ["OneControllerConnected"] = "1 controller connected",
        ["ManyControllersConnected"] = "{0} controllers connected",
        ["OpenNovaHub"] = "Open NovaHub",
        ["Exit"] = "Exit",
        ["UsbCable"] = "USB cable",
        ["ConnectionIdentification"] = "Connection identification",
        ["NoActiveSession"] = "The controller does not have an active session.",
        ["UnableOpenBrowser"] = "Could not open the browser: {0}",
        ["Theme.stellar-white"] = " Stellar White",
        ["Theme.space-purple"] = " Space Purple",
        ["Theme.black"] = " Black",
        ["Theme.gray"] = " Gray",
        ["Theme.pink"] = " Pink",
        ["Theme.light-blue"] = " Light Blue",
        ["Theme.golden-yellow"] = " Golden Yellow",
        ["Theme.midnight-blue"] = " Midnight Blue",
        ["Theme.light-green"] = " Light Green"
    };

    public static string CurrentKey { get; private set; } = "pt-BR";

    public static void Apply(string? key)
    {
        var language = Languages.FirstOrDefault(item => item.Key == key) ?? Languages[0];
        CurrentKey = language.Key;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(language.Key);
        var values = language.Key == "en-US" ? English : Portuguese;
        foreach (var pair in values)
            Application.Current.Resources[$"Loc.{pair.Key}"] = pair.Value;
    }

    public static string Get(string key) =>
        (Application.Current.TryFindResource($"Loc.{key}") as string) ?? key;

    public static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    public static string LoadPreference() => AppSettingsStore.Load().Language;
    public static void SavePreference(string key) => AppSettingsStore.UpdateLanguage(key);
    public static string ThemeLabel(string key) => Get($"Theme.{key}");
}
