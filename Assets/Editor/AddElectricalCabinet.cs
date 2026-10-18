using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BestStop;

public static class AddElectricalCabinet
{
    const string PrefabPath = "Assets/Prefabs/Props/ElectricalCabinet.prefab";
    const string BackgroundPath = "Assets/Art/StoreBackgroundNoCabinet.png";

    [MenuItem("Best Stop/Add editable electrical cabinet")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Import(BackgroundPath, Vector2.zero);
        const string texturePath = "Assets/Art/ElectricalCabinet.png";
        Import(texturePath, new Vector2(0.5f, 0));
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        const string spritePath = "Assets/Art/Sprites/ElectricalCabinet.asset";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            int left = texture.width, right = -1, bottom = texture.height, top = -1;
            var pixels = texture.GetPixels32();
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
                if (pixels[y * texture.width + x].a > 64)
                {
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
            if (right < left) throw new Exception("Empty cabinet sprite");
            sprite = Sprite.Create(texture, new Rect(left, bottom, right - left + 1, top - bottom + 1), new Vector2(0.5f, 0), 1);
            sprite.name = "ElectricalCabinet";
            AssetDatabase.CreateAsset(sprite, spritePath);
        }
        if (!File.Exists(PrefabPath))
        {
            var cabinet = new GameObject("ElectricalCabinet");
            cabinet.AddComponent<SpriteRenderer>().sprite = sprite;
            cabinet.AddComponent<GroundSort>();
            cabinet.transform.localScale = Vector3.one * (150f / sprite.bounds.size.y);
            PrefabUtility.SaveAsPrefabAsset(cabinet, PrefabPath);
            UnityEngine.Object.DestroyImmediate(cabinet);
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BestStop.unity");
        var environment = GameObject.Find("Environment");
        if (environment == null) throw new Exception("Editable Environment is missing");
        var background = environment.transform.Find("Store background (no props)").GetComponent<SpriteRenderer>();
        // A background swap is a one-time migration. Subsequent calls preserve deletions.
        if (AssetDatabase.GetAssetPath(background.sprite) != BackgroundPath)
        {
            Vector2 oldSize = background.sprite.bounds.size;
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            Vector3 scale = background.transform.localScale;
            background.transform.localScale = new Vector3(scale.x * oldSize.x / background.sprite.bounds.size.x,
                scale.y * oldSize.y / background.sprite.bounds.size.y, scale.z);
            var props = environment.transform.Find("Props");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, props);
            instance.transform.position = new Vector3(1752, 326, 0);
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("ELECTRICAL_CABINET_READY: prefab and clean background; existing prop transforms preserved.");
    }

    static void Import(string path, Vector2 pivot)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 1;
        importer.isReadable = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    public static void ApplyAndBuild()
    {
        Apply();
        BuildBestStop.Build();
    }
}
