using System.Globalization;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.Interactions;
using UnityEngine;
using UnityEngine.Events;

namespace LiquidManagement;

// Lantern fuel from the cars' tanks (moved here from the Generator Mod; the fuel covers were found by Modelfinder).
// Every car with a fuel filler cover (sedans, trucks, railway trucks: 140 in the regions) can be opened in the game
// ("Fuel Cover", OpenClose). Behind an open cover the mod puts the tank's filler neck, a small hitbox; with a siphon
// hose (GEAR_LM_Hose, made at a workbench) the transfer menu opens there with the tank as the source. Each tank holds
// a random amount (settings) found the first time it is siphoned; what is left is kept in the save, by the cover's id
// (its ObjectGuid; it keeps its place when opened). A car's prefab: <car> / Car...FuelCover_Prefab (OpenClose,
// ObjectGuid; "into the car" is its -x) / ...Hinge / OBJ_Car...FuelCover (the cover's mesh, renderer and collider).
// The cars of some regions are in sub-scenes loaded later (Mountain Town), so the scene is searched again after every
// scene load. Switched off in the settings, there are no filler necks.
internal static class CarTanks
{
    private const string CoverMesh = "FuelCover";
    // the scene's objects are complete a moment after a scene is loaded
    private const float ScanDelaySeconds = 1f;
    private const float CheckSeconds = 0.2f;
    // the filler neck behind the cover, in the cover prefab's space: from 5 cm inside to 2 cm out of the body, so it is
    // in front where the cover was (only there while the cover is open)
    private static readonly Vector3 NeckCenter = new(-0.015f, 0f, 0f);
    private static readonly Vector3 NeckSize = new(0.07f, 0.09f, 0.09f);

    private sealed class Tank
    {
        public OpenClose Cover = null!;
        public GameObject Neck = null!;
        public BoxCollider Box = null!;
    }

    private static bool s_Pending;
    private static float s_ScanAt;
    private static float s_CheckAt;
    private static bool s_On;
    // by the cover prefab's instance id
    private static readonly Dictionary<int, Tank> s_Tanks = new();

    /// <summary>Any scene was loaded (also a region's sub-scenes): search again a moment later.</summary>
    internal static void OnSceneLoaded()
    {
        s_Pending = true;
        s_ScanAt = Time.realtimeSinceStartup + ScanDelaySeconds;
    }

    internal static void Update()
    {
        bool on = Settings.Options.Siphoning;
        if (on != s_On)
        {
            // switched in the settings: the necks go, or are searched for
            s_On = on;
            if (on)
            {
                OnSceneLoaded();
            }
            else
            {
                RemoveAll();
            }
        }
        if (!on || LiquidManagementMain.IsMenu())
        {
            return;
        }
        float now = Time.realtimeSinceStartup;
        if (s_Pending && now >= s_ScanAt)
        {
            s_Pending = false;
            Scan();
        }
        if (s_Tanks.Count == 0 || now < s_CheckAt)
        {
            return;
        }
        s_CheckAt = now + CheckSeconds;
        List<int>? gone = null;
        foreach (KeyValuePair<int, Tank> entry in s_Tanks)
        {
            Tank t = entry.Value;
            if (t.Cover == null || !t.Cover || t.Neck == null || !t.Neck)
            {
                (gone ??= new List<int>()).Add(entry.Key);
                continue;
            }
            // the neck only while the cover is open (else the cover itself is to be clicked)
            bool open = t.Cover.m_IsOpen && !t.Cover.m_Animating;
            if (t.Box.enabled != open)
            {
                t.Box.enabled = open;
            }
        }
        if (gone != null)
        {
            foreach (int id in gone)
            {
                Remove(id);
            }
        }
    }

    private static void Remove(int id)
    {
        if (s_Tanks.TryGetValue(id, out Tank? t) && t.Neck != null && t.Neck)
        {
            UnityEngine.Object.Destroy(t.Neck);
        }
        s_Tanks.Remove(id);
    }

    private static void RemoveAll()
    {
        foreach (int id in s_Tanks.Keys.ToList())
        {
            Remove(id);
        }
    }

    // the covers by their mesh (CarSedanFuelCoverMesh, CarTruckFuelCoverMesh, CarRailwayTruckFuelCoverMesh), shown
    private static void Scan()
    {
        string? scene = GameManager.m_ActiveScene;
        if (string.IsNullOrEmpty(scene))
        {
            return;
        }
        int added = 0;
        foreach (MeshFilter filter in UnityEngine.Object.FindObjectsOfType<MeshFilter>())
        {
            if (filter == null || !filter)
            {
                continue;
            }
            Mesh? mesh = filter.sharedMesh;
            Renderer? renderer = filter.GetComponent<Renderer>();
            if (mesh == null || !mesh || !mesh.name.Contains(CoverMesh, StringComparison.Ordinal) || renderer == null || !renderer)
            {
                continue;
            }
            OpenClose? cover = filter.GetComponentInParent<OpenClose>();
            if (cover == null || !cover || s_Tanks.ContainsKey(cover.GetInstanceID()))
            {
                continue;
            }
            s_Tanks[cover.GetInstanceID()] = AddNeck(scene!, cover);
            added++;
        }
        if (added > 0)
        {
            LiquidManagementMain.LogDebug($"{scene}: {added} car tank(s) found, {s_Tanks.Count} in all");
        }
    }

    private static Tank AddNeck(string scene, OpenClose cover)
    {
        Transform t = cover.transform;
        string? guid = null;
        try
        {
            guid = ObjectGuid.GetGuidFromGameObject(cover.gameObject, false);
        }
        catch (Exception)
        {
            // no id: by its place
        }
        string key = !string.IsNullOrEmpty(guid)
            ? guid!
            : string.Format(CultureInfo.InvariantCulture, "{0}|{1:F1},{2:F1},{3:F1}", scene, t.position.x, t.position.y, t.position.z);

        // not under the car: the game resolves a hit to the topmost parent on the same layer (the cover's own)
        var neck = new GameObject("LiquidManagement_CarTankNeck");
        neck.layer = vp_Layer.InteractiveProp;
        neck.transform.SetPositionAndRotation(t.TransformPoint(NeckCenter), t.rotation);
        neck.transform.localScale = t.lossyScale;
        BoxCollider box = neck.AddComponent<BoxCollider>();
        box.size = NeckSize;
        SimpleInteraction si = neck.AddComponent<SimpleInteraction>();
        si.m_CanInteract = true;
        si.m_DefaultHoverText = new LocalizedString { m_LocalizationID = "LM_CarTank" };
        si.AddEventCallback(InteractionEventType.PerformInteraction, (UnityAction<BaseInteraction>)(_ => Siphon(key)));
        box.enabled = cover.m_IsOpen;
        return new Tank { Cover = cover, Neck = neck, Box = box };
    }

    private static float Roll()
    {
        float a = Settings.Options.CarFuelMin, b = Settings.Options.CarFuelMax;
        return Mathf.Round(UnityEngine.Random.Range(Math.Min(a, b), Math.Max(a, b)) * 10f) / 10f;
    }

    private static void Siphon(string key)
    {
        try
        {
            if (!Settings.Options.Siphoning)
            {
                return;
            }
            GearItem? hose = GameManager.GetInventoryComponent().GetNonRuinedItem(HoseItem.Id);
            if (hose == null || !hose)
            {
                Fail("LM_NeedHose");
                return;
            }
            Dictionary<string, float> tanks = Save.Data.CarTanks;
            if (!tanks.TryGetValue(key, out float left))
            {
                left = Roll();
                tanks[key] = left;
                LiquidManagementMain.LogDebug($"Car tank {key}: {left:0.0} l");
            }
            var tank = new CarTankVessel(key, hose);
            if (tank.IsEmpty || tank.Liquid == null)
            {
                Fail("LM_CarTankEmpty");
                return;
            }
            List<Vessel> vessels = new() { tank };
            vessels.AddRange(ContainerVessel.InInventory());
            if (!vessels.Any(v => v != tank && v.SpaceUnits > 0L && v.Accepts(tank.Liquid)))
            {
                Fail("LM_NoLanternFuelSpace");
                return;
            }
            TransferMenu.Open(vessels, tank, new List<Vessel> { tank }, null, EnablePanelOnExit.None);
        }
        catch (Exception e)
        {
            LiquidManagementMain.LogErrorOnce("siphon", e);
        }
    }

    private static void Fail(string key)
    {
        HUDMessage.AddMessage(Localization.Get(key));
        GameAudioManager.PlayGUIError();
    }
}

/// <summary>A car's tank (CarTanks): lantern fuel, only ever a source; what is left is in the save.</summary>
internal sealed class CarTankVessel : Vessel
{
    private readonly string m_Key;
    private readonly GearItem m_Hose;

    internal CarTankVessel(string key, GearItem hose)
    {
        m_Key = key;
        m_Hose = hose;
    }

    private float Left
    {
        get => Save.Data.CarTanks.TryGetValue(m_Key, out float l) ? l : 0f;
        set => Save.Data.CarTanks[m_Key] = Math.Max(0f, value);
    }

    internal override string Name => Localization.Get("LM_CarTank");
    // the hose it is siphoned with
    internal override Texture? Icon => m_Hose != null && m_Hose ? m_Hose.GetInventoryIconTexture() : null;
    internal override LiquidType? Liquid => Liquids.Kerosene;
    internal override long Units => Liquids.FromLiters(Left).m_Units;
    internal override long CapacityUnits => Units;
    internal override bool Alive => true;
    internal override bool ShowsCapacity => false;
    internal override string WorkTextKey => "LM_Siphoning";
    internal override string DoneTextKey => "LM_Siphoned";
    // a liter a game minute through the hose, at least two minutes
    internal override float MinimumMinutes => 2f;

    // never a target
    internal override bool Accepts(LiquidType type) => false;

    internal override long Remove(long units)
    {
        long taken = Math.Clamp(units, 0L, Units);
        Left = Liquids.ToLiters(Units - taken);
        LiquidManagementMain.LogDebug($"Car tank {m_Key}: {Liquids.ToLiters(taken):0.00} l siphoned, {Left:0.00} l left");
        return taken;
    }

    // only what was just taken out goes back (the target was full)
    internal override long Add(LiquidType type, long units)
    {
        long put = Liquids.Same(type, Liquid) ? Math.Max(0L, units) : 0L;
        Left = Liquids.ToLiters(Units + put);
        return put;
    }
}
