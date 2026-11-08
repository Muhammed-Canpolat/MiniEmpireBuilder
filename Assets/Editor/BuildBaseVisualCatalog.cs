using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public static class BuildBaseVisualCatalog
{
    private const string Root = "Assets/Tiny Swords";
    private const string Output = "Assets/Resources/BaseVisualCatalog.asset";
    private const string Generated = "Assets/Resources/BaseVisualAnimations";
    private static readonly string[] Colors = { "Blue", "Yellow", "Red", "Purple", "Black" };
    private static readonly string[] States = { "Idle", "Run", "Attack", "Hit", "Death" };

    [MenuItem("Tools/Base Review/Build Visual Assets")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Generated))
            AssetDatabase.CreateFolder("Assets/Resources", "BaseVisualAnimations");
        var catalog = AssetDatabase.LoadAssetAtPath<BaseVisualCatalog>(Output);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BaseVisualCatalog>();
            AssetDatabase.CreateAsset(catalog, Output);
        }
        var baseController = MakeBaseController();
        var buildingTypes = new[] { BuildingType.MainBase, BuildingType.ArcherTower,
            BuildingType.CannonTower, BuildingType.Monastery, BuildingType.GoldMine };
        var buildingFiles = new[] { "Castle", "Archery", "Tower", "Monastery", "" };
        catalog.buildings = new BaseVisualCatalog.BuildingSet[buildingTypes.Length];
        var atlasItems = new List<UnityEngine.Object>();
        for (int type = 0; type < buildingTypes.Length; type++)
        {
            var set = new BaseVisualCatalog.BuildingSet { type = buildingTypes[type] };
            for (int level = 0; level < 5; level++)
            {
                Sprite sprite = buildingTypes[type] == BuildingType.GoldMine ? null : FirstSprite(
                    $"{Root}/Buildings/{Colors[level]} Buildings/{buildingFiles[type]}.png");
                var clip = MakeStillClip($"{buildingTypes[type]}_{Colors[level]}", sprite);
                string overridePath = $"{Generated}/{buildingTypes[type]}_{Colors[level]}.overrideController";
                var replacement = new AnimatorOverrideController(baseController);
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                replacement.GetOverrides(overrides);
                for (int i = 0; i < overrides.Count; i++)
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
                replacement.ApplyOverrides(overrides);
                AssetDatabase.DeleteAsset(overridePath);
                AssetDatabase.CreateAsset(replacement, overridePath);
                set.levels[level] = new BuildingLevelVisual
                {
                    color = LevelColor(level), sprite = sprite, animatorOverride = replacement
                };
                if (sprite != null) atlasItems.Add(sprite);
            }
            catalog.buildings[type] = set;
        }
        catalog.archerControllers = UnitControllers("Units", "Archer", "Archer");
        catalog.warriorControllers = UnitControllers("Units", "Warrior", "Warrior");
        catalog.monkControllers = UnitControllers("Units", "Monk", "Monk");
        catalog.pawnControllers = new RuntimeAnimatorController[5];
        for (int i = 0; i < 5; i++)
            catalog.pawnControllers[i] = FindController(
                $"{Root}/Pawn and Resources/Pawn/{Colors[i]} Pawn");
        catalog.heroControllers = new RuntimeAnimatorController[]
        {
            MakeHeroController("axe", "hero_axe_attack1"),
            MakeHeroController("spear", "hero_spear_attack"),
            MakeHeroController("bow", "hero_bow_shoot")
        };
        catalog.dustController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            $"{Root}/Particle FX/Dust 1 Animation/Dust 1.controller");
        catalog.goldStones = Enumerable.Range(1, 6).Select(i => FirstSprite(
            $"{Root}/Pawn and Resources/Gold/Gold Stones/Gold Stone {i}.png")).ToArray();
        catalog.goldIcon = FirstSprite(
            $"{Root}/Pawn and Resources/Gold/Gold Resource/Gold_Resource.png");
        catalog.cloudShadows = Enumerable.Range(1, 3).Select(i => FirstSprite(
            $"{Root}/Terrain/Decorations/Clouds/Clouds_{i:00}.png")).ToArray();
        catalog.bushes = Enumerable.Range(1, 4).Select(i => FirstSprite(
            $"{Root}/Terrain/Decorations/Bushes/Bush {i}.png")).ToArray();
        catalog.bushControllers = Enumerable.Range(1, 4).Select(i =>
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                $"{Root}/Terrain/Decorations/Bushes/Bush {i} Animation/Bush {i}.controller"))
            .ToArray();
        const string garrisonPath = "Assets/Resources/BaseGarrisonAnchor.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(garrisonPath) == null)
        {
            var anchor = new GameObject("GarrisonAnchor", typeof(SpriteRenderer), typeof(Animator));
            anchor.GetComponent<Animator>().cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            PrefabUtility.SaveAsPrefabAsset(anchor, garrisonPath);
            UnityEngine.Object.DestroyImmediate(anchor);
        }
        catalog.garrisonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(garrisonPath);
        atlasItems.AddRange(catalog.goldStones.Where(s => s != null));
        atlasItems.AddRange(catalog.bushes.Where(s => s != null));
        foreach (string color in Colors)
        {
            AddSpritesFromFolder(atlasItems, $"{Root}/Units/{color} Units/Archer");
            AddSpritesFromFolder(atlasItems, $"{Root}/Units/{color} Units/Warrior");
            AddSpritesFromFolder(atlasItems, $"{Root}/Units/{color} Units/Monk");
            AddSpritesFromFolder(atlasItems, $"{Root}/Pawn and Resources/Pawn/{color} Pawn");
        }
        AddSpritesFromFolder(atlasItems, $"{Root}/Particle FX", "Dust_");
        AddSpritesFromFolder(atlasItems, $"{Root}/Terrain/Decorations/Clouds");
        MakeAtlas(atlasItems);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BaseVisual] Visual catalog and atlas built.");
    }

    private static RuntimeAnimatorController[] UnitControllers(string folder, string subfolder, string prefix)
    {
        var result = new RuntimeAnimatorController[5];
        for (int i = 0; i < 5; i++)
            result[i] = FindController($"{Root}/{folder}/{Colors[i]} Units/{subfolder}");
        return result;
    }

    private static RuntimeAnimatorController FindController(string folder)
    {
        string guid = AssetDatabase.FindAssets("t:AnimatorController", new[] { folder })
            .FirstOrDefault();
        return guid == null ? null : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            AssetDatabase.GUIDToAssetPath(guid));
    }

    private static Sprite FirstSprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<Sprite>().FirstOrDefault();

    private static AnimatorController MakeHeroController(string weapon, string attackFile)
    {
        string path = $"{Generated}/Hero_{weapon}.controller";
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        string[] files = { $"hero_{weapon}_idle", $"hero_{weapon}_run", attackFile,
            $"hero_{weapon}_idle", $"hero_{weapon}_idle" };
        for (int i = 0; i < States.Length; i++)
        {
            var frames = AssetDatabase.LoadAllAssetsAtPath(
                $"Assets/Resources/Sprites/{files[i]}.png").OfType<Sprite>()
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
            var clip = MakeSequenceClip($"Hero_{weapon}_{States[i]}", frames,
                i == 2 ? 10f : i == 1 ? 8f : 5f, i < 2);
            controller.AddMotion(clip);
            controller.layers[0].stateMachine.states.Last().state.name = States[i];
        }
        var machine = controller.layers[0].stateMachine;
        var states = machine.states.ToDictionary(item => item.state.name, item => item.state);
        var run = states["Idle"].AddTransition(states["Run"]);
        run.hasExitTime = false;
        run.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        var idle = states["Run"].AddTransition(states["Idle"]);
        idle.hasExitTime = false;
        idle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
        var attack = machine.AddAnyStateTransition(states["Attack"]);
        attack.hasExitTime = false;
        attack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        foreach (string transient in new[] { "Attack", "Hit" })
        {
            var end = states[transient].AddTransition(states["Idle"]);
            end.hasExitTime = true;
            end.exitTime = 1f;
        }
        return controller;
    }

    private static AnimationClip MakeSequenceClip(string name, Sprite[] frames,
        float fps, bool loop)
    {
        string path = $"{Generated}/{name}.anim";
        AssetDatabase.DeleteAsset(path);
        var clip = new AnimationClip { name = name, frameRate = fps };
        if (frames.Length > 0)
        {
            var binding = new EditorCurveBinding { path = "", type = typeof(SpriteRenderer),
                propertyName = "m_Sprite" };
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        }
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static Color LevelColor(int index) => index switch
    {
        0 => new Color(0.43f, 0.69f, 1f),
        1 => new Color(1f, 0.84f, 0.37f),
        2 => new Color(1f, 0.42f, 0.4f),
        3 => new Color(0.74f, 0.53f, 1f),
        _ => new Color(0.34f, 0.35f, 0.42f)
    };

    private static AnimatorController MakeBaseController()
    {
        string path = $"{Generated}/BuildingStates.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller != null) return controller;
        controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (string state in States)
        {
            var clip = MakeStillClip("Template_" + state, null);
            controller.AddMotion(clip);
            var layer = controller.layers[0];
            var animatorState = layer.stateMachine.states.Last().state;
            animatorState.name = state;
        }
        return controller;
    }

    private static AnimationClip MakeStillClip(string name, Sprite sprite)
    {
        string path = $"{Generated}/{name}.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        var clip = existing != null ? existing : new AnimationClip { name = name, frameRate = 8f };
        var binding = new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, sprite == null ? null :
            new[] { new ObjectReferenceKeyframe { time = 0f, value = sprite } });
        if (existing == null) AssetDatabase.CreateAsset(clip, path);
        else EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void MakeAtlas(List<UnityEngine.Object> objects)
    {
        const string path = "Assets/Resources/BaseVisuals.spriteatlas";
        AssetDatabase.DeleteAsset(path);
        var atlas = new SpriteAtlas();
        AssetDatabase.CreateAsset(atlas, path);
        atlas.Add(objects.Distinct().ToArray());
        var packing = atlas.GetPackingSettings();
        packing.enableRotation = false;
        packing.enableTightPacking = false;
        atlas.SetPackingSettings(packing);
    }

    private static void AddSpritesFromFolder(List<UnityEngine.Object> items,
        string folder, string prefix = null)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileName(path);
            if (prefix != null && !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            items.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>());
        }
    }
}
