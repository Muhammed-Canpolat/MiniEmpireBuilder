using UnityEngine;

namespace MiniEmpire.UI
{
    /// <summary>
    /// One entry of the bottom menu. Create new ones with  Assets > Create > MiniEmpire > Menu Item,
    /// drop it into BottomMenuBar.items and it appears in the bar. Change icon / label here without touching the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "MenuItem_", menuName = "MiniEmpire/Menu Item")]
    public class MenuItemData : ScriptableObject
    {
        [Tooltip("Stable id your code listens for, e.g. \"build\", \"expedition\", \"hero\", \"island\"")]
        public string id = "new_item";
        public Sprite icon;
        public string labelTR = "YENİ";
        public string labelEN = "NEW";
        [Tooltip("Lower comes first (left)")]
        public int order = 0;
        public bool visible = true;
        [Tooltip("Shown but greyed-out and not clickable (e.g. unlocks at level 5)")]
        public bool locked = false;

        public string Label(bool turkish) => turkish ? labelTR : labelEN;
    }
}
