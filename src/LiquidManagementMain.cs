using Il2Cpp;
using MelonLoader;

namespace LiquidManagement;

// Liquid Management: every liquid container is a real item. It stays when it runs empty and can be filled again, and
// liquids can be poured from one container into another, by choice of source, target and amount.
public sealed class LiquidManagementMain : MelonMod
{
    internal static MelonLogger.Instance Log { get; private set; } = null!;

    private static readonly HashSet<string> s_LoggedErrors = new();

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        Settings.OnLoad();
        uConsole.RegisterCommand("lm_transfer", new Action(TransferMenu.Command));
    }

    public override void OnUpdate()
    {
        try
        {
            TransferMenu.Update();
        }
        catch (Exception e)
        {
            LogErrorOnce("update", e);
        }
    }

    internal static bool IsMenu()
    {
        string? scene = GameManager.m_ActiveScene;
        return string.IsNullOrEmpty(scene) || scene!.Contains("MainMenu") || scene == "Boot" || scene == "Empty" || InterfaceManager.IsMainMenuEnabled();
    }

    internal static void LogDebug(string message)
    {
        if (Settings.Options.Debug)
        {
            Log.Msg(message);
        }
    }

    // Per-frame code must not flood the log: each error source is reported once.
    internal static void LogErrorOnce(string where, Exception e)
    {
        if (s_LoggedErrors.Add(where))
        {
            Log.Error($"{where}: {e}");
        }
    }
}
