using HarmonyLib;
using Il2Cpp;

namespace LiquidManagement;

// "Pour" in the inventory: a fuel container has nothing to examine in the game (no harvest, repair or refuel), so the
// inventory offers only "Drop" for it. Here its examine button is shown, named "Pour", and opens the transfer menu
// instead of the examine screen (Panel_Inventory.OnExamine: mouse, keyboard and controller). The inventory comes back
// when the menu closes.
internal static class InventoryButton
{
    private static bool IsContainer(GearItem? gi)
    {
        return gi != null && gi.m_LiquidItem != null && Liquids.IsFuel(gi.m_LiquidItem.m_LiquidType);
    }

    [HarmonyPatch(typeof(ItemDescriptionPage), nameof(ItemDescriptionPage.CanExamine))]
    private static class CanExamine
    {
        private static void Postfix(GearItem gi, ref bool __result)
        {
            try
            {
                if (!__result && IsContainer(gi))
                {
                    __result = true;
                }
            }
            catch (Exception e)
            {
                LiquidManagementMain.LogErrorOnce("can examine", e);
            }
        }
    }

    [HarmonyPatch(typeof(ItemDescriptionPage), nameof(ItemDescriptionPage.GetExamineButtonLocalizationID))]
    private static class ButtonText
    {
        private static void Postfix(GearItem gi, ref string __result)
        {
            try
            {
                if (IsContainer(gi))
                {
                    __result = "LM_Pour";
                }
            }
            catch (Exception e)
            {
                LiquidManagementMain.LogErrorOnce("examine button text", e);
            }
        }
    }

    [HarmonyPatch(typeof(Panel_Inventory), nameof(Panel_Inventory.OnExamine))]
    private static class Examine
    {
        private static bool Prefix(Panel_Inventory __instance)
        {
            try
            {
                GearItem? gi = __instance.GetCurrentlySelectedItem()?.m_GearItem;
                if (!IsContainer(gi))
                {
                    return true;
                }
                List<ContainerVessel> all = ContainerVessel.InInventory();
                ContainerVessel? vessel = all.FirstOrDefault(v => v.Gear == gi);
                List<Vessel> vessels = all.Cast<Vessel>().ToList();
                if (vessel == null || !TransferMenu.CanPour(vessel, vessels))
                {
                    HUDMessage.AddMessage(Localization.Get("LM_NothingToPour"));
                    return false;
                }
                // like the game's water drop (Panel_Inventory.OnDrop): the inventory pauses and hides behind the menu
                // and comes back as it was (selection, filter) when the menu closes (EnablePanelOnExit.Inventory)
                __instance.PauseAndHide();
                TransferMenu.Open(vessels, vessel, null, EnablePanelOnExit.Inventory);
                if (!TransferMenu.Active)
                {
                    __instance.Enable(true, false);
                }
                return false;
            }
            catch (Exception e)
            {
                LiquidManagementMain.LogErrorOnce("inventory pour", e);
                return true;
            }
        }
    }
}
