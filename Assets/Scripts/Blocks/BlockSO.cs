using UnityEngine;

[CreateAssetMenu(menuName = "TenTrix/Block", fileName = "BlockSO")]
public class BlockSO : ScriptableObject
{
    [Header("Visuals")]
    public Color Color = Color.white;
    [Tooltip("카드 UI 등에서 사용되는 프리뷰 스프라이트")]
    public Sprite previewSprite;

    [Header("Shape Offsets (local grid coords)")]
    public Vector2Int[] ShapeOffsets; // local offsets from origin (0,0)

    // Optional: rotation not required in PRD, but can be extended later.
}
