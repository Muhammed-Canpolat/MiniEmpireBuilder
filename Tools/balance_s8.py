"""
8. oturum: yeni düşman sayıları + %10 can sonrası denge özeti (profil: good = kule + kahraman, towerOnly, heroOnly). PlaytestOutput/battle_r{R}_l{L}_s{S}[_profil].json dosyalarını okur;
bölge × seviye × profil için kazanma, kalan Ana Üs canı, süre, kahraman hasar payı, kahraman düşüşü tablosu yazar.
Çalıştırma: python Tools/balance_s7.py [çıktı.md]
"""
import glob
import json
import os
import re
import sys
from collections import defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
rows = defaultdict(list)
for path in glob.glob(os.path.join(ROOT, "PlaytestOutput", "battle_r*_l*_s*.json")):
    m = re.search(r"battle_r(\d+)_l(\d+)_s(\d+)(?:_(\w+))?\.json", os.path.basename(path))
    if not m:
        continue
    t = json.load(open(path, encoding="utf-8"))
    profile = m.group(4) or "good"
    rows[(int(m.group(1)), int(m.group(2)), profile)].append(t)

KIND = {1: "Hayatta Kal", 2: "Üssü Koru", 3: "Madeni Koru", 4: "Zamana Karşı", 5: "Boss"}
out = ["| Bölge | Seviye | Profil | Kazanma | Ana Üs canı (son) | Süre (sn) | Kahraman hasar payı | Kahraman düşüş | Yenilgi nedeni |",
       "|---|---|---|---|---|---|---|---|---|"]
for (r, l, p) in sorted(rows):
    ts = rows[(r, l, p)]
    wins = sum(1 for t in ts if t["result"] == "win")
    base = sum(t["baseHealthEnd"] for t in ts) / len(ts)
    dur = sum(t["durationSec"] for t in ts) / len(ts)
    share = sum(t["heroDamageShare"] for t in ts) / len(ts)
    downs = sum(t["heroDowns"] for t in ts) / len(ts)
    reasons = ",".join(sorted({t.get("defeatReason") or "-" for t in ts if t["result"] != "win"})) or "-"
    out.append(f"| {r} | {l} {KIND[l]} | {p} | {wins}/{len(ts)} | %{base * 100:.0f} | {dur:.0f} | %{share * 100:.0f} | {downs:.1f} | {reasons} |")
text = "\n".join(out)
print(text)
if len(sys.argv) > 1:
    open(sys.argv[1], "w", encoding="utf-8").write(text + "\n")
