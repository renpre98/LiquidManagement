using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using UnityEngine;

namespace LiquidManagement;

// The transfer menu: the game's own liquid amount menu (Panel_PickWater, which drops water or moves it between the
// inventory and a cupboard) in its generic mode (SetWaterSupplyForGenericTransfer: the game calls back with the
// amount chosen, or zero when cancelled, see ExitInterface). Two rows are added above the amount, made from copies of
// its amount label and arrows: the source and the target, each switched with its arrows. The amount steps come from
// the settings (Panel_PickWater.s_DeltaLiters, put back when the menu closes).
internal static class TransferMenu
{
    private const string Sound = "Play_SndActionRefuelLantern";

    private static bool s_Active;
    // the game calls back once when its menu closes; until then (and only then) a callback is ours
    private static bool s_Awaiting;
    private static int s_DisabledFrame = -1;
    private static List<Vessel> s_Vessels = new();
    private static List<Vessel> s_Sources = new();
    private static List<Vessel> s_Targets = new();
    private static int s_Source;
    private static int s_Target;
    private static Vessel? s_FixedTarget;
    private static EnablePanelOnExit s_Back;
    private static ItemLiquidVolume s_GameDelta;

    private static Row? s_SourceRow;
    private static Row? s_TargetRow;
    private static Panel_PickWater? s_RowsPanel;

    internal static bool Active => s_Active;

    private static Vessel? Source => s_Source >= 0 && s_Source < s_Sources.Count ? s_Sources[s_Source] : null;
    private static Vessel? Target => s_Target >= 0 && s_Target < s_Targets.Count ? s_Targets[s_Target] : null;

    /// <summary>Is there anything to pour, with this vessel as source or target?</summary>
    internal static bool CanPour(Vessel vessel, List<Vessel> vessels)
    {
        foreach (Vessel other in vessels)
        {
            if (other == vessel)
            {
                continue;
            }
            if (Pourable(vessel, other) || Pourable(other, vessel))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Pourable(Vessel from, Vessel to)
    {
        return !from.IsEmpty && from.Liquid != null && to.SpaceUnits > 0L && to.Accepts(from.Liquid);
    }

    /// <summary>Opens the menu. <paramref name="preferred"/> is the vessel it was opened for (examined): the source
    /// when it holds something to pour, else the target. <paramref name="onlySources"/> limits the sources (a car's
    /// tank, the liquids of a machine's tank); a fixed target (a machine's tank) cannot be switched.</summary>
    internal static void Open(List<Vessel> vessels, Vessel? preferred, List<Vessel>? onlySources, Vessel? fixedTarget, EnablePanelOnExit back)
    {
        if (s_Active)
        {
            return;
        }
        s_Vessels = vessels;
        s_FixedTarget = fixedTarget;
        s_Back = back;
        s_Sources = vessels.Where(v => v != fixedTarget && (onlySources == null || onlySources.Contains(v)) && !v.IsEmpty && v.Liquid != null
            && (fixedTarget != null ? Pourable(v, fixedTarget) : vessels.Any(t => t != v && Pourable(v, t)))).ToList();
        if (s_Sources.Count == 0)
        {
            HUDMessage.AddMessage(Localization.Get("LM_NothingToPour"));
            return;
        }
        s_Source = 0;
        Vessel? wantedTarget = null;
        if (preferred != null)
        {
            int index = s_Sources.IndexOf(preferred);
            if (index >= 0)
            {
                s_Source = index;
            }
            else
            {
                // an empty (or full) container was examined: fill it from the first source that can
                index = s_Sources.FindIndex(s => Pourable(s, preferred));
                if (index >= 0)
                {
                    s_Source = index;
                    wantedTarget = preferred;
                }
            }
        }
        BuildTargets(wantedTarget);

        Panel_PickWater panel = InterfaceManager.GetPanel<Panel_PickWater>();
        WaterSupply? ws = GameManager.GetInventoryComponent()?.GetPotableWaterSupply()?.m_WaterSupply;
        if (panel == null || ws == null)
        {
            LiquidManagementMain.Log.Warning("Transfer menu: the game's liquid menu is not there");
            return;
        }
        s_Active = true;
        s_Awaiting = true;
        s_DisabledFrame = -1;
        s_GameDelta = Panel_PickWater.s_DeltaLiters;
        try
        {
            EnsureRows(panel);
            panel.Enable(true);
            System.Action<ItemLiquidVolume> done = OnClosed;
            panel.SetWaterSupplyForGenericTransfer(ws, Liquids.Volume(MaxUnits()), done);
            panel.m_EnablePanelOnExit = back;
            s_Focus = Focus.Amount;
            SetRowsActive(true);
            SetIconSmall(panel, true);
            ApplyPair(panel, toMax: true);
        }
        catch (Exception e)
        {
            LiquidManagementMain.LogErrorOnce("transfer menu open", e);
            Close();
            ForgetCallback(panel);
        }
    }

    private static void BuildTargets(Vessel? wanted)
    {
        Vessel? source = Source;
        s_Targets = s_FixedTarget != null
            ? new List<Vessel> { s_FixedTarget }
            : source == null ? new List<Vessel>() : s_Vessels.Where(v => v != source && Pourable(source, v)).ToList();
        s_Target = wanted != null ? Math.Max(0, s_Targets.IndexOf(wanted)) : 0;
    }

    private static long MaxUnits()
    {
        Vessel? source = Source, target = Target;
        return source == null || target == null ? 0L : Math.Min(source.Units, target.SpaceUnits);
    }

    /// <summary>The amount steps: fixed in the settings, or by the most that can be poured.</summary>
    private static float StepLiters(long maxUnits)
    {
        float max = Liquids.ToLiters(maxUnits);
        return Settings.Options.AmountStep switch
        {
            1 => 0.05f,
            2 => 0.1f,
            3 => 0.25f,
            4 => 0.5f,
            _ => max >= 5f ? 0.5f : max >= 2f ? 0.25f : max >= 0.5f ? 0.1f : 0.05f,
        };
    }

    // a new source or target: new limits, the amount kept where it fits (or the most, when asked)
    private static void ApplyPair(Panel_PickWater panel, bool toMax)
    {
        long max = MaxUnits();
        Panel_PickWater.s_DeltaLiters = Liquids.FromLiters(StepLiters(max));
        panel.m_maxLiters = Liquids.Volume(max);
        long amount = toMax ? max : Math.Min(panel.m_numLiters.m_Units, max);
        panel.m_numLiters = Liquids.Volume(Math.Max(0L, amount));
        panel.Refresh();
    }

    private static void Step(Panel_PickWater panel, bool source, int direction)
    {
        if (!s_Active)
        {
            return;
        }
        if (source)
        {
            if (s_Sources.Count < 2)
            {
                return;
            }
            Vessel? keep = Target;
            s_Source = (s_Source + direction + s_Sources.Count) % s_Sources.Count;
            BuildTargets(keep);
        }
        else
        {
            if (s_Targets.Count < 2)
            {
                return;
            }
            s_Target = (s_Target + direction + s_Targets.Count) % s_Targets.Count;
        }
        ApplyPair(panel, toMax: true);
    }

    // the game closes its menu: the amount chosen, or zero when cancelled
    private static void OnClosed(ItemLiquidVolume amount)
    {
        if (!s_Awaiting)
        {
            return;
        }
        Panel_PickWater panel = InterfaceManager.GetPanel<Panel_PickWater>();
        Vessel? source = Source, target = Target;
        Close();
        ForgetCallback(panel);
        ReturnTo(s_Back);
        if (amount.m_Units <= 0L || source == null || target == null || source.Liquid == null)
        {
            return;
        }
        LiquidType liquid = source.Liquid;
        long units = amount.m_Units;
        float liters = Liquids.ToLiters(units);
        Work(source.WorkTextKey, Math.Max(source.MinimumMinutes, liters), () => Pour(source, target, liquid, units));
    }

    // In its generic mode the game's menu does not bring back the panel it came from (ExitInterface skips that for
    // GenericTransfer): done here as the game does for its own modes, e.g. the inventory as it was (Enable(true,
    // resetFilter false), after Panel_Inventory.PauseAndHide).
    private static void ReturnTo(EnablePanelOnExit back)
    {
        try
        {
            if (back == EnablePanelOnExit.Inventory)
            {
                InterfaceManager.GetPanel<Panel_Inventory>()?.Enable(true, false);
            }
        }
        catch (Exception e)
        {
            LiquidManagementMain.LogErrorOnce("transfer menu return", e);
        }
    }

    // the menu is gone: the rows too, the game's steps back
    private static void Close()
    {
        if (!s_Active)
        {
            return;
        }
        s_Active = false;
        Panel_PickWater.s_DeltaLiters = s_GameDelta;
        SetRowsActive(false);
        if (s_RowsPanel != null && s_RowsPanel)
        {
            SetIconSmall(s_RowsPanel, false);
        }
        RestoreAmountColor(s_RowsPanel);
    }

    // the game keeps the callback after its menu closed: another of its transfers must not call it again
    private static void ForgetCallback(Panel_PickWater? panel)
    {
        s_Awaiting = false;
        s_DisabledFrame = -1;
        if (panel != null)
        {
            panel.m_OnGenericTransferWater = null;
        }
    }

    /// <summary>Each frame: the menu was closed without its callback (the game calls it right after disabling the
    /// menu, in the same call): forget it.</summary>
    internal static void Update()
    {
        if (s_Awaiting && s_DisabledFrame >= 0 && Time.frameCount > s_DisabledFrame)
        {
            ForgetCallback(InterfaceManager.GetPanel<Panel_PickWater>());
        }
    }

    private static void Pour(Vessel source, Vessel target, LiquidType liquid, long units)
    {
        if (!source.Alive || !target.Alive || !Liquids.Same(source.Liquid, liquid) || !target.Accepts(liquid))
        {
            return;
        }
        long taken = source.Remove(Math.Min(units, Math.Min(source.Units, target.SpaceUnits)));
        long put = target.Add(liquid, taken);
        if (put < taken)
        {
            source.Add(liquid, taken - put);
        }
        HUDMessage.AddMessage(string.Format(Localization.Get(source.DoneTextKey), Liquids.Format(put), Liquids.DisplayName(liquid)));
        LiquidManagementMain.LogDebug($"Poured {Liquids.ToLiters(put):0.000} l {Liquids.Name(liquid)}: {source.Name} -> {target.Name}");
        Panel_Inventory_Examine examine = InterfaceManager.GetPanel<Panel_Inventory_Examine>();
        if (examine != null && examine.IsEnabled())
        {
            examine.RefreshMainWindow();
        }
    }

    /// <summary>The game's progress bar: takes game time, can be cancelled; done runs only when it finished.</summary>
    private static void Work(string textKey, float gameMinutes, Action done)
    {
        Action<bool, bool, float> exit = (success, _, _) =>
        {
            if (success)
            {
                try
                {
                    done();
                }
                catch (Exception e)
                {
                    LiquidManagementMain.LogErrorOnce("pour", e);
                }
            }
        };
        InterfaceManager.GetPanel<Panel_GenericProgressBar>().Launch(
            Localization.Get(textKey),
            Math.Clamp(1.5f + gameMinutes * 0.5f, 1.5f, 5f), // real seconds
            gameMinutes,
            0f,
            Sound,
            null,
            false,
            true,
            (OnExitDelegate)exit);
    }

    // ---------------------------------------------------------------- the two rows

    private sealed class Row
    {
        public GameObject Label = null!;
        public UILabel Text = null!;
        public GameObject Previous = null!;
        public GameObject Next = null!;

        public void SetActive(bool active)
        {
            Label.SetActive(active);
            Previous.SetActive(active);
            Next.SetActive(active);
        }
    }

    // Layout (the game's menu, local to the amount label's parent): title at the top centre, its description below,
    // the icon on the left of the amount row with the amount and its arrows to the right. The two rows go centred
    // where the description is (emptied while ours) and below it; the icon is made smaller and moved down a little
    // meanwhile, so the second row clears it.
    private const float RowHalfWidth = 185f;
    private const float RowSpacing = 26f;
    private const int IconSize = 64;
    private const float IconDown = 6f;

    private static Vector3 s_IconPosition;
    private static int s_IconWidth;
    private static int s_IconHeight;
    private static bool s_IconChanged;

    private static void EnsureRows(Panel_PickWater panel)
    {
        if (s_RowsPanel == panel && s_SourceRow != null && s_SourceRow.Label != null && s_SourceRow.Label)
        {
            return;
        }
        UILabel amount = panel.m_Label_NumUnits;
        Transform parent = amount.transform.parent;
        Vector3 title = parent.InverseTransformPoint(panel.m_Label_Title.transform.position);
        Vector3 description = parent.InverseTransformPoint(panel.m_Label_Description.transform.position);
        float top = description.y - 2f;
        s_SourceRow = MakeRow(panel, title.x, top, true);
        s_TargetRow = MakeRow(panel, title.x, top - RowSpacing, false);
        s_RowsPanel = panel;
        LiquidManagementMain.LogDebug($"Transfer menu: amount label at {amount.transform.localPosition} ({amount.width}x{amount.height}, font {amount.fontSize}), "
            + $"arrows at {panel.m_ButtonDecrease.transform.localPosition} / {panel.m_ButtonIncrease.transform.localPosition}, "
            + $"title at {title}, description at {description}, icon at {parent.InverseTransformPoint(panel.m_GearIcon.transform.position)} "
            + $"({panel.m_GearIcon.width}x{panel.m_GearIcon.height}), buttons at {parent.InverseTransformPoint(panel.m_Execute_Button.transform.position)} / "
            + $"{parent.InverseTransformPoint(panel.m_ExecuteAll_Button.transform.position)}; rows at y {top} and {top - RowSpacing}");
    }

    private static Row MakeRow(Panel_PickWater panel, float x, float y, bool source)
    {
        UILabel amount = panel.m_Label_NumUnits;
        Transform parent = amount.transform.parent;
        GameObject label = UnityEngine.Object.Instantiate(amount.gameObject, parent);
        label.name = source ? "LiquidManagement_SourceRow" : "LiquidManagement_TargetRow";
        label.transform.localPosition = new Vector3(x, y, amount.transform.localPosition.z);
        UILabel text = label.GetComponent<UILabel>();
        text.pivot = UIWidget.Pivot.Center;
        text.alignment = NGUIText.Alignment.Center;
        text.overflowMethod = UILabel.Overflow.ShrinkContent;
        text.width = (int)(2f * RowHalfWidth - 50f);
        text.fontSize = Math.Max(12, (int)(amount.fontSize * 0.8f));
        UILocalize? localize = label.GetComponent<UILocalize>();
        if (localize != null)
        {
            UnityEngine.Object.Destroy(localize);
        }

        GameObject Arrow(GameObject original, int direction)
        {
            GameObject arrow = UnityEngine.Object.Instantiate(original, original.transform.parent);
            arrow.name = label.name + (direction < 0 ? "_Previous" : "_Next");
            Vector3 at = arrow.transform.parent.InverseTransformPoint(parent.TransformPoint(new Vector3(x + direction * RowHalfWidth, y, 0f)));
            arrow.transform.localPosition = new Vector3(at.x, at.y, original.transform.localPosition.z);
            foreach (UIButton button in arrow.GetComponentsInChildren<UIButton>(true))
            {
                EventDelegate.Set(button.onClick, (EventDelegate.Callback)(System.Action)(() => Step(panel, source, direction)));
            }
            return arrow;
        }

        Row row = new()
        {
            Label = label,
            Text = text,
            Previous = Arrow(panel.m_ButtonDecrease, -1),
            Next = Arrow(panel.m_ButtonIncrease, 1),
        };
        row.SetActive(false);
        return row;
    }

    private static void SetIconSmall(Panel_PickWater panel, bool small)
    {
        UITexture icon = panel.m_GearIcon;
        if (icon == null || small == s_IconChanged)
        {
            return;
        }
        if (small)
        {
            s_IconPosition = icon.transform.localPosition;
            s_IconWidth = icon.width;
            s_IconHeight = icon.height;
            icon.width = IconSize;
            icon.height = IconSize;
            icon.transform.localPosition = s_IconPosition - new Vector3(0f, IconDown, 0f);
        }
        else
        {
            icon.width = s_IconWidth;
            icon.height = s_IconHeight;
            icon.transform.localPosition = s_IconPosition;
        }
        s_IconChanged = small;
    }

    private static void SetRowsActive(bool active)
    {
        if (s_SourceRow?.Label != null && s_SourceRow.Label)
        {
            s_SourceRow.SetActive(active);
        }
        if (s_TargetRow?.Label != null && s_TargetRow.Label)
        {
            s_TargetRow.SetActive(active);
        }
    }

    private static string Describe(Vessel vessel)
    {
        return vessel.ShowsCapacity
            ? $"{vessel.Name}  {Liquids.FormatNumber(vessel.Units)} / {Liquids.Format(vessel.CapacityUnits)}"
            : $"{vessel.Name}  {Liquids.Format(vessel.Units)}";
    }

    // after the game's own refresh (its texts are about water): our title, icon and rows
    private static void ShowTexts(Panel_PickWater panel)
    {
        Vessel? source = Source, target = Target;
        if (source == null || target == null)
        {
            return;
        }
        panel.m_Label_Title.text = target.IsMachine
            ? string.Format(Localization.Get("LM_FillTitle"), target.Name)
            : string.Format(Localization.Get("LM_TransferTitle"), Liquids.DisplayName(source.Liquid));
        panel.m_Label_Description.text = "";
        // a machine's tank has no icon of its own: the container's
        Texture? icon = source.Icon ?? target.Icon;
        if (icon != null)
        {
            panel.m_GearIcon.mainTexture = icon;
        }
        if (s_SourceRow != null)
        {
            s_SourceRow.Text.text = $"{Localization.Get("LM_From")}  {Describe(source)}";
            s_SourceRow.Previous.SetActive(s_Sources.Count > 1);
            s_SourceRow.Next.SetActive(s_Sources.Count > 1);
        }
        if (s_TargetRow != null)
        {
            s_TargetRow.Text.text = $"{Localization.Get("LM_To")}  {Describe(target)}";
            s_TargetRow.Previous.SetActive(s_Targets.Count > 1);
            s_TargetRow.Next.SetActive(s_Targets.Count > 1);
        }
        ShowFocus(panel);
    }

    [HarmonyPatch(typeof(Panel_PickWater), nameof(Panel_PickWater.Refresh))]
    private static class RefreshTexts
    {
        private static void Postfix(Panel_PickWater __instance)
        {
            if (!s_Active)
            {
                return;
            }
            try
            {
                ShowTexts(__instance);
            }
            catch (Exception e)
            {
                LiquidManagementMain.LogErrorOnce("transfer menu texts", e);
            }
        }
    }

    // Keyboard and controller: up/down choose the row (from, into, amount; only rows with a choice), left/right
    // change it. The game's menu reads only left/right, for the amount: on another row its change is undone. The
    // chosen row is bright, the others dimmed. Opening, the amount is chosen, so it starts like the game's menu.
    private enum Focus
    {
        Source,
        Target,
        Amount,
    }

    private static Focus s_Focus = Focus.Amount;
    private static Color s_AmountColor;
    private static bool s_AmountColorSaved;
    private static long s_UnitsBeforeUpdate;
    private static bool s_MovedThisFrame;

    private static List<Focus> Rows()
    {
        List<Focus> rows = new();
        if (s_Sources.Count > 1)
        {
            rows.Add(Focus.Source);
        }
        if (s_FixedTarget == null && s_Targets.Count > 1)
        {
            rows.Add(Focus.Target);
        }
        rows.Add(Focus.Amount);
        return rows;
    }

    private static void ShowFocus(Panel_PickWater panel)
    {
        if (!s_AmountColorSaved)
        {
            s_AmountColor = panel.m_Label_NumUnits.color;
            s_AmountColorSaved = true;
        }
        Color bright = s_AmountColor;
        Color dim = new(bright.r, bright.g, bright.b, bright.a * 0.45f);
        bool many = Rows().Count > 1;
        if (s_SourceRow != null)
        {
            s_SourceRow.Text.color = !many || s_Focus == Focus.Source ? bright : dim;
        }
        if (s_TargetRow != null)
        {
            s_TargetRow.Text.color = !many || s_Focus == Focus.Target ? bright : dim;
        }
        panel.m_Label_NumUnits.color = !many || s_Focus == Focus.Amount ? bright : dim;
    }

    private static void RestoreAmountColor(Panel_PickWater? panel)
    {
        if (s_AmountColorSaved && panel != null && panel)
        {
            panel.m_Label_NumUnits.color = s_AmountColor;
        }
        s_AmountColorSaved = false;
    }

    [HarmonyPatch(typeof(Panel_PickWater), nameof(Panel_PickWater.Update))]
    private static class Keys
    {
        private static void Prefix(Panel_PickWater __instance)
        {
            if (!s_Active || !__instance.IsEnabled())
            {
                return;
            }
            try
            {
                List<Focus> rows = Rows();
                if (!rows.Contains(s_Focus))
                {
                    s_Focus = Focus.Amount;
                }
                float vertical = Utils.GetMenuMovementVertical(__instance, true, true);
                // the game changes the amount with up/down too; with rows to move between, up/down only moves
                s_MovedThisFrame = vertical != 0f && rows.Count > 1;
                if (s_MovedThisFrame)
                {
                    // up: the row above
                    int index = Math.Clamp(rows.IndexOf(s_Focus) + (vertical > 0f ? -1 : 1), 0, rows.Count - 1);
                    s_Focus = rows[index];
                    ShowFocus(__instance);
                }
                if (s_Focus != Focus.Amount)
                {
                    float horizontal = Utils.GetMenuMovementHorizontal(__instance, true, true);
                    if (horizontal != 0f)
                    {
                        Step(__instance, s_Focus == Focus.Source, horizontal > 0f ? 1 : -1);
                    }
                }
                s_UnitsBeforeUpdate = __instance.m_numLiters.m_Units;
            }
            catch (Exception e)
            {
                LiquidManagementMain.LogErrorOnce("transfer menu keys", e);
            }
        }

        private static void Postfix(Panel_PickWater __instance)
        {
            bool moved = s_MovedThisFrame;
            s_MovedThisFrame = false;
            if (!s_Active || (s_Focus == Focus.Amount && !moved) || __instance.m_numLiters.m_Units == s_UnitsBeforeUpdate)
            {
                return;
            }
            // left/right belonged to another row, or up/down moved between the rows
            __instance.m_numLiters = Liquids.Volume(s_UnitsBeforeUpdate);
            __instance.Refresh();
        }
    }

    // the game's menu is disabled (it calls back right after, or never when e.g. a load closes it): the rows go
    // away, the steps come back
    [HarmonyPatch(typeof(Panel_PickWater), nameof(Panel_PickWater.Enable))]
    private static class Disabled
    {
        private static void Postfix(Panel_PickWater __instance, bool enable)
        {
            if (!enable && s_Active)
            {
                Close();
                s_DisabledFrame = Time.frameCount;
            }
        }
    }

    /// <summary>Console: lm_transfer opens the menu for the containers in the inventory.</summary>
    internal static void Command()
    {
        List<Vessel> vessels = ContainerVessel.InInventory().Cast<Vessel>().ToList();
        Open(vessels, null, null, null, EnablePanelOnExit.None);
    }
}
