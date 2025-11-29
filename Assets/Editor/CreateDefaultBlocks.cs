#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class CreateDefaultBlocks
{
    [MenuItem("TenTrix/Create Default Block Assets")]
    public static void Create()
    {
        string folder = "Assets/Blocks";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "Blocks");
        }

        CreateBlock("Block_1x1", new Vector2Int[] { new Vector2Int(0,0) }, new Color(1f,1f,1f));
        // I
        CreateBlock("Block_I", new Vector2Int[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(3,0) }, new Color(0.2f,0.8f,1f));
        // O
        CreateBlock("Block_O", new Vector2Int[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1) }, new Color(1f,1f,0.2f));
        // T
        CreateBlock("Block_T", new Vector2Int[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(1,1) }, new Color(0.8f,0.2f,1f));
        // S
        CreateBlock("Block_S", new Vector2Int[] { new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(0,1), new Vector2Int(1,1) }, new Color(0.2f,1f,0.2f));
        // Z
        CreateBlock("Block_Z", new Vector2Int[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(2,1) }, new Color(1f,0.2f,0.2f));
        // J
        CreateBlock("Block_J", new Vector2Int[] { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1) }, new Color(0.2f,0.2f,1f));
        // L
        CreateBlock("Block_L", new Vector2Int[] { new Vector2Int(2,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1) }, new Color(1f,0.5f,0.2f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Default Block assets created under Assets/Blocks");

        static void CreateBlock(string name, Vector2Int[] shape, Color color)
        {
            var asset = ScriptableObject.CreateInstance<BlockSO>();
            asset.ShapeOffsets = shape;
            asset.Color = color;
            var path = Path.Combine("Assets/Blocks", name + ".asset");
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
#endif
