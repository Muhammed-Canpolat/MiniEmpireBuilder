using UnityEngine;

[CreateAssetMenu(menuName = "Mini Empire/Decor")]
public sealed class DecorData : ScriptableObject
{
    public string id;
    public string displayName;
    public Vector2Int footprint = Vector2Int.one;
    public int cost;
    public int unlockMainBaseLevel = 1;
    public Sprite sprite;
    public bool animated;
    public RuntimeAnimatorController animatorController;
}
