using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using UnityEngine;

namespace LiquidManagement;

/// <summary>Somewhere a liquid can be: a container in the inventory, later also the tank of a machine of another
/// mod. Amounts are the game's units (ItemLiquidVolume.m_Units), so nothing is lost to rounding.</summary>
internal abstract class Vessel
{
    internal abstract string Name { get; }
    internal abstract Texture? Icon { get; }
    /// <summary>The liquid it is for. A container keeps its liquid: the game saves only the amount and whether it
    /// is potable, so after a load or a scene change it holds the liquid it was made with again (a lantern fuel can
    /// filled with whale oil would come back with lantern fuel).</summary>
    internal abstract LiquidType? Liquid { get; }
    internal abstract long Units { get; }
    internal abstract long CapacityUnits { get; }
    internal abstract bool Alive { get; }

    internal long SpaceUnits => Math.Max(0L, CapacityUnits - Units);
    internal bool IsEmpty => Units <= 0L;

    /// <summary>Can take this liquid: only its own, empty or not.</summary>
    internal virtual bool Accepts(LiquidType type) => Liquids.Same(Liquid, type);

    /// <summary>Takes out up to that much, returns what came out.</summary>
    internal abstract long Remove(long units);

    /// <summary>Puts in up to that much (as far as there is room), returns what went in.</summary>
    internal abstract long Add(LiquidType type, long units);
}

/// <summary>A liquid container (LiquidItem) in the inventory.</summary>
internal sealed class ContainerVessel : Vessel
{
    internal readonly GearItem Gear;
    private readonly LiquidItem m_Item;

    internal ContainerVessel(GearItem gear, LiquidItem item)
    {
        Gear = gear;
        m_Item = item;
    }

    internal override string Name => Gear.DisplayName;
    internal override Texture? Icon => Gear.GetInventoryIconTexture();
    internal override LiquidType? Liquid => m_Item.m_LiquidType;
    internal override long Units => m_Item.m_Liquid.m_Units;
    internal override long CapacityUnits => m_Item.m_LiquidCapacity.m_Units;
    internal override bool Alive => Gear != null && Gear && m_Item != null && m_Item && Gear.m_InPlayerInventory;

    internal override long Remove(long units)
    {
        long taken = Math.Clamp(units, 0L, Units);
        m_Item.m_Liquid = Liquids.Volume(Units - taken);
        return taken;
    }

    internal override long Add(LiquidType type, long units)
    {
        long put = Liquids.Same(type, Liquid) ? Math.Clamp(units, 0L, SpaceUnits) : 0L;
        m_Item.m_Liquid = Liquids.Volume(Units + put);
        return put;
    }

    /// <summary>The liquid containers in the inventory that hold a liquid this mod handles (phase 1: fuel).</summary>
    internal static List<ContainerVessel> InInventory()
    {
        List<ContainerVessel> list = new();
        Inventory inventory = GameManager.GetInventoryComponent();
        if (inventory == null || inventory.m_Items == null)
        {
            return list;
        }
        for (int i = 0; i < inventory.m_Items.Count; i++)
        {
            GearItem? gear = inventory.m_Items[i]?.m_GearItem;
            LiquidItem? item = gear == null ? null : gear.m_LiquidItem;
            if (gear != null && item != null && item && Liquids.IsFuel(item.m_LiquidType))
            {
                list.Add(new ContainerVessel(gear, item));
            }
        }
        return list;
    }
}
