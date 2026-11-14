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

    [Section("Pouring")]

    [Name("Amount steps")]
    [Description("How much the arrows change the amount in the transfer menu. Auto: finer the less can be poured (0.05 l below 0.5 l, 0.1 l below 2 l, 0.25 l below 5 l, else 0.5 l).")]
    [Choice("Auto", "0.05 l", "0.1 l", "0.25 l", "0.5 l")]
    public int AmountStep = 0;

    [Section("Car tanks")]

    [Name("Siphon car tanks")]
    [Description("With a siphon hose (made at a workbench from cured gut), lantern fuel can be siphoned out of a car's tank behind its open fuel cover. Off: no siphoning, and the hose's recipe is hidden. Some find it too easy.")]
    public bool Siphoning = true;

    [Name("Fuel in a car, least")]
    [Description("Lantern fuel a car's tank holds at least, in liters. Each tank is found with a random amount between least and most, the first time it is siphoned.")]
    [Slider(0f, 40f, 41)]
    public float CarFuelMin = 5f;

    [Name("Fuel in a car, most")]
    [Description("Lantern fuel a car's tank holds at most, in liters.")]
    [Slider(0f, 40f, 41)]
    public float CarFuelMax = 20f;

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
