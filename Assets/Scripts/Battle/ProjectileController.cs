using UnityEngine;
using DG.Tweening;
using System;

public enum ProjectileType
{
    Arrow,
    Axe,
    Spear
}

/// <summary>
/// Mermi, Ok ve Balta fırlatma hareketlerini ve görsel efektlerini yönetir.
/// </summary>
public class ProjectileController : MonoBehaviour
{
    private Action onHitCallback;
    private ProjectileType type;

    public void Launch(Vector3 start, Vector3 target, ProjectileType pType, Action onHit)
    {
        transform.position = start;
        onHitCallback = onHit;
        type = pType;

        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 20;

        Vector2 dir = (target - start).normalized;
        float dist = Vector2.Distance(start, target);
        
        switch (type)
        {
            case ProjectileType.Arrow:
                SetupArrow(sr, start, target, dir, dist);
                break;
            case ProjectileType.Axe:
                SetupAxe(sr, start, target, dir, dist);
                break;
            case ProjectileType.Spear:
                SetupSpear(sr, start, target, dir, dist);
                break;
        }
    }

    private void SetupArrow(SpriteRenderer sr, Vector3 start, Vector3 target, Vector2 dir, float dist)
    {
        // Ok sprite'ı veya placeholder dikdörtgen
        sr.sprite = CreateRectSprite();
        sr.color = new Color(0.8f, 0.7f, 0.3f);
        transform.localScale = new Vector3(0.08f, 0.35f, 1f);

        // Rotasyon hedefe doğru
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // Hareket süresi menzile göre değişebilir, ortalama 0.25s
        float duration = Mathf.Clamp(dist * 0.05f, 0.15f, 0.4f);

        // Ok için TrailRenderer ekle
        TrailRenderer tr = gameObject.AddComponent<TrailRenderer>();
        tr.time = 0.15f;
        tr.startWidth = 0.05f;
        tr.endWidth = 0f;
        tr.material = new Material(Shader.Find("Sprites/Default"));
        tr.startColor = new Color(1f, 1f, 1f, 0.5f);
        tr.endColor = new Color(1f, 1f, 1f, 0f);

        transform.DOMove(target, duration).SetEase(Ease.Linear).OnComplete(OnHitTarget);
    }

    private void SetupAxe(SpriteRenderer sr, Vector3 start, Vector3 target, Vector2 dir, float dist)
    {
        // Balta sprite'ı
        if (SpriteManager.Instance != null && SpriteManager.Instance.HeroAxe != null)
        {
            sr.sprite = SpriteManager.Instance.HeroAxe;
            transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        }
        else
        {
            sr.sprite = CreateRectSprite();
            sr.color = new Color(0.7f, 0.7f, 0.7f);
            transform.localScale = new Vector3(0.3f, 0.3f, 1f);
        }

        float duration = Mathf.Clamp(dist * 0.06f, 0.2f, 0.5f);
        float jumpPower = Mathf.Clamp(dist * 0.2f, 0.5f, 2f);

        // Zıplayarak kavisli hareket
        transform.DOJump(target, jumpPower, 1, duration).SetEase(Ease.Linear).OnComplete(OnHitTarget);
        
        // Kendi ekseninde dönme
        transform.DORotate(new Vector3(0, 0, -360f), 0.2f, RotateMode.FastBeyond360)
            .SetLoops(-1, LoopType.Incremental).SetEase(Ease.Linear);
    }

    private void SetupSpear(SpriteRenderer sr, Vector3 start, Vector3 target, Vector2 dir, float dist)
    {
        if (SpriteManager.Instance != null && SpriteManager.Instance.HeroSpear != null)
        {
            sr.sprite = SpriteManager.Instance.HeroSpear;
            transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        }
        else
        {
            sr.sprite = CreateRectSprite();
            sr.color = new Color(0.6f, 0.8f, 0.9f); // Açık mavi / gümüş
            transform.localScale = new Vector3(0.08f, 0.5f, 1f);
        }

        // Rotasyon hedefe doğru
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // Mızrak oktan daha hızlıdır
        float duration = Mathf.Clamp(dist * 0.035f, 0.1f, 0.3f);

        transform.DOMove(target, duration).SetEase(Ease.OutSine).OnComplete(OnHitTarget);
    }

    private void OnHitTarget()
    {
        // Hedefe ulaşıldı
        transform.DOKill(); // Dönme animasyonlarını vb. durdur
        
        onHitCallback?.Invoke();
        
        // Mermi tipine göre çarpma efekti
        if (BattleVfxManager.Instance != null)
        {
            if (type == ProjectileType.Spear || type == ProjectileType.Arrow)
            {
                BattleVfxManager.Instance.SpawnSmallDust(transform.position);
            }
            else if (type == ProjectileType.Axe)
            {
                BattleVfxManager.Instance.SpawnSpark(transform.position);
            }
        }

        Destroy(gameObject);
    }

    private Sprite CreateRectSprite()
    {
        Texture2D tex = new Texture2D(8, 32);
        Color[] px = new Color[8 * 32];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 8, 32), new Vector2(0.5f, 0.5f), 32f);
    }
}
