using System.Collections.Generic;
using UnityEngine;

/// <summary>Ağaç: sallanma döngüsü; piyon kesince kütüğe döner, süre sonunda yeniden büyür.</summary>
public sealed class IslandTree : MonoBehaviour
{
    public static readonly List<IslandTree> All = new List<IslandTree>();

    public Sprite[] idle;
    public Sprite stump;
    private IslandLoopAnimator anim;
    private SpriteRenderer sr;
    private float regrowAt;

    public bool Chopped { get; private set; }
    /// <summary>Kalıcı olarak kesildi (AŞAMA 2 §3): görünmez, yeniden büyümez.</summary>
    public bool Cleared { get; private set; }
    /// <summary>Ayakların dünya konumu (nesnenin kendisi).</summary>
    public Vector2 Feet => transform.position;
    /// <summary>Hücre (sütun, satır): fırın adı "Tree{v}_{sütun}_{satır}"; okunamazsa konumdan.</summary>
    public Vector2Int Cell { get; private set; }
    public SpriteRenderer Renderer => sr;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<IslandLoopAnimator>();
        Cell = ParseCell(gameObject.name, transform.position);
    }

    private static Vector2Int ParseCell(string name, Vector3 position)
    {
        var parts = name.Split('_');
        if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 2], out int c) && int.TryParse(parts[parts.Length - 1], out int r))
            return new Vector2Int(c, r);
        return new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(IslandLayout.Height - position.y));
    }

    public static IslandTree At(Vector2Int cell)
    {
        foreach (var tree in All) if (tree != null && tree.Cell == cell) return tree;
        return null;
    }

    /// <summary>Kalıcı kesim: kütüğe döner ve solar (animate) ya da hemen kapanır.</summary>
    public void Clear(bool animate)
    {
        if (Cleared) return;
        Cleared = true;
        Chopped = true;
        if (anim != null) anim.enabled = false;
        if (!animate || sr == null) { gameObject.SetActive(false); return; }
        if (stump != null) sr.sprite = stump;
        StartCoroutine(FadeOut());
    }

    private System.Collections.IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(0.5f);
        var color = sr.color;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            sr.color = new Color(color.r, color.g, color.b, color.a * (1f - t / 0.8f));
            yield return null;
        }
        gameObject.SetActive(false);
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    public void Chop()
    {
        if (Chopped || Cleared || stump == null) return;
        Chopped = true;
        regrowAt = Time.time + Progression.Island.TreeRegrowSeconds;
        if (anim != null) anim.enabled = false;
        if (sr != null) sr.sprite = stump;
    }

    private void Update()
    {
        if (Cleared || !Chopped || Time.time < regrowAt) return;
        Chopped = false;
        if (anim != null) anim.enabled = true;
    }
}
