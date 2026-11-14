using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 7. oturum (D88): seviye yerleşimi seçiliyken Scene görünümünde düzenleme. Kara hücreleri (yeşil), su (boş), rotalar (kahve çizgi,
/// kontrol noktaları sürüklenir), yuvalar (sarı daire + Topçu menzil çemberi, sürüklenir), Ana Üs / maden / kahraman doğuşu.
/// Inspector: Doğrula, yuvaları yeniden üret, maden/kahraman yeniden yerleştir, rotayı şablona sıfırla.
/// Kaydırma: Scene görünümünü 2D'ye alıp (0, 12) çevresine odaklanın ("Çerçevele" düğmesi).
/// </summary>
[CustomEditor(typeof(BattleLevelLayout))]
public sealed class BattleLevelLayoutEditor : Editor
{
    private List<string> lastErrors;

    private void OnEnable() => SceneView.duringSceneGui += OnScene;
    private void OnDisable() => SceneView.duringSceneGui -= OnScene;

    private BattleRegionMap Map(BattleLevelLayout layout) =>
        AssetDatabase.LoadAssetAtPath<BattleRegionMap>($"Assets/Resources/BattleMaps/Region_{layout.regionId}.asset");

    public override void OnInspectorGUI()
    {
        var layout = (BattleLevelLayout)target;
        EditorGUILayout.HelpBox("Scene görünümünde düzenlenir: kahve noktalar rota kontrol noktaları, sarı daireler kule yuvaları " +
                                "(çember = Topçu menzili), kırmızı kare Ana Üs, altın maden, mavi kahraman.", MessageType.Info);
        if (GUILayout.Button("Scene'de çerçevele"))
        {
            var view = SceneView.lastActiveSceneView;
            if (view != null) { view.in2DMode = true; view.Frame(new Bounds(new Vector3(0f, 11f, 0f), new Vector3(17f, 34f, 1f)), false); }
        }
        var map = Map(layout);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Doğrula")) lastErrors = BattleLayoutGenerator.Validate(map, layout);
            if (GUILayout.Button("Yuvaları yeniden üret") && map != null)
            {
                Undo.RecordObject(layout, "Yuvaları üret");
                layout.slots = new Vector2[0];
                layout.slots = BattleLayoutGenerator.GenerateSlots(map, layout, BattleLayoutGenerator.SlotCountFor(layout.regionId, layout.levelNumber, layout.Routes.Length));
                EditorUtility.SetDirty(layout);
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Madeni/kahramanı yerleştir") && map != null)
            {
                Undo.RecordObject(layout, "Yerleştir");
                layout.heroSpawn = BattleLayoutGenerator.GenerateHeroSpawn(map, layout);
                if (layout.HasMine) layout.minePosition = BattleLayoutGenerator.GenerateMine(map, layout);
                EditorUtility.SetDirty(layout);
            }
            if (GUILayout.Button("Rotayı şablona sıfırla") && map != null)
            {
                Undo.RecordObject(layout, "Rota sıfırla");
                BattleMapAuthoring.FillLevel(map, layout, layout.regionId, layout.levelNumber,
                    BattleLayoutGenerator.RouteTemplate(layout.regionId, layout.levelNumber));
                EditorUtility.SetDirty(layout);
            }
        }
        if (lastErrors != null)
        {
            if (lastErrors.Count == 0) EditorGUILayout.HelpBox("Geçerli ✓", MessageType.None);
            foreach (var e in lastErrors) EditorGUILayout.HelpBox(e, MessageType.Error);
        }
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }

    private void OnScene(SceneView view)
    {
        var layout = target as BattleLevelLayout;
        if (layout == null) return;
        var map = Map(layout);
        if (map != null && map.mask != null)
        {
            var fill = new Color(0.35f, 0.6f, 0.3f, 0.18f);
            for (int r = 0; r < map.mask.Length; r++)
                for (int c = 0; c < map.columns; c++)
                    if (map.IsLandCell(c, r))
                    {
                        var center = BattleMapGeometry.CellCenter(c, r);
                        Handles.DrawSolidRectangleWithOutline(new Rect(center.x - 0.5f, center.y - 0.5f, 1f, 1f), fill, Color.clear);
                    }
        }
        // Rotalar
        for (int ri = 0; ri < layout.routes.Count; ri++)
        {
            var data = layout.routes[ri];
            if (data?.points == null || data.points.Length < 2) continue;
            var route = new BattleRoute(data.points);
            var line = new Vector3[route.Points.Length];
            for (int i = 0; i < line.Length; i++) line[i] = route.Points[i];
            Handles.color = new Color(0.47f, 0.34f, 0.2f, 1f);
            Handles.DrawAAPolyLine(8f, line);
            Handles.color = new Color(0.85f, 0.7f, 0.45f, 1f);
            Handles.DrawAAPolyLine(4f, line);
            foreach (float s in route.Bends()) Handles.DrawWireDisc(route.PointAt(s), Vector3.forward, 0.3f);
            for (int i = 0; i < data.points.Length; i++)
            {
                EditorGUI.BeginChangeCheck();
                Handles.color = i == 0 ? Color.red : new Color(0.55f, 0.35f, 0.15f);
                var moved = Handles.FreeMoveHandle(data.points[i], 0.35f, Vector3.zero, Handles.CircleHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(layout, "Rota noktası");
                    data.points[i] = new Vector2(Mathf.Round(moved.x * 10f) / 10f, Mathf.Round(moved.y * 10f) / 10f);
                    EditorUtility.SetDirty(layout);
                }
            }
        }
        // Yuvalar
        if (layout.slots != null)
            for (int i = 0; i < layout.slots.Length; i++)
            {
                var p = layout.slots[i];
                Handles.color = new Color(1f, 0.84f, 0.2f, 0.9f);
                Handles.DrawWireDisc(p, Vector3.forward, BattleMapGeometry.SlotRadius);
                Handles.color = new Color(1f, 0.84f, 0.2f, 0.35f);
                Handles.DrawWireDisc(p + new Vector2(0f, BattleLayoutGenerator.RangeCenterLift), Vector3.forward, BattleLayoutGenerator.CoverageRange);
                Handles.Label(p + new Vector2(-0.2f, 0.2f), (i + 1).ToString());
                EditorGUI.BeginChangeCheck();
                Handles.color = new Color(1f, 0.84f, 0.2f, 1f);
                var moved = Handles.FreeMoveHandle(p, 0.5f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(layout, "Yuva");
                    layout.slots[i] = new Vector2(Mathf.Round(moved.x * 10f) / 10f, Mathf.Round(moved.y * 10f) / 10f);
                    EditorUtility.SetDirty(layout);
                }
            }
        PointHandle(layout, ref layout.basePosition, new Color(0.9f, 0.25f, 0.2f), "Ana Üs", 0.8f);
        PointHandle(layout, ref layout.heroSpawn, new Color(0.3f, 0.6f, 1f), "Kahraman", 0.4f);
        if (layout.HasMine) PointHandle(layout, ref layout.minePosition, new Color(1f, 0.75f, 0.1f), "Maden", 0.7f);
    }

    private static void PointHandle(BattleLevelLayout layout, ref Vector2 point, Color color, string label, float size)
    {
        Handles.color = color;
        Handles.Label(point + new Vector2(size, size), label);
        EditorGUI.BeginChangeCheck();
        var moved = Handles.FreeMoveHandle(point, size, Vector3.zero, Handles.RectangleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(layout, label);
            point = new Vector2(Mathf.Round(moved.x * 10f) / 10f, Mathf.Round(moved.y * 10f) / 10f);
            EditorUtility.SetDirty(layout);
        }
    }
}
