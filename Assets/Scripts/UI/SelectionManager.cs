using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 4. oturum (1.2): Ana Üs ve adadaki seçilebilir öğelerin (bina, dekor, kule, ağaç/kaya istemi) TEK seçim yöneticisi. Aynı anda en
/// çok bir seçim açıktır: yeni seçim öncekini kapatır; boş yere ya da başka bir öğeye dokunmak ve geri tuşu (Android geri = Escape)
/// seçimi iptal eder. Her seçim kendi kapatma işlevini verir (panel/eylem düğmeleri yok edilir).
/// </summary>
public static class SelectionManager
{
    private static object owner;
    private static Action closer;
    private static int selectedFrame = -1;

    public static object Current => owner;
    public static bool HasSelection => owner != null;
    /// <summary>Bu karede yapılan seçim aynı dokunuşla kapatılmasın diye.</summary>
    public static bool SelectedThisFrame => selectedFrame == Time.frameCount;
    public static event Action Changed;

    /// <summary><paramref name="item"/> seçildi; <paramref name="close"/> seçim kalkınca çağrılır (öncekini kapatır).</summary>
    public static void Select(object item, Action close)
    {
        if (item == null) return;
        if (owner != null && !ReferenceEquals(owner, item)) Clear();
        owner = item;
        closer = close;
        selectedFrame = Time.frameCount;
        Changed?.Invoke();
    }

    /// <summary>Öğe kendisi kapandıysa (düğmeye basıldı vb.) kaydı düşürür; kapatma işlevini çağırmaz.</summary>
    public static void Release(object item)
    {
        if (owner == null || !ReferenceEquals(owner, item)) return;
        owner = null;
        closer = null;
        Changed?.Invoke();
    }

    public static bool IsSelected(object item) => owner != null && ReferenceEquals(owner, item);

    /// <summary>Açık seçimi kapatır; kapatılacak bir şey varsa true.</summary>
    public static bool Clear()
    {
        if (owner == null) return false;
        var close = closer;
        owner = null;
        closer = null;
        try { close?.Invoke(); }
        catch (Exception e) { Debug.LogWarning("[Selection] kapatma hatası: " + e.Message); }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Sahne değişiminde kaydı sıfırlar (yok edilen nesnelere kapatma çağrılmaz).</summary>
    public static void Reset()
    {
        owner = null;
        closer = null;
    }

    /// <summary>Sahneye geri tuşu dinleyicisini kurar (bir kez).</summary>
    public static void InstallBackKey()
    {
        Reset();
        if (UnityEngine.Object.FindFirstObjectByType<SelectionBackKey>() != null) return;
        new GameObject("SelectionBackKey").AddComponent<SelectionBackKey>();
    }
}

/// <summary>Geri tuşu (Android) / Escape: açık seçimi iptal eder.</summary>
public sealed class SelectionBackKey : MonoBehaviour
{
    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SelectionManager.Clear();
#endif
    }
}
