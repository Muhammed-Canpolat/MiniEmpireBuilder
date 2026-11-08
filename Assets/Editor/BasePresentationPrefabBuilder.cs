using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Creates editable presentation prefabs used by the runtime map builder.</summary>
public static class BasePresentationPrefabBuilder
{
    [MenuItem("Mini Empire/Build presentation prefabs")]
    public static void Build()
    {
        var mine = new GameObject("BaseMineVisual");
        mine.AddComponent<BaseMineVisual>();
        PrefabUtility.SaveAsPrefabAsset(mine, "Assets/Resources/BaseMineVisual.prefab");
        Object.DestroyImmediate(mine);

        var method = typeof(BaseGarrisonVisual).GetMethod("Defaults",
            BindingFlags.NonPublic | BindingFlags.Static);
        var field = typeof(BaseGarrisonVisual).GetField("anchors",
            BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var type in new[] { BuildingType.MainBase, BuildingType.ArcherTower,
            BuildingType.CannonTower, BuildingType.Monastery })
        {
            var root = new GameObject("Garrison_" + type);
            var component = root.AddComponent<BaseGarrisonVisual>();
            field.SetValue(component, method.Invoke(null, new object[] { type }));
            PrefabUtility.SaveAsPrefabAsset(root,
                "Assets/Resources/Garrison_" + type + ".prefab");
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
