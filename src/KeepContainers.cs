using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using UnityEngine;

namespace LiquidManagement;

// Empty containers stay. The game destroys every liquid container that runs empty while it takes a liquid out of the
// inventory (PlayerManager.DeductLiquidFromInventory asks ILiquidContainer.ShouldDestroyWhenEmpty, which LiquidItem
// hard-codes to true, then calls Inventory.DestroyGear(GearItem)): lantern fuel cans when a lamp or a generator is
// refuelled, whale oil cans the same. That getter is a 3-byte native body ("return true") which IL2CPP may share with
// other methods, so it must not be patched; instead the destroy call is skipped while a kept liquid is deducted.
internal static class KeepContainers
{
    // the deduction in progress (its liquid), only for the frame it runs in, so a missed postfix cannot leak
    private static int s_Depth;
    private static int s_Frame = -1;
    private static LiquidType? s_Liquid;

    private const float EmptyLiters = 0.001f;

    internal static bool Keep(LiquidType? type)
    {
        return (Settings.Options.KeepLanternFuel && Liquids.Same(type, Liquids.Kerosene))
            || (Settings.Options.KeepWhaleOil && Liquids.Same(type, Liquids.WhaleOil));
    }

    private static bool Deducting => s_Depth > 0 && s_Frame == Time.frameCount;

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.DeductLiquidFromInventory), new[] { typeof(Il2CppTLD.IntBackedUnit.ItemLiquidVolume), typeof(LiquidType) })]
    private static class Deduct
    {
        private static void Prefix(LiquidType liquidType)
        {
            if (s_Frame != Time.frameCount)
            {
                s_Depth = 0;
            }
            s_Depth++;
            s_Frame = Time.frameCount;
            s_Liquid = liquidType;
        }

        private static void Postfix()
        {
            if (--s_Depth <= 0)
            {
                s_Depth = 0;
                s_Liquid = null;
            }
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.DestroyGear), new[] { typeof(GearItem) })]
    private static class DestroyGearItem
    {
        private static bool Prefix(GearItem gi)
        {
            try
            {
                LiquidItem? liquid = gi == null ? null : gi.m_LiquidItem;
                if (liquid == null)
                {
                    return true;
                }
                LiquidType? type = liquid.m_LiquidType;
                if (Deducting && Liquids.Same(type, s_Liquid) && Keep(type) && Liquids.Liters(liquid) < EmptyLiters)
                {
                    LiquidManagementMain.LogDebug($"Kept the empty {gi!.name} ({Liquids.Name(type)})");
                    return false;
                }
                LiquidManagementMain.LogDebug($"Destroyed {gi!.name} ({Liquids.Name(type)}, {Liquids.Liters(liquid):0.000} l){(Deducting ? " while deducting " + Liquids.Name(s_Liquid) : "")}");
            }
            catch (Exception e)
            {
                LiquidManagementMain.LogErrorOnce("keep containers", e);
            }
            return true;
        }
    }

    // Only to find other ways the game throws containers away (debug log).
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.DestroyGear), new[] { typeof(GameObject) })]
    private static class DestroyGameObject
    {
        private static void Prefix(GameObject go)
        {
            if (!Settings.Options.Debug || go == null)
            {
                return;
            }
            LiquidItem? liquid = go.GetComponent<LiquidItem>();
            if (liquid != null)
            {
                LiquidManagementMain.LogDebug($"Destroyed (object) {go.name} ({Liquids.Name(liquid.m_LiquidType)}, {Liquids.Liters(liquid):0.000} l){(Deducting ? " while deducting" : "")}");
            }
        }
    }
}
