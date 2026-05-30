using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace DiagDump;

[BepInPlugin("maxenterme.DiagDump", "DiagDump", "4.0.5")]
public class Plugin : BaseUnityPlugin
{
    internal static Plugin Instance = null!;

    private void Awake()
    {
        Instance = this;

        // Enable Harmony debug logging
        #pragma warning disable CS0618
        Harmony.DEBUG = true;
        #pragma warning restore CS0618
        Logger.LogInfo("Harmony DEBUG enabled");

        try
        {
            var harmony = new Harmony("maxenterme.DiagDump");
            harmony.PatchAll();
            Logger.LogInfo("DiagDump Harmony PatchAll succeeded");
        }
        catch (Exception e)
        {
            Logger.LogError($"DiagDump Harmony error: {e}");
        }

        Logger.LogInfo("DiagDump v5 loaded");
    }

    internal static void Log(string msg)
    {
        Instance.Logger.LogInfo(msg);
    }

    internal static void LogWarn(string msg)
    {
        Instance.Logger.LogWarning(msg);
    }

    internal static void LogErr(string msg)
    {
        Instance.Logger.LogError(msg);
    }
}

[HarmonyPatch]
public static class TestPatch
{
    private static bool _dumped;

    [HarmonyPatch(typeof(RoundDirector), "Update")]
    [HarmonyPostfix]
    private static void Update_PurePostfix()
    {
        if (_dumped) return;
        _dumped = true;

        Plugin.Log("*** RoundDirector.Update postfix FIRED - starting dump ***");

        try
        {
            // 1. Dump all Harmony patches on key methods
            Plugin.Log("=== HARMONY PATCH LIST ===");
            DumpPatches(typeof(RoundDirector), "Update");
            DumpPatches(typeof(RoundDirector), "StartRound");
            DumpPatches(typeof(RoundDirector), "ExtractionCompleted");
            DumpPatches(typeof(GameDirector), "Update");
            DumpPatches(typeof(LevelGenerator), "GenerateDone");
            DumpPatches(typeof(ShopManager), "ShopInitialize");
            DumpPatches(typeof(ValuableDirector), "SetupHost");
            DumpPatches(typeof(EnemyDirector), "AmountSetup");
            DumpPatches(typeof(SemiFunc), "OnSceneSwitch");
            DumpPatches(typeof(ItemAttributes), "GetValue");

            // 1b. Dump itemDictionary keys
            DumpItemDictionary();

            // 2. Assembly scan
            Plugin.Log("=== ASSEMBLY SCAN ===");
            var modNames = new[] {
                "LevelDisplay", "StageTimer", "Revive", "ShopExpander",
                "ValuableSpawnConfig", "EnemySpawnConfig", "UnlimitedPlayers", "LateJoin"
            };
            foreach (var modName in modNames)
                ScanAssembly(modName);

            // 3. Shop scene dump (runs on ShopInitialize)
            Plugin.Log("=== SHOP DUMP HOOK ===");
            try
            {
                var h = new Harmony("maxenterme.DiagDump.Shop");
                var target = typeof(ShopManager).GetMethod("ShopInitialize",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (target != null)
                {
                    h.Patch(target, postfix: new HarmonyMethod(typeof(TestPatch),
                        nameof(ShopInitialize_Dump)));
                    Plugin.Log("  Shop dump hook installed");
                }
            }
            catch (Exception e) { Plugin.LogErr($"Shop dump hook: {e}"); }

            // 4. Manual patch test
            Plugin.Log("=== MANUAL PATCH TEST ===");
            ManualPatchTest();

            Plugin.Log("=== DUMP COMPLETE ===");
        }
        catch (Exception e)
        {
            Plugin.LogErr($"DUMP CRASHED: {e}");
        }
    }

    private static bool _shopDumped;

    public static void ShopInitialize_Dump(ShopManager __instance)
    {
        // Only dump when real shop objects exist (skip menu/lobby)
        if (UnityEngine.Object.FindObjectOfType<ItemVolume>() == null)
        {
            Plugin.Log("ShopInitialize_Dump: skipping (no ItemVolume found)");
            return;
        }
        if (_shopDumped) return;
        _shopDumped = true;

        try
        {
            Plugin.Log("=== SHOP SCENE DUMP ===");
            Plugin.Log($"ShopManager pos: {__instance.transform.position}");

            // Dump ShopManager fields
            var smType = typeof(ShopManager);
            foreach (var f in smType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                try
                {
                    var val = f.GetValue(__instance);
                    if (val is System.Collections.IList list)
                        Plugin.Log($"  SM.{f.Name} ({f.FieldType.Name}): count={list.Count}");
                    else
                        Plugin.Log($"  SM.{f.Name} ({f.FieldType.Name}): {val}");
                }
                catch { }
            }

            // Dump shop-relevant GameObjects (shelves, counters, items, spawn points)
            Plugin.Log("=== SHOP OBJECTS ===");
            var keywords = new[] { "Shop", "shop", "Shelf", "shelf", "Counter", "counter",
                "Stand", "Item", "item", "Spawn", "spawn", "Module", "Cashier",
                "Soda", "Candy", "Medical", "Magazine", "valuable" };
            var allObjects = UnityEngine.Object.FindObjectsOfType<UnityEngine.GameObject>();
            foreach (var obj in allObjects)
            {
                if (!obj.activeInHierarchy) continue;
                bool match = false;
                foreach (var kw in keywords)
                    if (obj.name.Contains(kw)) { match = true; break; }
                if (!match) continue;
                DumpObject(obj, 0, 3); // dump with children up to depth 3
            }
            Plugin.Log("=== SHOP DUMP END ===");
        }
        catch (Exception e)
        {
            Plugin.LogErr($"ShopDump: {e}");
        }
    }

    private static void DumpObject(UnityEngine.GameObject obj, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        var indent = new string(' ', depth * 2);
        var pos = obj.transform.position;
        var components = obj.GetComponents<UnityEngine.Component>();
        var compNames = string.Join(",", components.Select(c => c?.GetType().Name ?? "null"));
        Plugin.Log($"{indent}[{obj.name}] pos=({pos.x:F1},{pos.y:F1},{pos.z:F1}) comp=[{compNames}]");

        for (int i = 0; i < obj.transform.childCount; i++)
        {
            var child = obj.transform.GetChild(i);
            if (child.gameObject.activeInHierarchy)
                DumpObject(child.gameObject, depth + 1, maxDepth);
        }
    }

    private static void DumpItemDictionary()
    {
        try
        {
            var stats = StatsManager.instance;
            if (stats == null || stats.itemDictionary == null)
            {
                Plugin.LogWarn("=== ITEM DICTIONARY: StatsManager/itemDictionary null ===");
                return;
            }

            Plugin.Log($"=== ITEM DICTIONARY ({stats.itemDictionary.Count} keys) ===");
            foreach (var key in stats.itemDictionary.Keys.OrderBy(k => k))
                Plugin.Log($"  ITEMKEY: '{key}'");
            Plugin.Log("=== ITEM DICTIONARY END ===");
        }
        catch (Exception e)
        {
            Plugin.LogErr($"DumpItemDictionary: {e}");
        }
    }

    private static void DumpPatches(Type type, string methodName)
    {
        try
        {
            var method = type.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                Plugin.LogWarn($"  {type.Name}.{methodName}: not found");
                return;
            }

            var info = Harmony.GetPatchInfo(method);
            if (info == null)
            {
                Plugin.Log($"  {type.Name}.{methodName}: NO patches");
                return;
            }

            int total = info.Prefixes.Count + info.Postfixes.Count + info.Transpilers.Count;
            Plugin.Log($"  {type.Name}.{methodName}: {total} patch(es)");
            foreach (var p in info.Prefixes)
                Plugin.Log($"    Pre: [{p.owner}] {p.PatchMethod.DeclaringType?.FullName}.{p.PatchMethod.Name}");
            foreach (var p in info.Postfixes)
                Plugin.Log($"    Post: [{p.owner}] {p.PatchMethod.DeclaringType?.FullName}.{p.PatchMethod.Name}");
        }
        catch (Exception e)
        {
            Plugin.LogErr($"  {type.Name}.{methodName}: {e.Message}");
        }
    }

    private static void ScanAssembly(string asmName)
    {
        try
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == asmName);
            if (asm == null)
            {
                Plugin.LogWarn($"  {asmName}: NOT LOADED");
                return;
            }

            Plugin.Log($"  {asmName}: {asm.Location}");
            var types = asm.GetTypes();
            int patchClasses = 0, patchMethods = 0;
            foreach (var type in types)
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                patchClasses++;
                foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    var attrs = m.GetCustomAttributes(typeof(HarmonyPatch), false);
                    if (attrs.Length == 0) continue;
                    patchMethods++;
                    string kind = m.GetCustomAttributes(typeof(HarmonyPostfix), false).Length > 0 ? "Post"
                        : m.GetCustomAttributes(typeof(HarmonyPrefix), false).Length > 0 ? "Pre" : "?";
                    Plugin.Log($"    {type.Name}.{m.Name} [{kind}]");
                    foreach (HarmonyPatch attr in attrs)
                        Plugin.Log($"      -> {attr.info.declaringType?.FullName}.{attr.info.methodName}");
                }
            }
            Plugin.Log($"  {asmName}: {patchClasses} classes, {patchMethods} methods");
        }
        catch (Exception e)
        {
            Plugin.LogErr($"  {asmName}: {e}");
        }
    }

    private static void ManualPatchTest()
    {
        try
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "LevelDisplay");
            if (asm == null) { Plugin.LogWarn("LevelDisplay asm not found"); return; }

            var patchType = asm.GetType("LevelDisplay.LevelDisplayHUD");
            if (patchType == null) { Plugin.LogWarn("LevelDisplayHUD type not found"); return; }

            Plugin.Log($"Found: {patchType.FullName}");
            var target = typeof(RoundDirector).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var postfix = patchType.GetMethod("RoundDirector_Update_Postfix",
                BindingFlags.Static | BindingFlags.NonPublic);

            Plugin.Log($"  target={target != null}, postfix={postfix != null}");
            if (target != null && postfix != null)
            {
                var h = new Harmony("maxenterme.DiagDump.Manual");
                h.Patch(target, postfix: new HarmonyMethod(postfix));
                Plugin.Log("  Manual patch applied OK!");
            }
        }
        catch (Exception e)
        {
            Plugin.LogErr($"ManualPatch: {e}");
        }
    }
}
