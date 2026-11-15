"""AAB denetimi (AŞAMA 4): 16 KB sayfa hizası, ABI'ler, test kolaylığı izleri, izinler.

Kullanım: python Tools/check_aab.py Builds/Release/MiniEmpireBuilder-x.aab
- Her lib/<abi>/*.so için ELF64 PT_LOAD segmentlerinin p_align değeri >= 16384 olmalı (Android 16 KB sayfa cihazları).
- global-metadata.dat içinde test kolaylığı sınıf adları (DevMenu, PlaytestBot, DevSettings'in kolaylık anahtarları) olmamalı.
- AndroidManifest.xml (protobuf) içindeki izin adları kaba olarak listelenir.
- GNU_RELRO denetimi (SEZGİSEL): RELRO bitişi 16 KB'a hizalı ya da içeren LOAD'un bitişine eşit olmalı.
  Bu kural yalnızca bir sezgisel denetimdir; kesin sonuç Play Console'un "16 KB desteği" raporu ve
  16 KB'lık emülatör görüntüsüyle doğrulanmalıdır.
- Manifest oyun beyanı (appCategory=game / isGame) ve screenOrientation izi kaba olarak aranır (tam ayrıştırma yok).
  Doğrulama için: bundletool dump manifest.
"""
import struct
import sys
import zipfile

# Bu sınıf/metot adları yayın derlemesinde hiç olmamalı (#if UNITY_EDITOR || DEVELOPMENT_BUILD).
# Not: DevSettings sınıfı ve özellik adları (GemTapShortcut, UnlimitedGems) yayında da bulunur ama Available=false sabitiyle hep false döner.
CHEAT_MARKERS = [b"DevMenu", b"PlaytestBot", b"PlaytestJourney", b"BattleDebugOverlay", b"BaseDebugOverlay", b"DevAdvanceTime", b"DevSetPlayerData"]


def load_aligns(data):
    if data[:4] != b"\x7fELF":
        return None
    if data[4] != 2:
        return "ELF32"
    e_phoff = struct.unpack_from("<Q", data, 0x20)[0]
    e_phentsize, e_phnum = struct.unpack_from("<HH", data, 0x36)
    aligns = []
    for i in range(e_phnum):
        off = e_phoff + i * e_phentsize
        p_type = struct.unpack_from("<I", data, off)[0]
        if p_type == 1:  # PT_LOAD
            aligns.append(struct.unpack_from("<Q", data, off + 0x30)[0])
    return aligns


PT_LOAD = 1
PT_GNU_RELRO = 0x6474E552
PAGE_16K = 16384


def check_relro(data):
    """ELF64 için GNU_RELRO denetimi. Dönüş: None (ELF64 değil), 'yok', ya da (başlangıç, bitiş, uygun_mu)."""
    if len(data) < 0x40 or data[:4] != b"ELF" or data[4] != 2:
        return None
    e_phoff = struct.unpack_from("<Q", data, 0x20)[0]
    e_phentsize, e_phnum = struct.unpack_from("<HH", data, 0x36)
    loads, relro = [], None
    for i in range(e_phnum):
        off = e_phoff + i * e_phentsize
        p_type = struct.unpack_from("<I", data, off)[0]
        p_vaddr = struct.unpack_from("<Q", data, off + 0x10)[0]
        p_memsz = struct.unpack_from("<Q", data, off + 0x28)[0]
        if p_type == PT_LOAD:
            loads.append((p_vaddr, p_vaddr + p_memsz))
        elif p_type == PT_GNU_RELRO:
            relro = (p_vaddr, p_vaddr + p_memsz)
    if relro is None:
        return "yok"
    start, end = relro
    # RELRO bitişi 16 KB'a hizalı ya da onu içeren LOAD segmentinin bitişine eşit olmalı.
    good = end % PAGE_16K == 0 or any(ls <= start < le and le == end for ls, le in loads)
    return start, end, good


def main(path):
    ok = True
    with zipfile.ZipFile(path) as z:
        names = z.namelist()
        abis = sorted({n.split("/")[2] for n in names if "/lib/" in n and n.count("/") >= 3})
        print("ABI:", ", ".join(abis) or "(yok)")
        for n in names:
            if not n.endswith(".so"):
                continue
            aligns = load_aligns(z.read(n))
            info = z.getinfo(n)
            good = isinstance(aligns, list) and aligns and min(aligns) >= 16384
            ok &= bool(good)
            print(f"{'OK ' if good else 'BAD'} {n}  LOAD p_align={aligns}  ({info.file_size/1048576:.1f} MB)")
            r = check_relro(z.read(n))
            if r == "yok":
                print(f"    RELRO yok: {n}")
            elif r is not None:
                rs, re_, rgood = r
                ok &= rgood
                if rgood:
                    print(f"    RELRO OK  {n}  başlangıç=0x{rs:x} bitiş=0x{re_:x}")
                else:
                    print(f"    UYARI: RELRO bitişi 16 KB'a hizalı değil ve LOAD bitişine eşit değil: {n}  başlangıç=0x{rs:x} bitiş=0x{re_:x}")
        meta = [n for n in names if n.endswith("global-metadata.dat")]
        for n in meta:
            blob = z.read(n)
            found = [m.decode() for m in CHEAT_MARKERS if m in blob]
            print("Test kolaylığı izi (metadata):", ", ".join(found) if found else "yok")
            ok &= not found
        man = [n for n in names if n.endswith("AndroidManifest.xml") and n.startswith("base/manifest")]
        for n in man:
            blob = z.read(n)
            perms = sorted({p.decode() for p in __import__("re").findall(rb"android\.permission\.[A-Z_]+", blob)})
            print("İzinler:", ", ".join(perms))
            idx = blob.find(b"appCategory")
            cat = idx >= 0 and b"game" in blob[idx:idx + 11 + 64]
            isgame = b"isGame" in blob
            print(f"Oyun beyanı: appCategory=game {'VAR' if cat else 'BULUNAMADI'} (isGame: {'var' if isgame else 'yok'})")
            if not cat and not isgame:
                print("UYARI: büyük ekranda yön kilidi muafiyeti için doğrulayın (bundletool dump manifest)")
            print("screenOrientation:", "geçiyor" if b"screenOrientation" in blob else "geçmiyor")
    print("SONUÇ:", "UYGUN" if ok else "SORUN VAR")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
