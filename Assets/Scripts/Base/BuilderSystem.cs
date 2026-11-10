using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Ana Üs sahnesindeki inşaatçıların görünümü. Asıl görevler ve süreler GameManager'da,
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

    /// <summary>Kiralanmış inşaatçılar (index = inşaatçı sırası).</summary>
    public Builder[] Builders { get; private set; } = { new Builder() };

    public event Action OnStatusChanged;

    // Bu oturumda bekleyen geri çağrılar; anahtar görevin kendisi (elmas/iksir bitiş anını değiştirebilir).
    private readonly Dictionary<BuilderTaskSave, Action> pendingCallbacks = new Dictionary<BuilderTaskSave, Action>();
    private int lastBusyCount = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Sync();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBuilderTaskCompleted += HandleCompleted;
            GameManager.Instance.OnBuilderHired += HandleHired;
        }
        Sync();
        OnStatusChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBuilderTaskCompleted -= HandleCompleted;
            GameManager.Instance.OnBuilderHired -= HandleHired;
        }
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        Sync();
        int busy = 0;
        foreach (var builder in Builders) if (!builder.isAvailable) busy++;
        // Geri sayım her karede yenilenir; meşgul inşaatçı yoksa yalnız durum değişince bildir.
        if (busy > 0 || busy != lastBusyCount) OnStatusChanged?.Invoke();
        lastBusyCount = busy;
    }

    private void Sync()
    {
        var gm = GameManager.Instance;
        int count = gm != null ? gm.WorkingBuilderCount : 1;
        if (Builders == null || Builders.Length != count)
        {
            var resized = new Builder[count];
            for (int i = 0; i < count; i++)
                resized[i] = Builders != null && i < Builders.Length ? Builders[i] : new Builder();
            Builders = resized;
        }
        for (int i = 0; i < Builders.Length; i++)
        {
            var b = Builders[i];
            var task = gm?.TaskForBuilder(i);
            if (task == null)
            {
                b.isAvailable = true;
                b.currentTask = "";
                b.timeRemaining = 0f;
                b.totalTime = 0f;
                continue;
            }
            b.isAvailable = false;
            b.currentTask = task.label;
            b.timeRemaining = (float)GameManager.SecondsRemaining(task);
            b.totalTime = (float)TimeSpan.FromTicks(task.endTicks - task.startTicks).TotalSeconds;
            b.targetPosition = TaskPosition(task);
        }
    }

    /// <summary>Yükseltilen bina taşınmış olabilir: konum sahnedeki binadan okunur.</summary>
    public static Vector3 TaskPosition(BuilderTaskSave task)
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
        if (pendingCallbacks.TryGetValue(task, out var callback))
        {
            pendingCallbacks.Remove(task);
            callback?.Invoke();
        }
        OnStatusChanged?.Invoke();
    }

    private void HandleHired(int total)
    {
        Sync();
        OnStatusChanged?.Invoke();
    }

    // ==================== API ====================

    public bool HasAvailableBuilder() => GameManager.Instance == null || GameManager.Instance.HasFreeBuilder;

    public int FreeCount => GameManager.Instance != null ? GameManager.Instance.FreeBuilderCount : 1;
    public int TotalCount => GameManager.Instance != null ? GameManager.Instance.WorkingBuilderCount : 1;

    /// <summary>Yeni yapı / kaldırma gibi işler için görev. Geri çağrı bu oturumda çalışır.</summary>
    public bool TryStartTask(string label, float duration, Vector3 targetPos, Action onComplete = null,
        string slotId = "")
    {
        var gm = GameManager.Instance;
        if (gm == null) { onComplete?.Invoke(); return true; }
        string kind = label == "Kaldırma" ? "remove" : "build";
        if (!gm.StartBuilderTask(kind, label, duration, targetPos, slotId)) return false;
        if (onComplete != null) pendingCallbacks[gm.LastStartedTask] = onComplete;
        Sync();
        OnStatusChanged?.Invoke();
        return true;
    }

    /// <summary>Üst köşe için durum metni</summary>
    public string GetStatusText()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HasFreeBuilder) return L.T("İnşaatçı: Müsait", "Builder: Free");
        return L.T("İnşaatçı: ", "Builder: ") + Progression.FormatDuration(gm.BuilderSecondsRemaining);
    }
}
