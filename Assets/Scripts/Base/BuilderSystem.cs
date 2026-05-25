using UnityEngine;
using System;

/// <summary>
/// İnşaatçı sistemi — görev atama, geri sayım, durum metni
/// BaseSceneSetup tarafından oluşturulur
/// </summary>
public class BuilderSystem : MonoBehaviour
{
    public static BuilderSystem Instance { get; private set; }

    // ==================== İNŞAATÇI SINIFI ====================

    [Serializable]
    public class Builder
    {
        public bool   isAvailable   = true;
        public string currentTask   = "";
        public float  timeRemaining = 0f;
        public float  totalTime     = 0f;
        public Vector3 targetPosition = Vector3.zero;
        [NonSerialized] public Action onComplete;

        public float Progress => totalTime > 0f ? Mathf.Clamp01(1f - timeRemaining / totalTime) : 0f;
    }


    // Slot 0: aktif inşaatçı | Slot 1: kilitli (IAP placeholder)
    public Builder[] Builders { get; private set; }

    public event Action OnStatusChanged;

    // ==================== UNITY YAŞAM DÖNGÜSÜ ====================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Builders = new Builder[]
        {
            new Builder(),
            new Builder { isAvailable = false }   // Kilitli slot
        };
    }

    private void Start()
    {
        // Kayıtlı inşaatçı durumunu geri yükle
        var saveData = GameManager.Instance?.PlayerData?.builderState;
        if (saveData != null && !saveData.isAvailable && saveData.timeRemaining > 0f)
        {
            Builders[0].isAvailable   = false;
            Builders[0].currentTask   = saveData.currentTask;
            Builders[0].timeRemaining = saveData.timeRemaining;
            Builders[0].totalTime     = saveData.totalTime > 0f ? saveData.totalTime : saveData.timeRemaining;
        }

        OnStatusChanged?.Invoke();
    }

    private void Update()
    {
        var b = Builders[0];
        if (b.isAvailable) return;

        b.timeRemaining -= Time.deltaTime;
        OnStatusChanged?.Invoke(); // Her frame UI güncellemesi

        if (b.timeRemaining <= 0f)
        {
            b.isAvailable   = true;
            b.currentTask   = "";
            b.timeRemaining = 0f;
            b.totalTime     = 0f;

            var cb = b.onComplete;
            b.onComplete = null;
            cb?.Invoke();

            OnStatusChanged?.Invoke();
            PersistState();
        }
    }

    private void OnApplicationPause(bool paused)  { if (paused) PersistState(); }
    private void OnApplicationQuit()              { PersistState(); }

    // ==================== API ====================

    public bool HasAvailableBuilder() => Builders[0].isAvailable;

    /// <summary>
    /// İnşaatçıya görev ata. Müsait değilse false döner.
    /// </summary>
    public bool TryStartTask(string label, float duration, Vector3 targetPos, Action onComplete = null)
    {
        var b = Builders[0];
        if (!b.isAvailable) return false;

        b.isAvailable    = false;
        b.currentTask    = label;
        b.timeRemaining  = duration;
        b.totalTime      = duration;
        b.targetPosition = targetPos;
        b.onComplete     = onComplete;

        OnStatusChanged?.Invoke();

        PersistState();
        return true;
    }

    /// <summary>Sol üst köşe için durum metni</summary>
    public string GetStatusText()
    {
        var b = Builders[0];
        if (b.isAvailable) return "İnşaatçı: Müsait ✓";

        int mins = Mathf.FloorToInt(b.timeRemaining / 60f);
        int secs = Mathf.FloorToInt(b.timeRemaining % 60f);
        return $"İnşaatçı: Meşgul ({mins}:{secs:D2})";
    }

    // ==================== KAYIT ====================

    private void PersistState()
    {
        if (GameManager.Instance?.PlayerData == null) return;
        var b = Builders[0];

        GameManager.Instance.PlayerData.builderState = new BuilderSaveData
        {
            isAvailable   = b.isAvailable,
            currentTask   = b.currentTask,
            timeRemaining = b.timeRemaining,
            totalTime     = b.totalTime
        };

        GameManager.Instance.SaveGame();
    }
}
