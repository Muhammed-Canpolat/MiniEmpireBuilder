using UnityEngine;

/// <summary>
/// Kamerayı izleyen hafif ortam partikülleri (ParticleSystem yerine elle; 36 sprite):
/// Bölge 4 kar (aşağı süzülür), Bölge 5 kül (yavaş düşer), Bölge 6 kıvılcım (yükselir, titrer).
/// Oynanışa etkisi yok; küçük ve yarı saydam olduğu için düşman/okunurluğu kapatmaz.
/// </summary>
public sealed class RegionAmbientParticles : MonoBehaviour
{
    private const int Count = 36;
    private Transform[] flakes;
    private Vector2[] velocity;
    private float[] phase;
    private int regionId;

    public void Setup(int id)
    {
        regionId = id;
        var sprite = BaseMapManager.MakeCircleSprite(Color.white);
        flakes = new Transform[Count];
        velocity = new Vector2[Count];
        phase = new float[Count];
        var root = new GameObject("RegionAmbient").transform;
        root.SetParent(transform, false);
        for (int i = 0; i < Count; i++)
        {
            var go = new GameObject("Ambient");
            go.transform.SetParent(root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 450;
            float size = id == 4 ? Random.Range(0.06f, 0.14f) : id == 5 ? Random.Range(0.05f, 0.1f) : Random.Range(0.05f, 0.09f);
            go.transform.localScale = Vector3.one * size;
            renderer.color = id == 4 ? new Color(1f, 1f, 1f, Random.Range(0.45f, 0.8f))
                : id == 5 ? new Color(0.35f, 0.32f, 0.33f, Random.Range(0.35f, 0.6f))
                : new Color(1f, Random.Range(0.45f, 0.75f), 0.15f, Random.Range(0.55f, 0.9f));
            velocity[i] = id == 4 ? new Vector2(Random.Range(-0.5f, -0.15f), Random.Range(-1.4f, -0.8f))
                : id == 5 ? new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(-0.6f, -0.3f))
                : new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.6f, 1.2f));
            phase[i] = Random.Range(0f, 6.28f);
            flakes[i] = go.transform;
        }
        Scatter(true);
    }

    private Rect View()
    {
        var cam = Camera.main;
        if (cam == null) return new Rect(-6f, -10f, 12f, 20f);
        float h = cam.orthographicSize * 2f, w = h * cam.aspect;
        Vector3 c = cam.transform.position;
        return new Rect(c.x - w / 2f, c.y - h / 2f, w, h);
    }

    private void Scatter(bool all)
    {
        var view = View();
        for (int i = 0; i < Count; i++)
            if (all) flakes[i].position = new Vector3(Random.Range(view.xMin, view.xMax), Random.Range(view.yMin, view.yMax), 0f);
    }

    private void Update()
    {
        if (flakes == null) return;
        var view = View();
        float t = Time.time, dt = Time.deltaTime;
        for (int i = 0; i < Count; i++)
        {
            var p = flakes[i].position;
            float sway = Mathf.Sin(t * 1.3f + phase[i]) * (regionId == 6 ? 0.35f : 0.25f);
            p.x += (velocity[i].x + sway) * dt;
            p.y += velocity[i].y * dt;
            // Görüş dışına çıkan partikül karşı kenardan geri girer.
            if (p.y < view.yMin - 0.5f) p.y = view.yMax + 0.4f;
            if (p.y > view.yMax + 0.5f) p.y = view.yMin - 0.4f;
            if (p.x < view.xMin - 0.5f) p.x = view.xMax + 0.4f;
            if (p.x > view.xMax + 0.5f) p.x = view.xMin - 0.4f;
            flakes[i].position = p;
        }
    }
}
