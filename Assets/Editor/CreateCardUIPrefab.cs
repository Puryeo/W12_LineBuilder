using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CreateCardUIPrefab
{
    [MenuItem("TenTrix/Create Card UI Prefab")]
    public static void Create()
    {
        // Ensure folder
        const string prefabsFolder = "Assets/Prefabs/UI";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(prefabsFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }

        // Temporary root GO
        var root = new GameObject("CardUI_PrefabRoot");
        var rt = root.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120f, 160f);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Image component (required by CardUI)
        var img = root.AddComponent<Image>();
        img.color = Color.white;
        // leave sprite null so designer can assign later

        // Add CardUI script (must exist in project)
        var cardUi = root.AddComponent<CardUI>();
        // CardUI.Initialize will run at runtime; no runtime data set here.

        // Add optionally a CanvasGroup for better drag visuals
        root.AddComponent<CanvasGroup>();

        // Create a child for optional label (TextMeshPro)
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(root.transform, false);
        var labelRt = labelGO.AddComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(1f, 0.2f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.anchoredPosition = new Vector2(0f, 10f);

        // Use TextMeshProUGUI instead of legacy Text
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Card";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.color = Color.black;
        tmp.fontSize = 14;
        tmp.enableWordWrapping = false;
        tmp.margin = new Vector4(0, 0, 0, 0);

        // Save prefab
        var prefabPath = Path.Combine(prefabsFolder, "CardUI.prefab");
        prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        if (prefab != null)
        {
            Debug.Log($"[CreateCardUIPrefab] Created prefab at: {prefabPath}");
        }
        else
        {
            Debug.LogError("[CreateCardUIPrefab] Failed to create prefab.");
        }

        // cleanup temp
        Object.DestroyImmediate(root);

        // Focus the created prefab in Project window
        var created = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (created != null)
        {
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }
    }
}