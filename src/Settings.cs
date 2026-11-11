using ModSettings;

namespace LiquidManagement;

internal sealed class LiquidSettings : JsonModSettings
{
    [Section("Containers")]

    [Name("Keep empty lantern fuel containers")]
    [Description("Lantern fuel cans stay when they run empty, ready to be filled again. The game throws them away.")]
    public bool KeepLanternFuel = true;

    [Name("Keep empty whale oil containers")]
    [Description("Whale oil cans stay when they run empty, ready to be filled again. The game throws them away.")]
    public bool KeepWhaleOil = true;

    [Section("Debug")]

    [Name("Debug log")]
    [Description("Writes what the mod does into the MelonLoader log.")]
    public bool Debug = false;
}

internal static class Settings
{
    internal static LiquidSettings Options { get; private set; } = null!;

    internal static void OnLoad()
    {
        Options = new LiquidSettings();
        Options.AddToModSettings("Liquid Management");
    }
}
