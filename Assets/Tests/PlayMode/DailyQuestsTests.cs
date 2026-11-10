using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Günlük görevler: tarihe göre 3 görev, ilerleme, ödül, bonus; "sen yokken" özeti.</summary>
public class DailyQuestsTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Call(string method, params object[] args) => Type("DailyQuests").GetMethod(method).Invoke(null, args);
    private static object Static(string member) => Type("DailyQuests").GetProperty(member).GetValue(null);

    [UnityTest]
    public IEnumerator QuestsProgressClaimAndBonus()
    {
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Axe") });
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        var day = Type("DailyQuests").GetProperty("TodayOverride");
        day.SetValue(null, (DateTime?)new DateTime(2026, 11, 10));
        try
        {
            // Aynı gün hep aynı 3 farklı görev.
            var kinds = Enumerable.Range(0, 3).Select(i => Call("Quest", i)).Select(q => q.GetType().GetField("kind").GetValue(q).ToString()).ToList();
            Assert.AreEqual(3, kinds.Distinct().Count());
            var kindsAgain = Enumerable.Range(0, 3).Select(i => Call("Quest", i)).Select(q => q.GetType().GetField("kind").GetValue(q).ToString()).ToList();
            CollectionAssert.AreEqual(kinds, kindsAgain);

            // Her görevi bitir, ödülünü al; bonus en sonda.
            Assert.IsFalse((bool)Call("ClaimBonus"));
            for (int i = 0; i < 3; i++)
            {
                var quest = Call("Quest", i);
                int target = (int)quest.GetType().GetField("target").GetValue(quest);
                Assert.IsNull(Call("Claim", i), "bitmeden alınmaz");
                Call("Report", Enum.Parse(Type("QuestKind"), kinds[i]), target + 5);
                Assert.IsTrue((bool)Call("IsDone", i));
                Assert.AreEqual(target, Call("Progress", i), "ilerleme hedefte durur");
            }
            Assert.IsTrue((bool)Static("HasClaimable"));
            for (int i = 0; i < 3; i++) Assert.NotNull(Call("Claim", i));
            int gems = (int)managerType.GetProperty("Gems").GetValue(manager);
            Assert.IsTrue((bool)Call("ClaimBonus"));
            Assert.AreEqual(gems + 5, managerType.GetProperty("Gems").GetValue(manager));
            Assert.IsFalse((bool)Static("HasClaimable"));

            // Ertesi gün yeni görevler, sıfır ilerleme.
            day.SetValue(null, (DateTime?)new DateTime(2026, 11, 11));
            Assert.AreEqual(0, Call("Progress", 0));
            Assert.IsFalse((bool)Call("IsClaimed", 0));
        }
        finally
        {
            day.SetValue(null, null);
        }

        // "Sen yokken": 15 dk altı not alınmaz; üstünde biten işler listelenir.
        var away = Type("AwayReport");
        away.GetMethod("Take").Invoke(null, null);
        away.GetMethod("Begin").Invoke(null, new object[] { save, 60d });
        Assert.IsNull(away.GetProperty("Pending").GetValue(null));
        managerType.GetMethod("StartBuilderTask").Invoke(manager, new object[] { "remove", "Kaldırma", 10f, Vector3.zero, "", "" });
        var task = managerType.GetProperty("LastStartedTask").GetValue(manager);
        task.GetType().GetField("endTicks").SetValue(task, DateTime.UtcNow.Ticks - 1);
        away.GetMethod("Begin").Invoke(null, new object[] { save, 3600d });
        var report = away.GetMethod("Take").Invoke(null, null);
        Assert.NotNull(report);
        Assert.AreEqual(1, ((IList)report.GetType().GetField("finished").GetValue(report)).Count);
    }
}
