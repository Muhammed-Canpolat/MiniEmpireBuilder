using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BaseReviewCapture
{
    private static int frames;
    private static string pendingPath;
    private static EditorWindow captureView;

    [MenuItem("Tools/Base Review/Open Base Scene")]
    private static void OpenBaseScene()
    {
        if (!EditorApplication.isPlaying) return;
        Time.timeScale = 1f;
        SceneManager.LoadScene("BaseScene");
    }

    [MenuItem("Tools/Base Review/Toggle Fence Debug")]
    private static void ToggleFenceDebug()
    {
        if (!EditorApplication.isPlaying) return;
        var overlay = Object.FindObjectOfType<BaseDebugOverlay>();
        if (overlay == null) return;
        overlay.SetVisible(!overlay.Visible);
        Debug.Log("[BaseReview] Fence debug " + (overlay.Visible ? "on" : "off"));
    }

    [MenuItem("Tools/Base Review/Capture Item 1")]
    private static void CaptureItem1() => Capture(1);
    [MenuItem("Tools/Base Review/Capture Item 2")]
    private static void CaptureItem2() => Capture(2);
    [MenuItem("Tools/Base Review/Capture Item 3")]
    private static void CaptureItem3() => Capture(3);
    [MenuItem("Tools/Base Review/Capture Item 4")]
    private static void CaptureItem4() => Capture(4);
    [MenuItem("Tools/Base Review/Capture Item 5")]
    private static void CaptureItem5() => Capture(5);

    private static void Capture(int item)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Enter PlayMode before capturing a Game view screenshot.");
            return;
        }
        string folder = Path.Combine(Application.dataPath, "../UIReviewScreenshots/CurrentReview");
        Directory.CreateDirectory(folder);
        pendingPath = Path.GetFullPath(Path.Combine(folder, $"item-{item:00}.png"));
        captureView = SetGameViewResolution(390, 844);
        frames = 6;
        EditorApplication.update -= CaptureAfterFrames;
        EditorApplication.update += CaptureAfterFrames;
    }

    private static EditorWindow SetGameViewResolution(int width, int height)
    {
        var editorAssembly = typeof(Editor).Assembly;
        var sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
        var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singletonType.GetProperty("instance")?.GetValue(null);
        var groupType = editorAssembly.GetType("UnityEditor.GameViewSizeGroupType");
        var groupValue = System.Enum.Parse(groupType, "Android");
        var group = sizesType.GetMethod("GetGroup")?.Invoke(sizes, new[] { groupValue });
        var groupClass = group.GetType();
        var totalMethod = groupClass.GetMethod("GetTotalCount");
        var sizeMethod = groupClass.GetMethod("GetGameViewSize");
        int total = (int)totalMethod.Invoke(group, null);
        int index = -1;
        for (int i = 0; i < total; i++)
        {
            var size = sizeMethod.Invoke(group, new object[] { i });
            var type = size.GetType();
            int w = (int)type.GetProperty("width").GetValue(size);
            int h = (int)type.GetProperty("height").GetValue(size);
            if (w == width && h == height) { index = i; break; }
        }
        if (index < 0)
        {
            var sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
            var kindType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
            var kind = System.Enum.Parse(kindType, "FixedResolution");
            var newSize = System.Activator.CreateInstance(sizeType, kind, width, height, "Base Review 390x844");
            groupClass.GetMethod("AddCustomSize")?.Invoke(group, new[] { newSize });
            index = (int)totalMethod.Invoke(group, null) - 1;
        }
        var viewType = editorAssembly.GetType("UnityEditor.GameView");
        EditorWindow selected = null;
        foreach (var view in Resources.FindObjectsOfTypeAll(viewType))
        {
            var property = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            property?.SetValue(view, index);
            ((EditorWindow)view).Repaint();
            if (selected == null) selected = (EditorWindow)view;
        }
        selected?.Focus();
        return selected;
    }

    private static void CaptureAfterFrames()
    {
        if (--frames > 0) return;
        EditorApplication.update -= CaptureAfterFrames;
        captureView?.Focus();
        ScreenCapture.CaptureScreenshot(pendingPath);
        Debug.Log("[BaseReview] Game view screenshot: " + pendingPath);
    }
}
