using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace LiquidManagement;

// The siphon hose (GEAR_LM_Hose) is a ModComponent item (Distributable/auto-mapped, bundle from the
// LiquidManagementUnity project) whose prefab is only a shell: an empty root with a collider and a placeholder cube.
// Every hose object gets its real look here: HoseGeometry's coil in the cured gut's material, so no game geometry or
// texture is shipped.
internal static class HoseItem
{
    internal const string Id = "GEAR_LM_Hose";
    /// <summary>Its blueprint (ModComponent: "BP_" + the blueprint file's name).</summary>
    internal const string Blueprint = "BP_LiquidManagement_Hose";
    private const string PlaceholderName = "Placeholder";
    /// <summary>The cured gut item: the hose takes its material (texture GEAR_Gut_Cured_Dif); the prefab is never instantiated.</summary>
    private const string CuredGutKey = "GEAR_GutDried";
    private const float InspectMinDistance = 0.5f;
    private const float InspectDistancePerMeter = 1.9f; // the hose fills about half the screen height

    private static Mesh? s_Mesh;
    private static Material? s_Material;

    private static bool IsHose(GameObject go) => go.name.StartsWith(Id, StringComparison.Ordinal);

    // built once, shared by all hoses
    private static bool EnsureLook()
    {
        if (!s_Material)
        {
            GameObject? gut = Addressables.LoadAssetAsync<GameObject>(CuredGutKey).WaitForCompletion();
            MeshRenderer? renderer = gut != null ? gut.GetComponentInChildren<MeshRenderer>(true) : null;
            s_Material = renderer != null ? renderer.sharedMaterial : null;
            if (!s_Material)
            {
                LiquidManagementMain.Log.Warning("Hose: the cured gut's material did not load, the hose keeps its placeholder.");
                return false;
            }
        }
        if (!s_Mesh)
        {
            s_Mesh = HoseGeometry.Build(HoseGeometry.CentreLine(), "LiquidManagement_Hose");
        }
        return true;
    }

    private static void Dress(GearItem item)
    {
        GameObject go = item.gameObject;
        if (!IsHose(go))
        {
            return;
        }
        Transform? placeholder = go.transform.Find(PlaceholderName);
        if (placeholder == null || !placeholder || !EnsureLook())
        {
            return; // already dressed, or the cube stays (the item still works)
        }
        // the coil resting on the ground at the item's origin
        Bounds mesh = s_Mesh!.bounds;
        var hose = new GameObject("Hose");
        hose.layer = go.layer;
        hose.transform.SetParent(go.transform, false);
        hose.transform.localPosition = -new Vector3(mesh.center.x, mesh.min.y, mesh.center.z);
        hose.AddComponent<MeshFilter>().sharedMesh = s_Mesh;
        hose.AddComponent<MeshRenderer>().sharedMaterial = s_Material;
        UnityEngine.Object.Destroy(placeholder.gameObject);
        var bounds = new Bounds(new Vector3(0f, mesh.size.y * 0.5f, 0f), mesh.size);
        BoxCollider? box = go.GetComponent<BoxCollider>();
        if (box)
        {
            box!.center = bounds.center;
            box.size = bounds.size;
        }
        FitInspect(go, bounds);
    }

    // The inspect view (picking up) puts the item's pivot in front of the camera and then moves it by Inspect.m_Offset
    // in its own axes. The hose's pivot is at the bottom (it rests on the ground), so the offset is minus the centre of
    // its look, and the distance fits its size.
    private static void FitInspect(GameObject go, Bounds bounds)
    {
        Inspect? inspect = go.GetComponent<Inspect>();
        if (inspect == null || !inspect)
        {
            return;
        }
        float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        inspect.m_Offset = -bounds.center;
        inspect.m_DistanceFromCamera = Mathf.Max(InspectMinDistance, size * InspectDistancePerMeter);
    }

    [HarmonyPatch(typeof(GearItem), nameof(GearItem.Awake))]
    private static class DressOnAwake
    {
        private static void Postfix(GearItem __instance)
        {
            try
            {
                Dress(__instance);
            }
            catch (Exception e)
            {
                LiquidManagementMain.LogErrorOnce("hose look", e);
            }
        }
    }

    // fitted again when the inspection starts (as the Generator Mod does for its parts), in case the item's inspect
    // values were set after it woke up
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.EnterInspectGearMode),
        new[] { typeof(GearItem), typeof(Container), typeof(IceFishingHole), typeof(Harvestable), typeof(CookingPotItem) })]
    private static class FitInspectOnEnter
    {
        private static void Prefix(GearItem gear)
        {
            try
            {
                if (gear == null || !gear || !IsHose(gear.gameObject) || !s_Mesh)
                {
                    return;
                }
                Bounds mesh = s_Mesh!.bounds;
                FitInspect(gear.gameObject, new Bounds(new Vector3(0f, mesh.size.y * 0.5f, 0f), mesh.size));
            }
            catch (Exception e)
            {
                LiquidManagementMain.LogErrorOnce("hose inspect", e);
            }
        }
    }

    // Siphoning switched off in the settings: the hose's recipe is not shown. A postfix on purpose: ModComponent
    // replaces ItemPassesFilter with its own prefix and complains about other mods that skip the original.
    [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.ItemPassesFilter))]
    private static class HideRecipe
    {
        private static void Postfix(Il2CppTLD.Gear.BlueprintData bpi, ref bool __result)
        {
            if (__result && !Settings.Options.Siphoning && bpi && bpi.name == Blueprint)
            {
                __result = false;
            }
        }
    }
}
