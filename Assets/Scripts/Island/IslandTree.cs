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
    /// <summary>Ayakların dünya konumu (nesnenin kendisi).</summary>
    public Vector2 Feet => transform.position;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<IslandLoopAnimator>();
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    public void Chop()
    {
        if (Chopped || stump == null) return;
        Chopped = true;
        regrowAt = Time.time + Progression.Island.TreeRegrowSeconds;
        if (anim != null) anim.enabled = false;
        if (sr != null) sr.sprite = stump;
    }

    private void Update()
    {
        if (!Chopped || Time.time < regrowAt) return;
        Chopped = false;
        if (anim != null) anim.enabled = true;
    }
}
