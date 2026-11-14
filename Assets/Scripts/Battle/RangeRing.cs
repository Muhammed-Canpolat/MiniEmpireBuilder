using UnityEngine;

/// <summary>
/// 6. oturum (1.2): menzil çemberi. Hedefleme ile AYNI merkez (<see cref="BattleStructure.RangeCenter"/>) ve AYNI yarıçapı kullanır.
/// Kök neden: halka ölçeği tweenlenen yapının çocuğuydu; açılış animasyonunda (Y ölçeği 0,2 → 1) yerel konumu 5 kat büyüyüp merkez
/// kulenin çok altına kayıyordu ve merkez kulenin tabanıydı. Halka artık hiçbir ölçekli nesnenin çocuğu değil; her karede dünya
/// konumuna yazılır, yarıçap dünya birimidir (her yönde eşit).
/// </summary>
public sealed class RangeRing : MonoBehaviour
{
    private GameObject ring;
    private System.Func<Vector2> center;
    private float radius;

    public GameObject Ring => ring;
    public float Radius => radius;
    public Vector2 Center => center != null ? center() : (Vector2)transform.position;

    public static RangeRing Attach(GameObject owner, System.Func<Vector2> center, float radius, Color color, int dashes = 36)
    {
        var rr = owner.AddComponent<RangeRing>();
        rr.center = center;
        rr.radius = radius;
        // Ölçeği 1 olan, ebeveynsiz kök: hiçbir tween halkayı bozamaz.
        var holder = new GameObject("RangeRingRoot_" + owner.name);
        rr.ring = BattleWorldMarkers.DashedCircle(holder.transform, radius, color, dashes);
        rr.ring.transform.SetParent(null, true);
        rr.ring.transform.localScale = Vector3.one;
        Destroy(holder);
        rr.Follow();
        return rr;
    }

    public void SetVisible(bool show)
    {
        if (ring != null && ring.activeSelf != show) ring.SetActive(show);
    }

    private void Follow()
    {
        if (ring == null) return;
        Vector2 c = Center;
        ring.transform.position = new Vector3(c.x, c.y, 0.05f);
    }

    private void LateUpdate() => Follow();

    private void OnDisable() { if (ring != null) ring.SetActive(false); }

    private void OnDestroy()
    {
        if (ring != null) Destroy(ring);
    }
}
