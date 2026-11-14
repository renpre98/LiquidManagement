using Il2Cpp;
using Il2CppTLD.Gear;
using UnityEngine;

namespace LiquidManagement;

/// <summary>
/// For other mods: the tank of a machine (e.g. the Generator Mod's generator) in the transfer menu - filled from the
/// player's containers, or poured back into them, container and amount by choice. Reference LiquidManagement.dll (the
/// Generator Mod does, it requires Liquid Management), or find this type by reflection
/// ("LiquidManagement.MachineTankApi") to keep Liquid Management optional and check <see cref="Version"/> first.
/// </summary>
public static class MachineTankApi
{
    /// <summary>Changes when a method's signature changes.</summary>
    public const int Version = 2;

    /// <summary>
    /// Opens the transfer menu to fill a machine's tank. Returns false when the menu did not open (nothing to pour:
    /// the player is told).
    /// </summary>
    /// <param name="name">The tank's name in the menu ("Generator").</param>
    /// <param name="accepts">Which liquids go into the tank.</param>
    /// <param name="liters">What is in it now, in liters (asked again before pouring).</param>
    /// <param name="capacityLiters">How much it holds.</param>
    /// <param name="add">Puts liters of a liquid in and returns how much went in.</param>
    public static bool OpenFill(string name, Func<LiquidType, bool> accepts, Func<float> liters, float capacityLiters, Func<LiquidType, float, float> add)
    {
        try
        {
            if (TransferMenu.Active)
            {
                return false;
            }
            var tank = new MachineVessel(name, accepts, liters, capacityLiters, add);
            List<Vessel> vessels = new() { tank };
            // whale oil first: lantern fuel also feeds the player's lanterns
            vessels.AddRange(ContainerVessel.InInventory().OrderBy(v => Liquids.Same(v.Liquid, Liquids.WhaleOil) ? 0 : 1));
            if (tank.SpaceUnits <= 0L)
            {
                HUDMessage.AddMessage(Localization.Get("LM_TankFull"));
                return false;
            }
            TransferMenu.Open(vessels, null, null, tank, EnablePanelOnExit.None);
            return TransferMenu.Active;
        }
        catch (Exception e)
        {
            LiquidManagementMain.LogErrorOnce("machine tank", e);
            return false;
        }
    }

    /// <summary>
    /// Opens the transfer menu to pour a machine's tank back into the player's containers. A tank holding a mix (the
    /// generator's drum: whale oil and lantern fuel) shows up once per liquid, each going into its own containers.
    /// Returns false when the menu did not open (nothing to pour or no room: the player is told).
    /// </summary>
    /// <param name="name">The tank's name in the menu ("Generator").</param>
    /// <param name="liquids">The liquids it may hold.</param>
    /// <param name="liters">How much of a liquid is in it now, in liters (asked again before pouring).</param>
    /// <param name="take">Takes liters of a liquid out and returns how much came out.</param>
    /// <param name="add">Puts liters of a liquid back in (when a container took less) and returns how much went in.</param>
    public static bool OpenPourOut(string name, LiquidType[] liquids, Func<LiquidType, float> liters, Func<LiquidType, float, float> take, Func<LiquidType, float, float> add)
    {
        try
        {
            if (TransferMenu.Active)
            {
                return false;
            }
            List<Vessel> sources = liquids.Where(l => l != null)
                .Select(l => (Vessel)new MachineSourceVessel(name, l, liters, take, add))
                .Where(v => !v.IsEmpty)
                .ToList();
            List<Vessel> vessels = new(sources);
            vessels.AddRange(ContainerVessel.InInventory());
            TransferMenu.Open(vessels, null, sources, null, EnablePanelOnExit.None);
            return TransferMenu.Active;
        }
        catch (Exception e)
        {
            LiquidManagementMain.LogErrorOnce("machine tank pour out", e);
            return false;
        }
    }
}

/// <summary>A machine's tank of another mod (MachineTankApi): only ever a target; the mod keeps its contents.</summary>
internal sealed class MachineVessel : Vessel
{
    private readonly string m_Name;
    private readonly Func<LiquidType, bool> m_Accepts;
    private readonly Func<float> m_Liters;
    private readonly float m_Capacity;
    private readonly Func<LiquidType, float, float> m_Add;

    internal MachineVessel(string name, Func<LiquidType, bool> accepts, Func<float> liters, float capacityLiters, Func<LiquidType, float, float> add)
    {
        m_Name = name;
        m_Accepts = accepts;
        m_Liters = liters;
        m_Capacity = capacityLiters;
        m_Add = add;
    }

    internal override string Name => m_Name;
    internal override Texture? Icon => null;
    // it may hold several liquids (the generator's drum takes a mix): never a source
    internal override LiquidType? Liquid => null;
    internal override long Units => Liquids.FromLiters(Math.Max(0f, m_Liters())).m_Units;
    internal override long CapacityUnits => Liquids.FromLiters(m_Capacity).m_Units;
    internal override bool Alive => true;
    internal override bool IsMachine => true;

    internal override bool Accepts(LiquidType type) => m_Accepts(type);

    internal override long Remove(long units) => 0L;

    internal override long Add(LiquidType type, long units)
    {
        if (units <= 0L || !m_Accepts(type))
        {
            return 0L;
        }
        float put = m_Add(type, Liquids.ToLiters(Math.Min(units, SpaceUnits)));
        return Math.Clamp(Liquids.FromLiters(put).m_Units, 0L, units);
    }
}

/// <summary>One liquid in a machine's tank of another mod (MachineTankApi.OpenPourOut): only ever a source.</summary>
internal sealed class MachineSourceVessel : Vessel
{
    private readonly string m_Name;
    private readonly LiquidType m_Liquid;
    private readonly Func<LiquidType, float> m_Liters;
    private readonly Func<LiquidType, float, float> m_Take;
    private readonly Func<LiquidType, float, float> m_Add;

    internal MachineSourceVessel(string name, LiquidType liquid, Func<LiquidType, float> liters, Func<LiquidType, float, float> take, Func<LiquidType, float, float> add)
    {
        m_Name = name;
        m_Liquid = liquid;
        m_Liters = liters;
        m_Take = take;
        m_Add = add;
    }

    internal override string Name => $"{m_Name} ({Liquids.DisplayName(m_Liquid)})";
    internal override Texture? Icon => null;
    internal override LiquidType? Liquid => m_Liquid;
    internal override long Units => Liquids.FromLiters(Math.Max(0f, m_Liters(m_Liquid))).m_Units;
    internal override long CapacityUnits => Units;
    internal override bool Alive => true;
    internal override bool ShowsCapacity => false;

    // never a target
    internal override bool Accepts(LiquidType type) => false;

    internal override long Remove(long units)
    {
        if (units <= 0L)
        {
            return 0L;
        }
        float taken = m_Take(m_Liquid, Liquids.ToLiters(Math.Min(units, Units)));
        return Math.Clamp(Liquids.FromLiters(taken).m_Units, 0L, units);
    }

    // only what was just taken out goes back (the container took less)
    internal override long Add(LiquidType type, long units)
    {
        if (units <= 0L || !Liquids.Same(type, m_Liquid))
        {
            return 0L;
        }
        return Math.Clamp(Liquids.FromLiters(m_Add(type, Liquids.ToLiters(units))).m_Units, 0L, units);
    }
}
