using UnityEngine;
using System;

/// <summary>
/// Ana Üs sahnesindeki inşaatçının görünümü. Asıl görev ve süre GameManager'da,
/// gerçek saate (UTC) bağlı tutulur: oyun kapalıyken ve savaştayken de ilerler.
/// Bu bileşen yalnız durumu yansıtır ve sahne içi tamamlanma geri çağrılarını çalıştırır.
/// </summary>
public class BuilderSystem : MonoBehaviour
{
    public static BuilderSystem Instance { get; private set; }

    [Serializable]
    public class Builder
    {
        public bool   isAvailable   = true;
        public string currentTask   = "";
        public float  timeRemaining = 0f;
        public float  totalTime     = 0f;
        public Vector3 targetPosition = Vector3.zero;

        public float Progress => totalTime > 0f ? Mathf.Clamp01(1f - timeRemaining / totalTime) : 0f;
    }

    // Slot 0: aktif inşaatçı | Slot 1: kilitli (ileride ikinci inşaatçı)
    public Builder[] Builders { get; private set; }

    public event Action OnStatusChanged;

    private Action pendingCallback;
    private long pendingTaskEnd;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Builders = new[] { new Builder(), new Builder { isAvailable = false } };
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnBuilderTaskCompleted += HandleCompleted;
        Sync();
        OnStatusChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnBuilderTaskCompleted -= HandleCompleted;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        bool wasBusy = !Builders[0].isAvailable;
        Sync();
        if (wasBusy || !Builders[0].isAvailable) OnStatusChanged?.Invoke();
    }

    private void Sync()
    {
        var gm = GameManager.Instance;
        var b = Builders[0];
        if (gm == null || !gm.IsBuilderBusy)
        {
            b.isAvailable = true;
            b.currentTask = "";
            b.timeRemaining = 0f;
            b.totalTime = 0f;
            return;
        }
        var task = gm.BuilderTask;
        b.isAvailable = false;
        b.currentTask = task.label;
        b.timeRemaining = (float)gm.BuilderSecondsRemaining;
        b.totalTime = (float)TimeSpan.FromTicks(task.endTicks - task.startTicks).TotalSeconds;
        b.targetPosition = TaskPosition(task);
    }

    /// <summary>Yükseltilen bina taşınmış olabilir: konum sahnedeki binadan okunur.</summary>
    private static Vector3 TaskPosition(BuilderTaskSave task)
    {
        if (!string.IsNullOrEmpty(task.slotId) && BaseMapManager.Instance != null)
        {
            var building = BaseMapManager.Instance.GetMapObjectBySlot(task.slotId);
            if (building?.gameObject != null) return building.gameObject.transform.position;
        }
        return new Vector3(task.x, task.y, 0f);
    }

    private void HandleCompleted(BuilderTaskSave task)
    {
        Sync();
        if (pendingCallback != null && task.endTicks == pendingTaskEnd)
        {
            var callback = pendingCallback;
            pendingCallback = null;
            callback();
        }
        OnStatusChanged?.Invoke();
    }

    // ==================== API ====================

    public bool HasAvailableBuilder() => GameManager.Instance == null || !GameManager.Instance.IsBuilderBusy;

    /// <summary>Yeni yapı / kaldırma gibi işler için görev. Geri çağrı bu oturumda çalışır.</summary>
    public bool TryStartTask(string label, float duration, Vector3 targetPos, Action onComplete = null)
    {
        var gm = GameManager.Instance;
        if (gm == null) { onComplete?.Invoke(); return true; }
        string kind = label == "Kaldırma" ? "remove" : "build";
        if (!gm.StartBuilderTask(kind, label, duration, targetPos)) return false;
        pendingCallback = onComplete;
        pendingTaskEnd = gm.BuilderTask.endTicks;
        Sync();
        OnStatusChanged?.Invoke();
        return true;
    }

    /// <summary>Üst köşe için durum metni</summary>
    public string GetStatusText()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsBuilderBusy) return "İnşaatçı: Müsait";
        return $"İnşaatçı: {Progression.FormatDuration(gm.BuilderSecondsRemaining)}";
    }
}
