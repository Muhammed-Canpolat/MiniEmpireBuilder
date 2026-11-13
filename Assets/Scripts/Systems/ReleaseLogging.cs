using UnityEngine;

/// <summary>
/// 3. oturum (AŞAMA 4): yayın derlemesinde (editör ve Development dışı) Debug.Log çıktısı kapalıdır; yalnız uyarı, hata ve
/// istisnalar yazılır. Test kolaylıkları ayrıca derleme anında kapalıdır (<see cref="DevSettings.Available"/>).
/// </summary>
public static class ReleaseLogging
{
    public static bool IsRelease =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        false;
#else
        true;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        if (IsRelease) Debug.unityLogger.filterLogType = LogType.Warning;
    }
}
