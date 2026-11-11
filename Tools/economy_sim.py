"""
Ekonomi ilerleme simülasyonu (Mini Empire Builder) — normal ekonomi, elmas/reklam yok.

Sürekli oynayan "sıradan oyuncu": yeni aşama açıksa onu, değilse en yüksek aşamayı tekrar oynar (%30 ödül),
her savaş dönüşü madeni toplar, inşaatçı boşsa en iyi yükseltmeyi başlatır (önce Ana Üs, sonra maden, sonra
kuleler). Kazanma olasılığı bot ölçümlerinden (sıradan profil): S1 %100, S2-4 %85, S5 %60. Yenilgi 1 can.
Değerler oyundan kopyalandı (Progression, GameManager, region_0*.asset); değişirse burayı da güncelle.

Kullanım: python Tools/economy_sim.py   →  eski/yeni süre tablosu ile 1 sa ve 3 sa sonundaki durum.
"""
import random

MAIN_BASE_COSTS = [0, 0, 150, 250, 400, 600, 850, 1150, 1500, 1900, 2400, 3000, 3700, 4500, 5400, 6500]
FACTOR = {"MainBase": 1.0, "CannonTower": 0.7, "Monastery": 0.6, "GoldMine": 0.55, "ArcherTower": 0.5}
UNLOCK = {"ArcherTower": 2, "CannonTower": 4, "Monastery": 6}
OLD_TIMES = [0, 0, 15, 60, 1800, 3600, 10800, 21600, 43200, 64800, 86400, 108000, 129600, 151200, 172800, 172800]
NEW_TIMES = [0, 0, 10, 60, 180, 360, 600, 900, 1800, 3600, 7200, 10800, 14400, 21600, 28800, 43200]
REGION_REQ = [0, 1, 3, 6, 9, 12, 14]
REWARD = {r: [35 * r + 18 * s if r <= 5 else 265 + 18 * s for s in range(5)] for r in range(1, 7)}
WIN = [1.0, 0.85, 0.85, 0.85, 0.6]
BATTLE_MIN = [2.0, 3.0, 3.0, 3.5, 4.5]
KILL_GOLD_SHARE = 0.6  # savaş içi kazanılan net altın ≈ ödülün %60'ı (bot telemetrisi: kule harcaması sonrası)


def cost(t, level):
    base = MAIN_BASE_COSTS[min(level, 15)]
    return max(10, round(base * FACTOR[t] / 10) * 10)


def simulate(times, regen_min, hours, seed=1):
    rnd = random.Random(seed)
    t = 0.0
    gold, lives, regen_at = 50, 5, None
    lv = {"MainBase": 1, "GoldMine": 1, "ArcherTower": 0, "CannonTower": 0, "Monastery": 0}
    builders, tasks = 1, []  # (end_time, type)
    region, stage = 1, 0  # sıradaki açık aşama (0-index)
    mine_t = 0.0
    log = {}
    idle = [0.0]

    def cap():
        nxt = cost("MainBase", min(lv["MainBase"] + 1, 15))
        return max(500, -(-int(nxt * 1.5) // 100) * 100)

    def mine_store(now):
        rate = 1 + 0.5 * (lv["GoldMine"] - 1)
        return min(200 + 150 * (lv["GoldMine"] - 1), rate * (now - mine_t))

    def try_upgrades(now):
        nonlocal gold, builders
        tasks[:] = [x for x in tasks if x[0] > now]
        if lv["MainBase"] >= 5 and builders == 1 and gold >= cost("MainBase", 6):
            gold -= cost("MainBase", 6)
            builders = 2
        while len(tasks) < builders:
            busy = {x[1] for x in tasks}
            mb = lv["MainBase"]
            options = []
            if "MainBase" not in busy and mb < 15:
                options.append(("MainBase", mb + 1))
            for b in ("GoldMine", "ArcherTower", "CannonTower", "Monastery"):
                if b in busy:
                    continue
                cur = lv[b]
                if cur == 0 and mb >= UNLOCK.get(b, 1):
                    options.append((b, 1))
                elif 0 < cur < (min(mb + 1, 15) if b == "GoldMine" else mb):
                    options.append((b, cur + 1))
            options = [o for o in options if gold >= cost(o[0], max(2, o[1]))]
            if not options:
                return
            b, target = options[0]
            gold -= cost(b, max(2, target))
            dur = times[target] if target >= 2 else 10
            tasks.append((now + dur, b, target))
            lv_pending[b] = target

    lv_pending = {}

    def finish(now):
        for task in list(tasks):
            if task[0] <= now:
                lv[task[1]] = task[2]
                tasks.remove(task)

    end = hours * 3600
    marks = [3600, 3 * 3600]
    def mark(now):
        for m in marks:
            if now >= m and m not in log:
                log[m] = (lv["MainBase"], f"B{min(region, 6)}-{stage + 1}", dict(lv))

    while t < end:
        mark(t)
        finish(t)
        try_upgrades(t)
        # canlar
        if lives < 5 and regen_at is not None and t >= regen_at:
            lives += 1
            regen_at = t + regen_min * 60 if lives < 5 else None
        if lives == 0:
            t = regen_at
            idle[0] += regen_min * 60
            continue
        # savaş seç
        if region <= 6 and lv["MainBase"] >= REGION_REQ[region]:
            r, s, replay = region, stage, False
        else:
            r, s, replay = (region - 1, 4, True) if stage == 0 else (region, stage - 1, True)
            if r < 1:
                r, s = 1, 0
        dt = BATTLE_MIN[s] * 60
        t += dt
        won = rnd.random() < WIN[s]
        if won:
            pay = REWARD[r][s] * (1 + KILL_GOLD_SHARE)
            if replay:
                pay *= 0.3
            gold = min(cap(), gold + int(pay))
            if not replay:
                stage += 1
                if stage == 5:
                    region, stage = region + 1, 0
        else:
            lives -= 1
            if regen_at is None:
                regen_at = t + regen_min * 60
        gold = min(cap(), gold + int(mine_store(t)))
        mine_t = t
    mark(end)
    return log


if __name__ == "__main__":
    for name, times, regen in (("ESKI (30 dk can)", OLD_TIMES, 30), ("YENI (20 dk can)", NEW_TIMES, 20)):
        res = [simulate(times, regen, 3.2, seed) for seed in range(1, 21)]
        for m, label in ((3600, "1 saat"), (3 * 3600, "3 saat")):
            mbs = sorted(x[m][0] for x in res)
            stages = [x[m][1] for x in res]
            print(f"{name} {label}: Ana Us medyan {mbs[len(mbs)//2]} (min {mbs[0]}, max {mbs[-1]}), sefer ~{max(set(stages), key=stages.count)}")
