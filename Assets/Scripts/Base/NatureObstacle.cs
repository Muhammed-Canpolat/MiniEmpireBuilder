using System.Collections.Generic;
using UnityEngine;

public enum NatureKind { Tree, Bush, Rock, Stump }

/// <summary>
/// Kilitli araziden gelen doğa nesnesi (ağaç, çalı, kaya, kütük). Arazi genişleyince kalır; yerleştirmeyi
/// engeller (hücre kaydı <see cref="ByCell"/>) ve oyuncu altın karşılığı kaldırabilir.
/// Kimlik: tür + hücre (kayıt: PlayerSaveData.removedNatureIds).
/// </summary>
public sealed class NatureObstacle : MonoBehaviour
{
    public static readonly Dictionary<Vector2Int, NatureObstacle> ByCell = new Dictionary<Vector2Int, NatureObstacle>();

    public NatureKind kind;
    [SerializeField] private Vector2Int _cell;

    /// <summary>
    /// 5. oturum (1.1): hücre atanınca kayıt güncellenir. Eskiden alan olarak AddComponent'tan SONRA atanıyordu; OnEnable o anda (0,0)
    /// ile çalıştığından gerçek hücre hiç kaydolmuyor, (0,0)'da hayalet kayıt kalıyordu.
    /// </summary>
    public Vector2Int cell
    {
        get => _cell;
        set
        {
            if (ByCell.TryGetValue(_cell, out var current) && current == this) ByCell.Remove(_cell);
            _cell = value;
            if (isActiveAndEnabled) ByCell[_cell] = this;
        }
    }

    public string Id => MakeId(kind, cell);
    public static string MakeId(NatureKind kind, Vector2Int cell) => $"{kind}_{cell.x}_{cell.y}";

    public int RemoveCost => Progression.NatureRemoveCost(kind);

    public string DisplayName => kind == NatureKind.Tree ? L.T("Ağaç", "Tree")
        : kind == NatureKind.Bush ? L.T("Çalı", "Bush")
        : kind == NatureKind.Rock ? L.T("Kaya", "Rock") : L.T("Kütük", "Stump");

    private void OnEnable() => ByCell[_cell] = this;

    private void OnDisable()
    {
        if (ByCell.TryGetValue(_cell, out var current) && current == this) ByCell.Remove(_cell);
    }

    /// <summary>Hücre dikdörtgeninde doğa nesnesi var mı?</summary>
    public static bool AnyIn(RectInt cells)
    {
        if (ByCell.Count == 0) return false;
        for (int y = cells.yMin; y < cells.yMax; y++)
        for (int x = cells.xMin; x < cells.xMax; x++)
            if (ByCell.ContainsKey(new Vector2Int(x, y))) return true;
        return false;
    }

    /// <summary>Altın harcayıp nesneyi kaldırır ve kayda yazar. Altın yetmezse false.</summary>
    public bool TryRemove()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.SpendGold(RemoveCost)) return false;
        var data = gm.PlayerData;
        if (data.removedNatureIds == null) data.removedNatureIds = new List<string>();
        if (!data.removedNatureIds.Contains(Id)) data.removedNatureIds.Add(Id);
        BaseMapManager.Instance?.SaveAll();
        BaseBuildingVisual.SpawnDust(transform.position, 110);
        ByCell.Remove(cell);
        Destroy(gameObject);
        return true;
    }
}
