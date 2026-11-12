using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ada doğa nesnelerinin çalışma anı bileşenleri. Tiny Swords klipleri tek seferlik oynar (m_LoopTime 0, kit §9):
/// bu yüzden kare dizileri (<see cref="Sprite"/>[]) 10 fps ile döngüye alınır. Hepsi editör pişiricisinin
/// ürettiği <c>Resources/Island/IslandNature.prefab</c> içinde serileştirilir (kodla çizilmiş görsel yok).
/// </summary>
public sealed class IslandLoopAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 10f;
    public float phase;
    private SpriteRenderer sr;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    private void OnEnable()
    {
        if (phase <= 0f) phase = Random.value * 4f;
    }

    private void LateUpdate()
    {
        if (sr == null || frames == null || frames.Length == 0) return;
        sr.sprite = frames[(int)((Time.time + phase) * fps) % frames.Length];
    }
}
