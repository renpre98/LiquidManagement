using System.Text.Json;
using HarmonyLib;
using Il2Cpp;
using ModData;

namespace LiquidManagement;

/// <summary>What the mod keeps in the save (a ModData file next to the game's save): what is left in the car tanks.</summary>
internal sealed class SaveData
{
    internal const int CurrentFormat = 1;

    public int Format { get; set; } = CurrentFormat;

    /// <summary>Car tank (its fuel cover's ObjectGuid, see CarTanks) -> lantern fuel left in it, liters; missing = never
    /// siphoned.</summary>
    public Dictionary<string, float> CarTanks { get; set; } = new();
}

internal static class Save
{
    private static readonly ModDataManager s_Data = new("LiquidManagement", false);

    private static SaveData s_SaveData = new();
    private static bool s_Loaded;

    /// <summary>The current save's data, loaded on first use.</summary>
    internal static SaveData Data
    {
        get
        {
            EnsureLoaded();
            return s_SaveData;
        }
    }

    /// <summary>Back in the main menu: the next game loads its own.</summary>
    internal static void Reset()
    {
        s_SaveData = new SaveData();
        s_Loaded = false;
    }

    private static void EnsureLoaded()
    {
        if (s_Loaded || LiquidManagementMain.IsMenu())
        {
            return;
        }
        string? json = s_Data.Load();
        s_SaveData = string.IsNullOrEmpty(json) ? new SaveData() : JsonSerializer.Deserialize<SaveData>(json!) ?? new SaveData();
        s_SaveData.Format = SaveData.CurrentFormat;
        s_Loaded = true;
        LiquidManagementMain.LogDebug($"Loaded {s_SaveData.CarTanks.Count} car tank(s)");
    }

    [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.SaveSceneData))]
    private static class SaveWithScene
    {
        private static void Postfix()
        {
            // never overwrite the saved data with an empty one that was not loaded
            if (!s_Loaded)
            {
                return;
            }
            try
            {
                s_Data.Save(JsonSerializer.Serialize(s_SaveData));
                LiquidManagementMain.LogDebug($"Saved {s_SaveData.CarTanks.Count} car tank(s)");
            }
            catch (Exception e)
            {
                LiquidManagementMain.LogErrorOnce("save", e);
            }
        }
    }
}
