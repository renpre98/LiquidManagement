using Il2Cpp;
using Il2CppTLD.Gear;

namespace LiquidManagement;

// The game's liquids (LiquidType, ScriptableObjects). Whale oil comes with the Iron Road DLC; without it, or before
// the game has loaded its liquids, a getter may fail or give null.
internal static class Liquids
{
    internal static LiquidType? Kerosene => Get(LiquidType.GetKerosene);
    internal static LiquidType? WhaleOil => Get(LiquidType.GetWhaleOil);

    private static LiquidType? Get(Func<LiquidType> get)
    {
        try
        {
            LiquidType type = get();
            return type == null ? null : type;
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static bool Same(LiquidType? a, LiquidType? b) => a != null && b != null && a == b;

    internal static bool IsFuel(LiquidType? type) => Same(type, Kerosene) || Same(type, WhaleOil);

    internal static string Name(LiquidType? type) => type == null ? "no liquid" : type.name;

    internal static float Liters(LiquidItem item) => item.m_Liquid.ToQuantity(1f);
}
