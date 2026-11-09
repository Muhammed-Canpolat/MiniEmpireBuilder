using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bir inşaatçının kulübesi. Harita nesnesi olarak kayıtlıdır: basılı tutup taşınabilir,
/// yerleşim kontrolünde 2×2 alan kaplar. Konumu PlayerData.builderHuts[Index]'te saklanır.
/// </summary>
public sealed class BuilderHut : MonoBehaviour
{
    public static readonly List<BuilderHut> All = new List<BuilderHut>();
    public static readonly Vector2Int FootprintSize = new Vector2Int(2, 2);

    public int Index { get; private set; }
    private SpriteRenderer body;
    private Transform shadow;

    public Rect Footprint => new Rect((Vector2)transform.position - (Vector2)FootprintSize * 0.5f, FootprintSize);

    public void Initialize(int index, SpriteRenderer bodyRenderer, Transform shadowTransform)
    {
        Index = index;
        body = bodyRenderer;
        shadow = shadowTransform;
        RefreshSorting();
    }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable() => All.Remove(this);

    /// <summary>Taşındıktan sonra derinlik sırası ve kayıt güncellenir.</summary>
    public void OnMoved()
    {
        RefreshSorting();
        SavePosition();
    }

    private void RefreshSorting()
    {
        if (body == null) return;
        body.sortingOrder = 100 - Mathf.RoundToInt(transform.position.y * 2f);
        if (shadow != null)
            foreach (var renderer in shadow.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.sortingOrder = body.sortingOrder - 1;
    }

    public void SavePosition()
    {
        var player = GameManager.Instance?.PlayerData;
        if (player == null) return;
        if (player.builderHuts == null) player.builderHuts = new List<BuilderHutSave>();
        while (player.builderHuts.Count <= Index) player.builderHuts.Add(new BuilderHutSave());
        var save = player.builderHuts[Index];
        save.hasPosition = true;
        save.x = transform.position.x;
        save.y = transform.position.y;
    }

    public static bool IsInsideAny(Vector2 point)
    {
        foreach (var hut in All)
            if (hut != null && hut.Footprint.Contains(point)) return true;
        return false;
    }
}
