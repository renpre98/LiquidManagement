using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;

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

    /// <summary>The liquid's name in the player's language.</summary>
    internal static string DisplayName(LiquidType? type)
    {
        if (type == null)
        {
            return "";
        }
        try
        {
            return Localization.Get(type.LiquidName.m_LocalizationID);
        }
        catch (Exception)
        {
            return type.name;
        }
    }

    internal static float Liters(LiquidItem item) => item.m_Liquid.ToQuantity(1f);

    internal static long UnitsPerLiter => ItemLiquidVolume.Liter.m_Units;

    internal static ItemLiquidVolume Volume(long units) => new() { m_Units = units };

    internal static ItemLiquidVolume FromLiters(float liters) => Volume((long)Math.Round(liters * UnitsPerLiter));

    internal static float ToLiters(long units) => units / (float)UnitsPerLiter;

    /// <summary>An amount in the player's units (liters or gallons), with the unit.</summary>
    internal static string Format(long units) => Volume(units).ToFormattedStringWithUnitsNoOunces();

    /// <summary>An amount in the player's units, without the unit.</summary>
    internal static string FormatNumber(long units) => Volume(units).ToFormattedStringNoOunces();
}
