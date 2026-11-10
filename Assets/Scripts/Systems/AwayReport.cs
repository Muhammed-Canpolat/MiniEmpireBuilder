using System;
using System.Collections.Generic;

/// <summary>
/// "Sen yokken" özeti: oyun kapalıyken ya da arka plandayken (en az 15 dk) neler oldu.
/// Dönüş anında <see cref="Begin"/> o anki maden/can değerlerini ve bitecek işleri not alır;
/// Ana Üs'teki <c>AwayReportUI</c> üretim uygulandıktan sonra farkı gösterir.
/// </summary>
public static class AwayReport
{
    public const double MinAwayMinutes = 15;

    public sealed class Data
    {
        public double seconds;
        public int storedBefore;
        public int livesBefore;
        public readonly List<string> finished = new List<string>();
    }

    public static Data Pending { get; private set; }
    public static event Action OnReady;

    public static void Begin(PlayerSaveData save, double awaySeconds)
    {
        if (save == null || awaySeconds < MinAwayMinutes * 60d) return;
        var data = new Data
        {
            seconds = awaySeconds,
            storedBefore = save.goldMineStoredGold,
            livesBefore = save.lives
        };
        long now = DateTime.UtcNow.Ticks;
        if (save.builderTasks != null)
            foreach (var task in save.builderTasks)
                if (task != null && task.IsActive && task.endTicks <= now)
                    data.finished.Add(task.kind == "remove" ? L.T("Temizlik", "Clearing")
                        : string.IsNullOrEmpty(task.label) ? L.T("İnşaat", "Construction") : task.label);
        Pending = data;
        OnReady?.Invoke();
    }

    /// <summary>Gösterildi: bir daha gösterilmez.</summary>
    public static Data Take()
    {
        var data = Pending;
        Pending = null;
        return data;
    }
}
