using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 5. oturum (2.1): dünyadaki seçili nesnenin (dekor, bina, kule) HEMEN üstünde duran küçük, süslü eylem menüsü. Altın halkalı koyu yuvarlak
/// düğmeler (HUD ailesi: radyal menü / <see cref="WorldConfirmButtons"/>), ortada simge, altında koyu hap üstünde kısa etiket ve (varsa)
/// bedel/iade satırı (simge + sayı). Nesneyi her kare izler; güvenli alandan taşmaz (yan boşluk 12, üst/alt bar payı 150); üstte yer
/// yoksa nesnenin altına geçer. "Onay gerekir" işaretli bir düğmeye ilk dokunuş menüyü küçük bir ✓/✗ onay satırına çevirir; yalnız ✓ eylemi yapar.
/// Seçili nesne açıkken yumuşak altın bir ışıkla vurgulanır (menü kapanınca kalkar). Seçim <see cref="SelectionManager"/> üzerindendir:
/// boş yere dokunmak, başka nesne ve geri tuşu menüyü kapatır.
/// </summary>
public sealed class WorldActionMenu : MonoBehaviour
{
    /// <summary>Menü düğmesi tanımı.</summary>
    public sealed class Item
    {
        public string id;
        public Sprite icon;
        public string label;
        /// <summary>Bedel/iade satırı (isteğe bağlı): simge + sayı metni, ör. altın simgesi + "+2".</summary>
        public Sprite costIcon;
        public string costText;
        public Action action;
        /// <summary>Doğruysa ilk dokunuş onay satırı açar; eylem yalnız ✓ ile yapılır.</summary>
        public bool confirm;
        /// <summary>Onay satırının sorusu (boşsa "Emin misin?").</summary>
        public string question;
    }

    public const float ButtonSize = 48f;
    private const float ColumnWidth = 68f, ColumnGap = 6f, LabelHeight = 16f, CostHeight = 18f, HeaderHeight = 22f, RowGap = 3f;

    /// <summary>Açık menü (yoksa null).</summary>
    public static WorldActionMenu Current { get; private set; }

    private Func<Bounds> bounds;
    private Camera cam;
    private RectTransform rect, parentRect;
    private IList<Item> items;
    private object owner;
    private SpriteRenderer target;
    private SpriteRenderer highlight;
    private bool closed;
    private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
    private float width, height;

    public RectTransform Rect => rect;
    public bool IsConfirming { get; private set; }
    /// <summary>Son yerleşimde menünün nesneye göre yeri (test): "above" / "below".</summary>
    public string Side { get; private set; } = "";
    public GameObject Highlight => highlight != null ? highlight.gameObject : null;
    public Button ButtonFor(string id) => id != null && buttons.TryGetValue(id, out var b) ? b : null;
    public IEnumerable<string> ButtonIds => buttons.Keys;

    /// <param name="safe">Tuvalin güvenli alanı.</param>
    /// <param name="worldBounds">Nesnenin dünya sınırı; her kare okunur.</param>
    /// <param name="owner">SelectionManager sahibi (boş yere dokunuşta menü kapanır).</param>
    /// <param name="highlightTarget">Vurgulanacak nesnenin görseli (yoksa vurgu yok).</param>
    public static WorldActionMenu Show(Transform safe, Func<Bounds> worldBounds, Camera camera, IList<Item> items, string name = "WorldActionMenu",
        object owner = null, SpriteRenderer highlightTarget = null)
    {
        if (safe == null || items == null || items.Count == 0) return null;
        Current?.Close();
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(safe, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        var m = go.AddComponent<WorldActionMenu>();
        m.bounds = worldBounds;
        m.cam = camera;
        m.rect = rt;
        m.parentRect = (RectTransform)safe;
        m.items = items;
        m.owner = owner;
        m.target = highlightTarget;
        Current = m;
        m.Build(items, null, null, null);
        m.CreateHighlight();
        m.Follow();
        rt.localScale = Vector3.one * 0.6f;
        rt.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(go);
        if (owner != null) SelectionManager.Select(owner, m.Close);
        return m;
    }

    public void Close()
    {
        if (closed) return;
        closed = true;
        if (Current == this) Current = null;
        if (highlight != null) Destroy(highlight.gameObject);
        highlight = null;
        if (owner != null && SelectionManager.IsSelected(owner)) SelectionManager.Release(owner);
        gameObject.SetActive(false);   // Destroy kare sonunda işlenir; menü hemen "kapalı" görünsün
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        if (highlight != null) Destroy(highlight.gameObject);
    }

    // ==================== YAPI ====================

    /// <summary>Menü satırını kurar. <paramref name="headerText"/> doluysa üstte soru hapı (onay satırı) durur.</summary>
    private void Build(IList<Item> list, string headerText, Sprite headerIcon, string headerCost)
    {
        for (int i = rect.childCount - 1; i >= 0; i--) { var c = rect.GetChild(i); c.gameObject.SetActive(false); Destroy(c.gameObject); }
        buttons.Clear();
        bool anyCost = false;
        foreach (var it in list) if (!string.IsNullOrEmpty(it.costText)) anyCost = true;
        float labelY = anyCost ? CostHeight + RowGap : 0f;
        float buttonY = labelY + LabelHeight + RowGap;
        float top = buttonY + ButtonSize;
        bool header = !string.IsNullOrEmpty(headerText);
        float headerY = top + 4f;
        if (header) top = headerY + HeaderHeight;
        int n = list.Count;
        float rowWidth = n * ColumnWidth + (n - 1) * ColumnGap;
        width = rowWidth;
        height = top;
        RectTransform headerRect = null;
        if (header)
        {
            headerRect = Pill("Header", headerText, 13f, null, headerCost, headerIcon, HeaderHeight, 0f);
            width = Mathf.Max(width, headerRect.sizeDelta.x);
        }
        rect.sizeDelta = new Vector2(width, height);
        if (headerRect != null) PlacePill(headerRect, new Vector2(width * 0.5f, headerY));
        float startX = (width - rowWidth) * 0.5f;
        for (int i = 0; i < n; i++)
        {
            var it = list[i];
            float cx = startX + i * (ColumnWidth + ColumnGap) + ColumnWidth * 0.5f;
            var item = it;
            Round(it.id, cx, buttonY, it.icon, () => OnPressed(item));
            if (!string.IsNullOrEmpty(it.label))
                PlacePill(Pill(it.id + "_Label", it.label, 11f, null, null, null, LabelHeight, ColumnWidth - 2f), new Vector2(cx, labelY));
            if (!string.IsNullOrEmpty(it.costText))
                PlacePill(Pill(it.id + "_Cost", it.costText, 12f, it.costIcon, null, null, CostHeight, 40f), new Vector2(cx, 0f));
        }
    }

    private static void PlacePill(RectTransform pill, Vector2 bottomCenter)
    {
        pill.anchorMin = pill.anchorMax = Vector2.zero;
        pill.pivot = new Vector2(0.5f, 0f);
        pill.anchoredPosition = bottomCenter;
    }

    /// <summary>
    /// Altın çerçeveli koyu hap: [simge] metin [bedel simgesi + bedel]. İçerik ortalanır; <paramref name="fixedWidth"/> verilirse hap o genişliktedir
    /// (uzun metin otomatik küçülür), yoksa içeriğe göre.
    /// </summary>
    private RectTransform Pill(string name, string text, float fontSize, Sprite leftIcon, string cost, Sprite costIcon, float h, float fixedWidth)
    {
        var g = ReferenceUiVisuals.Gold;
        var rim = ReferenceUiVisuals.Image(rect, name, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(ColumnWidth, h), new Color(g.r, g.g, g.b, 0.85f));
        rim.sprite = ReferenceUiVisuals.SolidPillSprite();
        rim.type = Image.Type.Sliced;
        rim.raycastTarget = false;
        var face = ReferenceUiVisuals.Image(rim.transform, "Face", Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f),
            new Color(0.10f, 0.07f, 0.04f, 0.92f));
        face.sprite = ReferenceUiVisuals.SolidPillSprite();
        face.type = Image.Type.Sliced;
        face.raycastTarget = false;
        float icon = h - 5f;
        var label = ReferenceUiVisuals.Text(rim.transform, "Text", text, fontSize, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, bodyBold: true);
        label.enableAutoSizing = false;
        label.fontSize = fontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        float textW = string.IsNullOrEmpty(text) ? 0f : label.GetPreferredValues(text, 400f, h).x;
        float costW = string.IsNullOrEmpty(cost) ? 0f : label.GetPreferredValues(cost, 400f, h).x;
        float content = textW + (leftIcon != null ? icon + 3f : 0f) + (costW > 0f ? 8f + (costIcon != null ? icon + 3f : 0f) + costW : 0f)
            + (costW <= 0f && costIcon != null ? icon + 3f : 0f);
        float w = fixedWidth > 0f ? fixedWidth : Mathf.Max(content + 18f, 40f);
        ((RectTransform)rim.transform).sizeDelta = new Vector2(w, h);
        float scale = fixedWidth > 0f && content + 8f > w ? (w - 8f) / content : 1f;
        float x = (w - content * scale) * 0.5f;
        float cy = (h - icon) * 0.5f;
        void Seg(string seg, Sprite sprite, string str, float segW)
        {
            if (sprite != null)
            {
                var ic = ReferenceUiVisuals.Image(rim.transform, seg + "Icon", Vector2.zero, Vector2.zero, new Vector2(x, cy), new Vector2(x + icon * scale, cy + icon), Color.white);
                ic.sprite = sprite; ic.preserveAspect = true; ic.raycastTarget = false;
                x += (icon + 3f) * scale;
            }
            if (str != null)
            {
                var t = seg == "" ? label : ReferenceUiVisuals.Text(rim.transform, seg + "Text", str, fontSize, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, bodyBold: true);
                t.enableAutoSizing = false; t.fontSize = fontSize * scale; t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
                t.alignment = TextAlignmentOptions.Center;
                var tr = t.rectTransform;
                tr.anchorMin = tr.anchorMax = Vector2.zero;
                tr.offsetMin = new Vector2(x, 0f); tr.offsetMax = new Vector2(x + segW * scale + 1f, h);
                x += segW * scale;
            }
        }
        Seg("", leftIcon, string.IsNullOrEmpty(text) ? null : text, textW);
        if (costW > 0f) { x += 8f * scale; Seg("Cost", costIcon, cost, costW); }
        else if (costIcon != null) Seg("Cost", costIcon, null, 0f);
        if (string.IsNullOrEmpty(text)) label.gameObject.SetActive(false);
        return (RectTransform)rim.transform;
    }

    private Button Round(string id, float centerX, float y, Sprite icon, UnityEngine.Events.UnityAction action)
    {
        var ring = ReferenceUiVisuals.Image(rect, id, Vector2.zero, Vector2.zero,
            new Vector2(centerX - ButtonSize * 0.5f, y), new Vector2(centerX + ButtonSize * 0.5f, y + ButtonSize), ReferenceUiVisuals.Gold);
        ring.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        ring.raycastTarget = true;
        var shadow = ReferenceUiVisuals.Image(ring.transform, "Shadow", Vector2.zero, Vector2.one, new Vector2(1f, -4f), new Vector2(-1f, -2f),
            new Color(0f, 0f, 0f, 0.35f));
        shadow.sprite = ring.sprite;
        shadow.raycastTarget = false;
        shadow.transform.SetAsFirstSibling();
        var face = ReferenceUiVisuals.Image(ring.transform, "Face", Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f),
            id == "yes" ? new Color(0.17f, 0.24f, 0.13f, 0.96f) : id == "no" ? new Color(0.25f, 0.12f, 0.10f, 0.96f) : new Color(0.13f, 0.09f, 0.06f, 0.96f));
        face.sprite = ring.sprite;
        face.raycastTarget = false;
        var picture = ReferenceUiVisuals.Image(ring.transform, "Icon", Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f), Color.white);
        picture.sprite = icon;
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        var button = ring.gameObject.AddComponent<Button>();
        button.targetGraphic = ring;
        button.transition = Selectable.Transition.None;
        ring.gameObject.AddComponent<ReferenceButtonPress>();
        button.onClick.AddListener(action);
        buttons[id] = button;
        return button;
    }

    private void OnPressed(Item item)
    {
        if (closed) return;
        if (!item.confirm)
        {
            Close();
            item.action?.Invoke();
            return;
        }
        // Onay satırı: "Silinsin mi? [simge] +2"  ✓ / ✗
        IsConfirming = true;
        var yes = new Item { id = "yes", icon = CampaignSceneSetup.Art("ui_icon_check"), label = L.T("EVET", "YES"), action = item.action };
        var no = new Item { id = "no", icon = CampaignSceneSetup.Art("ui_icon_cross"), label = L.T("VAZGEÇ", "CANCEL"), action = null };
        var row = new List<Item> { yes, no };
        Build(row, string.IsNullOrEmpty(item.question) ? L.T("Emin misin?", "Are you sure?") : item.question, item.costIcon, item.costText);
        // ✗: asıl menüye dön
        buttons["no"].onClick.RemoveAllListeners();
        buttons["no"].onClick.AddListener(() => { IsConfirming = false; Build(items, null, null, null); Follow(); });
        Follow();
        rect.localScale = Vector3.one * 0.85f;
        rect.DOScale(1f, 0.15f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
    }

    // ==================== VURGU ====================

    private void CreateHighlight()
    {
        if (target == null || target.sprite == null || target.color.a < 0.1f) return;
        var go = new GameObject("SelectHighlight");
        go.transform.SetParent(target.transform, false);
        highlight = go.AddComponent<SpriteRenderer>();
        SyncHighlight(1f);
    }

    private void SyncHighlight(float pulse)
    {
        if (highlight == null) return;
        if (target == null) { Destroy(highlight.gameObject); highlight = null; return; }
        highlight.sprite = target.sprite;
        highlight.flipX = target.flipX;
        highlight.flipY = target.flipY;
        highlight.sortingLayerID = target.sortingLayerID;
        highlight.sortingOrder = target.sortingOrder - 1;
        const float scale = 1.09f;
        highlight.transform.localScale = Vector3.one * scale;
        var c = target.sprite != null ? target.sprite.bounds.center : Vector3.zero;
        highlight.transform.localPosition = new Vector3(-c.x * (scale - 1f), -c.y * (scale - 1f), 0.01f);
        var g = ReferenceUiVisuals.Gold;
        highlight.color = new Color(g.r, g.g, g.b, Mathf.Lerp(0.55f, 0.95f, pulse));
    }

    // ==================== İZLEME ====================

    private void LateUpdate()
    {
        if (closed) return;
        if (target != null || highlight != null)
        {
            if (target == null) { Close(); return; }   // nesne yok oldu
            SyncHighlight(0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
        }
        Follow();
    }

    public void Follow()
    {
        if (bounds == null || parentRect == null || closed) return;
        var camera = cam != null ? cam : Camera.main;
        if (camera == null) return;
        var canvas = parentRect.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var area = parentRect.rect;
        const float TopPad = 150f, BottomPad = 150f, Edge = 12f, Gap = 10f;
        var b = bounds();
        Vector2 lo = Vector2.positiveInfinity, hi = Vector2.negativeInfinity;
        foreach (var corner in new[] { new Vector3(b.min.x, b.min.y), new Vector3(b.max.x, b.max.y), new Vector3(b.min.x, b.max.y), new Vector3(b.max.x, b.min.y) })
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, camera.WorldToScreenPoint(corner), uiCamera, out Vector2 p)) return;
            lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p);
        }
        float halfW = width * 0.5f;
        float minX = area.xMin + Edge + halfW, maxX = area.xMax - Edge - halfW;
        float minY = area.yMin + BottomPad, maxY = area.yMax - TopPad - height;
        float x = maxX < minX ? area.center.x : Mathf.Clamp((lo.x + hi.x) * 0.5f, minX, maxX);
        float y;
        if (hi.y + Gap <= maxY) { y = hi.y + Gap; Side = "above"; }
        else if (lo.y - Gap - height >= minY) { y = lo.y - Gap - height; Side = "below"; }
        else { y = Mathf.Clamp(hi.y + Gap, minY, Mathf.Max(minY, maxY)); Side = "above"; }
        rect.anchoredPosition = new Vector2(x - area.center.x, y - area.center.y);
    }
}
