using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MiniEmpire.UI.EditorTools
{
    /// <summary>
    /// Tools > MiniEmpire UI > Build Top Bar / Build Bottom Bar
    /// Creates the full, already-wired hierarchy in the open scene. Run once, then tweak freely.
    /// Layout units assume CanvasScaler reference 886 x 1774, Match = 0 (width).
    /// </summary>
    public static class MiniEmpireUIBuilder
    {
        static readonly Color32 OutlineColor = new Color32(40, 22, 10, 255);

        // ------------------------------------------------------------------ menu
        [MenuItem("Tools/MiniEmpire UI/Build Top Bar")]
        public static void BuildTopBar()
        {
            if (!CheckFont()) return;
            var parent = SafeAreaRoot();
            var root = NewUI("TopBar", parent);
            SetAnchors(root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(0, 150);
            var ctrl = root.gameObject.AddComponent<TopBarController>();

            // background plank (9-sliced, stretches with screen width)
            var bg = NewUI("Bg", root);
            SetAnchors(bg, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            bg.offsetMin = new Vector2(10, 0); bg.offsetMax = new Vector2(-10, 0);
            bg.anchoredPosition = new Vector2(0, -22); bg.sizeDelta = new Vector2(-20, 98);
            Img(bg, S("topbar_bg"), true);

            // left resources
            var left = Group("LeftGroup", root, new Vector2(0, 1), new Vector2(0.5f, 1), 22, -91, 2);
            ctrl.counters.Add(Pill("gold", left, S("icon_gold"), "2.581"));
            ctrl.counters.Add(Pill("wood", left, S("icon_wood"), "227"));
            ctrl.counters.Add(Pill("meat", left, S("icon_meat"), "43"));

            // centre: banners + shield
            var bl = NewUI("BannerLeft", root);
            SetAnchors(bl, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(1, 1));
            Img(bl, S("banner_left"), false); bl.anchoredPosition = new Vector2(-51, -4);
            var br = NewUI("BannerRight", root);
            SetAnchors(br, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1));
            Img(br, S("banner_right"), false); br.anchoredPosition = new Vector2(51, -4);

            var sh = NewUI("LevelBadge", root);
            SetAnchors(sh, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            var shImg = Img(sh, S("level_shield_empty"), false); sh.anchoredPosition = new Vector2(0, -2);
            var lvl = Txt(NewUI("Level", sh), "7", 54, TextAlignmentOptions.Center);
            Place(lvl.rectTransform, new Vector2(0, 12), new Vector2(100, 64));
            var cap = Txt(NewUI("Caption", sh), "ANA ÜS", 16, TextAlignmentOptions.Center);
            Place(cap.rectTransform, new Vector2(0, -37), new Vector2(96, 22));
            var badge = sh.gameObject.AddComponent<LevelBadge>();
            badge.shield = shImg; badge.levelText = lvl; badge.captionText = cap;
            ctrl.levelBadge = badge;

            // right resources
            var right = Group("RightGroup", root, new Vector2(0.5f, 1), new Vector2(1, 1), 97, -101, 4);
            ctrl.counters.Add(Pill("gem", right, S("icon_gem"), "8"));
            ctrl.counters.Add(Pill("builder", right, S("icon_builder"), "2/2"));

            // settings button (ring + gear are separate, gear can be swapped)
            var st = NewUI("SettingsButton", root);
            SetAnchors(st, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            var stImg = Img(st, S("btn_round_empty"), false); stImg.raycastTarget = true;
            st.anchoredPosition = new Vector2(-14, -30);
            var gear = NewUI("Icon", st); Img(gear, S("icon_gear"), false); gear.anchoredPosition = Vector2.zero;
            var btn = st.gameObject.AddComponent<Button>(); btn.targetGraphic = stImg;
            ctrl.settingsButton = btn;

            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root.gameObject);
        }

        [MenuItem("Tools/MiniEmpire UI/Build Bottom Bar")]
        public static void BuildBottomBar()
        {
            if (!CheckFont()) return;
            var parent = SafeAreaRoot();
            var root = NewUI("BottomBar", parent);
            SetAnchors(root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            root.anchoredPosition = Vector2.zero; root.sizeDelta = new Vector2(0, 240);
            var bar = root.gameObject.AddComponent<BottomMenuBar>();

            var panel = NewUI("SlidingPanel", root);
            SetAnchors(panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            var bg = NewUI("Bg", panel);
            SetAnchors(bg, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            bg.offsetMin = new Vector2(10, 0); bg.offsetMax = new Vector2(-10, 0);
            bg.anchoredPosition = new Vector2(0, 4); bg.sizeDelta = new Vector2(-20, 178);
            var bgImg = Img(bg, S("bottombar_bg"), true); bgImg.raycastTarget = true; // blocks taps to the map

            var row = NewUI("ButtonRow", panel);
            SetAnchors(row, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            row.offsetMin = new Vector2(39, 0); row.offsetMax = new Vector2(-38, 0);
            row.anchoredPosition = new Vector2(0.5f, 9); row.sizeDelta = new Vector2(-77, 160);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = -3; hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true; hl.childControlHeight = false;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

            // separator template
            var sep = NewUI("SeparatorTemplate", row);
            Img(sep, S("separator_banner"), true);
            sep.sizeDelta = new Vector2(31, 120);
            var sle = sep.gameObject.AddComponent<LayoutElement>();
            sle.minWidth = 24; sle.preferredWidth = 31; sle.flexibleWidth = 0;
            sep.gameObject.SetActive(false);

            // button template
            var b = NewUI("ButtonTemplate", row);
            b.sizeDelta = new Vector2(182, 160);
            var frame = Img(b, S("menu_button_frame_noplate"), true); frame.raycastTarget = true;
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 110; le.preferredWidth = 182; le.flexibleWidth = 0;
            var button = b.gameObject.AddComponent<Button>(); button.targetGraphic = frame;
            var mb = b.gameObject.AddComponent<MenuButton>();

            var icon = NewUI("Icon", b);
            SetAnchors(icon, new Vector2(0.12f, 0.375f), new Vector2(0.88f, 0.95f), new Vector2(0.5f, 0.5f));
            icon.offsetMin = icon.offsetMax = Vector2.zero;
            var iconImg = Img(icon, S("icon_build"), false, false); iconImg.preserveAspect = true;

            var plate = NewUI("LabelPlate", b);
            SetAnchors(plate, new Vector2(0.143f, 0.125f), new Vector2(0.857f, 0.369f), new Vector2(0.5f, 0.5f));
            plate.offsetMin = plate.offsetMax = Vector2.zero;
            var plateImg = Img(plate, S("menu_label_plate"), true);

            var label = Txt(NewUI("Label", plate), "İNŞA ET", 24, TextAlignmentOptions.Center);
            SetAnchors(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            label.rectTransform.offsetMin = new Vector2(6, 2); label.rectTransform.offsetMax = new Vector2(-6, -2);
            label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 24;

            mb.frame = frame; mb.icon = iconImg; mb.labelPlate = plateImg; mb.label = label;
            b.gameObject.SetActive(false);

            // arrow tab (collapse / expand)
            var arrow = NewUI("ArrowToggle", panel);
            SetAnchors(arrow, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            var arrowImg = Img(arrow, S("btn_arrow_down"), false); arrowImg.raycastTarget = true;
            arrow.anchoredPosition = new Vector2(0, 153);
            var arrowBtn = arrow.gameObject.AddComponent<Button>(); arrowBtn.targetGraphic = arrowImg;

            bar.buttonTemplate = mb; bar.separatorTemplate = sep.gameObject; bar.buttonRow = row;
            bar.slidingPanel = panel; bar.toggleButton = arrowBtn; bar.toggleImage = arrowImg;
            bar.arrowDown = S("btn_arrow_down"); bar.arrowUp = S("btn_arrow_up");
            bar.collapseDistance = 150;

            bar.items.Add(Item("build", "icon_build", "İNŞA ET", "BUILD", 0));
            bar.items.Add(Item("expedition", "icon_expedition", "SEFER", "EXPEDITION", 1));
            bar.items.Add(Item("hero", "icon_hero", "KAHRAMAN", "HERO", 2));
            bar.items.Add(Item("island", "icon_island", "ADA", "ISLAND", 3));

            EditorUtility.SetDirty(bar);
            Selection.activeGameObject = root.gameObject;
        }

        // ------------------------------------------------------------------ pieces
        static TopBarController.Entry Pill(string id, RectTransform parent, Sprite icon, string sample)
        {
            var p = NewUI("Res_" + id, parent);
            p.sizeDelta = new Vector2(110, 68);
            Img(p, S("res_pill_bg"), true);
            var hl = p.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(63, 14, 0, 0);
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            var le = p.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 96; le.minHeight = 68; le.preferredHeight = 68;

            var ic = NewUI("Icon", p);
            ic.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            SetAnchors(ic, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f));
            var icImg = Img(ic, icon, false, false); icImg.preserveAspect = true;
            ic.sizeDelta = new Vector2(46, 46); ic.anchoredPosition = new Vector2(31, 1);

            var t = Txt(NewUI("Value", p), sample, 26, TextAlignmentOptions.MidlineLeft);

            var rc = p.gameObject.AddComponent<ResourceCounter>();
            rc.icon = icImg; rc.valueText = t;
            if (id == "builder") rc.style = NumberStyle.Full;
            return new TopBarController.Entry { id = id, counter = rc };
        }

        static RectTransform Group(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, float left, float right, float spacing)
        {
            var g = NewUI(name, parent);
            SetAnchors(g, aMin, aMax, new Vector2(0, 1));
            g.offsetMin = new Vector2(left, 0); g.offsetMax = new Vector2(right, 0);
            g.anchoredPosition = new Vector2(g.anchoredPosition.x, -38);
            g.sizeDelta = new Vector2(g.sizeDelta.x, 68);
            var hl = g.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            return g;
        }

        static MenuItemData Item(string id, string icon, string tr, string en, int order)
        {
            string dir = Root() + "/MenuItems";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root(), "MenuItems");
            string path = dir + "/MenuItem_" + id + ".asset";
            var d = AssetDatabase.LoadAssetAtPath<MenuItemData>(path);
            if (d != null) return d;
            d = ScriptableObject.CreateInstance<MenuItemData>();
            d.id = id; d.icon = S(icon); d.labelTR = tr; d.labelEN = en; d.order = order;
            AssetDatabase.CreateAsset(d, path);
            AssetDatabase.SaveAssets();
            return d;
        }

        // ------------------------------------------------------------------ helpers
        static RectTransform SafeAreaRoot()
        {
            Canvas canvas = null;
            if (Selection.activeGameObject != null) canvas = Selection.activeGameObject.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAny<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
                go.layer = LayerMask.NameToLayer("UI");
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var sc = go.GetComponent<CanvasScaler>();
                sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(886, 1774);
                sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                sc.matchWidthOrHeight = 0f;
            }
            if (FindAny<UnityEngine.EventSystems.EventSystem>() == null)
                EditorApplication.ExecuteMenuItem("GameObject/UI/Event System");

            var existing = canvas.transform.Find("SafeArea");
            if (existing != null) return (RectTransform)existing;
            var sa = NewUI("SafeArea", canvas.transform);
            SetAnchors(sa, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            sa.offsetMin = sa.offsetMax = Vector2.zero;
            sa.gameObject.AddComponent<SafeArea>();
            return sa;
        }

        static T FindAny<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "MiniEmpire UI");
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            SetAnchors(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static Image Img(RectTransform rt, Sprite s, bool sliced, bool nativeSize = true)
        {
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = s;
            im.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            im.raycastTarget = false;
            if (!sliced && nativeSize && s != null) im.SetNativeSize();
            return im;
        }

        static TextMeshProUGUI Txt(RectTransform rt, string text, float size, TextAlignmentOptions align)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.color = Color.white;
            t.alignment = align;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_2_OR_NEWER
            t.textWrappingMode = TextWrappingModes.NoWrap;
#else
            t.enableWordWrapping = false;
#endif
            var mat = OutlineMaterial(t.font);
            if (mat != null) t.fontSharedMaterial = mat;
            return t;
        }

        static Material OutlineMaterial(TMP_FontAsset font)
        {
            if (font == null) return null;
            string path = Root() + "/MiniEmpire_TextOutline_" + font.name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(font.material);
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.22f);
            m.SetColor("_OutlineColor", OutlineColor);
            m.SetFloat("_FaceDilate", 0.15f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static bool CheckFont()
        {
            if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null) return true;
            EditorUtility.DisplayDialog("MiniEmpire UI",
                "TextMeshPro default font not found.\nWindow > TextMeshPro > Import TMP Essential Resources, then run again.", "OK");
            return false;
        }

        static string rootCache;
        static string Root()
        {
            if (!string.IsNullOrEmpty(rootCache) && AssetDatabase.IsValidFolder(rootCache)) return rootCache;
            foreach (var g in AssetDatabase.FindAssets("topbar_bg t:Sprite"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g).Replace('\\', '/');
                int i = p.IndexOf("/MiniEmpireUI/");
                if (i >= 0) return rootCache = p.Substring(0, i + "/MiniEmpireUI".Length);
            }
            return rootCache = "Assets";
        }

        static Sprite S(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g).Replace('\\', '/');
                if (p.Contains("/MiniEmpireUI/") && Path.GetFileNameWithoutExtension(p) == name)
                    return AssetDatabase.LoadAssetAtPath<Sprite>(p);
            }
            Debug.LogWarning("MiniEmpire UI: sprite not found: " + name + " (is the PNG imported as Sprite? Tools > MiniEmpire UI > Reapply Sprite Import Settings)");
            return null;
        }
    }
}
