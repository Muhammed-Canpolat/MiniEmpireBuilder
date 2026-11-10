using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif

/// <summary>
/// Telefon (yerel) bildirimleri: oyun arka plana alınınca planlanır, açılınca hepsi silinir.
/// Ne zaman ne gönderileceği <see cref="NotificationPlanner"/>'da (saf hesap, test edilir);
/// Android'e gönderen kısım yalnız cihazda derlenir (com.unity.mobile.notifications).
/// Ayarlar'dan kapatılabilir; Android 13+ izni ilk Ana Üs girişinde bir kez sorulur.
/// </summary>
public sealed class GameNotifications : MonoBehaviour
{
    private const string EnabledKey = "MiniEmpireBuilder_Notifications";
    private const string AskedKey = "MiniEmpireBuilder_NotificationsAsked";
    private const string ChannelId = "game";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(EnabledKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            PlayerPrefs.Save();
            if (!value) CancelAll();
            else if (Instance != null) Instance.RequestPermissionOnce(true);
        }
    }

    private static GameNotifications Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Instance != null) return;
        var go = new GameObject("GameNotifications");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GameNotifications>();
    }

    private void Start()
    {
        RegisterChannel();
        CancelAll();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, _) =>
        {
            if (scene.name == "BaseScene") RequestPermissionOnce(false);
        };
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) ScheduleAll();
        else CancelAll();
    }

    private void OnApplicationQuit() => ScheduleAll();

    private void ScheduleAll()
    {
        CancelAll();
        var gm = GameManager.Instance;
        if (!Enabled || gm == null || gm.PlayerData == null) return;
        foreach (var item in NotificationPlanner.Plan(gm, DateTime.Now)) Send(item);
    }

    // ==================== PLATFORM ====================

    private static void RegisterChannel()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
        {
            Id = ChannelId,
            Name = L.T("Krallık", "Kingdom"),
            Description = L.T("İnşaat, can, maden ve günlük ödül hatırlatmaları",
                "Construction, lives, mine and daily reward reminders"),
            Importance = Importance.Default
        });
#endif
    }

    private static void Send(NotificationPlanner.Planned item)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidNotificationCenter.SendNotificationWithExplicitID(new AndroidNotification
        {
            Title = item.title,
            Text = item.body,
            FireTime = item.fireLocal
        }, ChannelId, item.id);
#else
        Debug.Log($"[Bildirim] {item.fireLocal:dd.MM HH:mm} · {item.title} · {item.body}");
#endif
    }

    private static void CancelAll()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidNotificationCenter.CancelAllNotifications();
#endif
    }

    /// <summary>Android 13+ bildirim izni: bir kez (ya da kullanıcı Ayarlar'dan açınca) sorulur.</summary>
    private void RequestPermissionOnce(bool force)
    {
        if (!Enabled || (!force && PlayerPrefs.GetInt(AskedKey, 0) == 1)) return;
        PlayerPrefs.SetInt(AskedKey, 1);
        PlayerPrefs.Save();
        StartCoroutine(RequestPermission());
    }

    private IEnumerator RequestPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var request = new PermissionRequest();
        while (request.Status == PermissionStatus.RequestPending) yield return null;
#else
        yield break;
#endif
    }
}

/// <summary>Bildirim planı: oyun durumundan hangi bildirimin ne zaman atılacağını hesaplar.</summary>
public static class NotificationPlanner
{
    public struct Planned
    {
        public int id;
        public string title;
        public string body;
        public DateTime fireLocal;
    }

    /// <summary>Gece rahatsız etmez: 22:00–09:00 arasına düşen bildirim sabah 09:00'a kayar.</summary>
    public const int QuietStartHour = 22;
    public const int QuietEndHour = 9;
    public const int DailyReminderHour = 19;
    public const int ComebackDays = 3;

    public static DateTime AvoidNight(DateTime time)
    {
        if (time.Hour >= QuietStartHour) return time.Date.AddDays(1).AddHours(QuietEndHour);
        if (time.Hour < QuietEndHour) return time.Date.AddHours(QuietEndHour);
        return time;
    }

    public static List<Planned> Plan(GameManager gm, DateTime nowLocal)
    {
        var list = new List<Planned>();
        var data = gm.PlayerData;
        void Add(int id, string title, string body, DateTime when)
        {
            when = AvoidNight(when);
            if (when <= nowLocal.AddMinutes(1)) return;
            list.Add(new Planned { id = id, title = title, body = body, fireLocal = when });
        }

        // İnşaat: her iş bitince.
        int index = 0;
        foreach (var task in gm.BuilderTasks)
        {
            if (task == null || !task.IsActive) continue;
            var end = new DateTime(task.endTicks, DateTimeKind.Utc).ToLocalTime();
            string what = task.kind == "remove" ? L.T("Temizlik", "Clearing")
                : string.IsNullOrEmpty(task.label) ? L.T("İnşaat", "Construction") : task.label;
            Add(100 + index++, L.T("İnşaat bitti!", "Construction complete!"),
                L.T($"{what} hazır. İnşaatçın yeni iş bekliyor.", $"{what} is ready. Your builder is waiting."), end);
        }

        // Can: hepsi dolunca.
        if (!LivesSystem.Unlimited && LivesSystem.Lives < LivesSystem.MaxLives)
        {
            var full = nowLocal + LivesSystem.NextLifeIn
                + TimeSpan.FromMinutes(LivesSystem.RegenMinutes * (LivesSystem.MaxLives - LivesSystem.Lives - 1));
            Add(200, L.T("Canların doldu!", "Lives refilled!"),
                L.T("Sefer seni bekliyor: 5 canla savaşa dön.", "The campaign awaits: return with 5 lives."), full);
        }

        // Maden: depo dolunca.
        float gps = gm.GetGoldPerSecond();
        int capacity = gm.GetGoldMineStorageCapacity();
        int stored = gm.GoldMineStored;
        if (gps > 0f && capacity > 0 && stored < capacity)
            Add(300, L.T("Maden doldu!", "Mine is full!"),
                L.T($"{capacity} altın toplanmayı bekliyor.", $"{capacity} gold is waiting to be collected."),
                nowLocal.AddSeconds((capacity - stored) / gps));

        // Günlük ödül: alınmadıysa bugün 19:00, alındıysa yarın 19:00.
        var reminder = nowLocal.Date.AddHours(DailyReminderHour);
        if (!DailyRewards.CanClaim || reminder <= nowLocal) reminder = reminder.AddDays(1);
        Add(400, L.T("Günlük ödülün hazır", "Daily reward ready"),
            L.T("Seriyi bozma: bugünün ödülünü al.", "Keep your streak: claim today's reward."), reminder);

        // Uzun süre girilmezse bir hatırlatma.
        Add(900, L.T("Krallığın seni bekliyor", "Your kingdom awaits"),
            L.T("Maden doldu, inşaatçılar boşta. Geri dön ve krallığını büyüt!",
                "The mine is full and builders are idle. Come back and grow your kingdom!"),
            nowLocal.Date.AddDays(ComebackDays).AddHours(DailyReminderHour));
        return list;
    }
}
