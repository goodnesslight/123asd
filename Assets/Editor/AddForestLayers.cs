using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AddForestLayers
{
    const string Art = "Assets/Art/Forest/";
    const string Prefabs = "Assets/Prefabs/Forest/";

    [MenuItem("Best Stop/Add layered forest")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Import("StoreForeground.png"); Import("DistantForest.png"); Import("SpruceTrees.png");
        var stripTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "DistantForest.png");
        var strip = SaveSprite("DistantForest", stripTexture, new Rect(0, 0, stripTexture.width, stripTexture.height));
        var treeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "SpruceTrees.png");
        float[] left = { 0, 0.325f, 0.735f }, right = { 0.31f, 0.72f, 1 };
        string[] names = { "SpruceTall", "SpruceBroad", "SpruceYoung" };
        for (int i = 0; i < 3; i++)
        {
            Sprite sprite = SaveSprite(names[i], treeTexture, Bounds(treeTexture, left[i], right[i]));
            SavePrefab(names[i], sprite, i == 2 ? 400 : 510, -15);
        }
        SavePrefab("DistantForest", strip, 470, -24);

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BestStop.unity");
        var environment = GameObject.Find("Environment").transform;
        if (environment.Find("Forest") == null)
        {
            var forest = new GameObject("Forest").transform;
            forest.SetParent(environment, false);
            // Background remains a full-frame plate but with transparent sky/forest areas.
            var bg = environment.Find("Store background (no props)").GetComponent<SpriteRenderer>();
            Vector2 old = bg.sprite.bounds.size;
            bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "StoreForeground.png");
            Vector3 scale = bg.transform.localScale;
            bg.transform.localScale = new Vector3(scale.x * old.x / bg.sprite.bounds.size.x,
                scale.y * old.y / bg.sprite.bounds.size.y, scale.z);
            bg.sortingOrder = -10;
            CreateSky(forest);
            var far = Spawn("DistantForest", "Far forest", forest, 1024, 325, 680);
            far.localScale = new Vector3(2200 / strip.bounds.size.x, 680 / strip.bounds.size.y, 1);
            var middle = Spawn("DistantForest", "Middle forest", forest, 1000, 315, 590);
            middle.localScale = new Vector3(2300 / strip.bounds.size.x, 590 / strip.bounds.size.y, 1);
            var midRenderer = middle.GetComponent<SpriteRenderer>();
            midRenderer.flipX = true;
            midRenderer.color = new Color(0.75f, 0.9f, 0.83f, 1);
            midRenderer.sortingOrder = -22;
            var near = new GameObject("Near trees").transform;
            near.SetParent(forest, false);
            float[] xs = { 65, 205, 360, 520, 700, 860, 1060, 1250, 1440, 1630, 1850, 2000 };
            float[] heights = { 560, 430, 480, 390, 470, 410, 490, 430, 520, 410, 500, 440 };
            for (int i = 0; i < xs.Length; i++)
            {
                var tree = Spawn(names[i % 3], names[i % 3] + " " + (i + 1), near, xs[i], 327, heights[i]);
                tree.GetComponent<SpriteRenderer>().flipX = i % 2 == 1;
            }
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("FOREST_LAYERS_READY: two forest strips, three tree prefabs, twelve editable trees; other props preserved.");
    }

    static void Import(string name)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + name);
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
        settings.spritePivot = Vector2.zero;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    static Rect Bounds(Texture2D texture, float min, float max)
    {
        int l = texture.width, r = -1, b = texture.height, t = -1;
        var pixels = texture.GetPixels32();
        for (int y = 0; y < texture.height; y++)
        for (int x = (int)(min * texture.width); x < (int)(max * texture.width); x++)
            if (pixels[y * texture.width + x].a > 128)
            { l = Mathf.Min(l, x); r = Mathf.Max(r, x); b = Mathf.Min(b, y); t = Mathf.Max(t, y); }
        if (r < l) throw new Exception("Empty tree sprite");
        return new Rect(l, b, r - l + 1, t - b + 1);
    }

    static Sprite SaveSprite(string name, Texture2D texture, Rect rect)
    {
        string path = Art + name + ".asset";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0), 1);
        sprite.name = name;
        AssetDatabase.CreateAsset(sprite, path);
        return sprite;
    }

    static void SavePrefab(string name, Sprite sprite, float height, int order)
    {
        if (File.Exists(Prefabs + name + ".prefab")) return;
        var obj = new GameObject(name);
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        obj.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
        PrefabUtility.SaveAsPrefabAsset(obj, Prefabs + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(obj);
    }

    static Transform Spawn(string prefabName, string name, Transform parent, float x, float y, float height)
    {
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefabName + ".prefab"), parent);
        obj.name = name;
        obj.transform.position = new Vector3(x, y, 0);
        obj.transform.localScale = Vector3.one * (height / obj.GetComponent<SpriteRenderer>().sprite.bounds.size.y);
        return obj.transform;
    }

    static void CreateSky(Transform parent)
    {
        string path = Art + "SkyColor.asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, new Color32(0, 157, 240, 255));
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
        }
        var sky = new GameObject("Sky").AddComponent<SpriteRenderer>();
        sky.transform.SetParent(parent, false);
        sky.sprite = SaveSprite("Sky", texture, new Rect(0, 0, 1, 1));
        sky.sortingOrder = -40;
        sky.transform.position = new Vector3(1024, 0, 0);
        sky.transform.localScale = new Vector3(2048, 1152, 1);
    }

    public static void ApplyAndBuild() { Apply(); BuildBestStop.Build(); }

    [MenuItem("Best Stop/Match reference tree proportions")]
    public static void MatchReferenceProportions()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Import("ReferenceSpruce.png");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "ReferenceSpruce.png");
        var sprite = SaveSprite("ReferenceSpruce", texture, Bounds(texture, 0, 1));
        SavePrefab("ReferenceSpruce", sprite, 480, -15);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BestStop.unity");
        var forest = GameObject.Find("Environment").transform.Find("Forest");
        var near = forest.Find("Near trees");
        // Measurements relative to the shop facade, not relative to the screen height.
        float[] xs = { 90, 235, 390, 550, 660, 850, 1050, 1260, 1435, 1640, 1900, 2020 };
        float[] heights = { 480, 320, 435, 310, 380, 325, 310, 390, 430, 330, 375, 320 };
        for (int i = 0; i < Mathf.Min(near.childCount, xs.Length); i++)
        {
            var tree = near.GetChild(i);
            var renderer = tree.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.flipX = i % 2 == 1;
            tree.position = new Vector3(xs[i], 327, 0);
            tree.localScale = Vector3.one * (heights[i] / sprite.bounds.size.y);
        }
        string[] layerNames = { "Far forest", "Middle forest" };
        float[] layerHeights = { 355, 340 };
        for (int i = 0; i < 2; i++)
        {
            var layer = forest.Find(layerNames[i]);
            var renderer = layer.GetComponent<SpriteRenderer>();
            var scale = layer.localScale;
            scale.y = layerHeights[i] / renderer.sprite.bounds.size.y;
            layer.localScale = scale;
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("REFERENCE_PROPORTIONS_MATCHED: tree heights measured against facade; new reference-style sprite.");
        BuildBestStop.Build();
    }

    [MenuItem("Best Stop/Apply reference forest composition")]
    public static void RecomposeReference()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Import("ReferenceForest.png");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "ReferenceForest.png");
        var sprite = SaveSprite("ReferenceForest", texture, Bounds(texture, 0, 1));
        SavePrefab("ReferenceForest", sprite, 470, -24);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BestStop.unity");
        var forest = GameObject.Find("Environment").transform.Find("Forest");
        string[] layers = { "Far forest", "Middle forest" };
        float[] widths = { 2200, 2350 }, heights = { 470, 430 };
        for (int i = 0; i < layers.Length; i++)
        {
            var layer = forest.Find(layers[i]);
            var renderer = layer.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = i == 0 ? Color.white : new Color(0.86f, 0.94f, 0.9f, 1);
            renderer.flipX = i == 1;
            layer.position = new Vector3(i == 0 ? 1024 : 990, 325, 0);
            layer.localScale = new Vector3(widths[i] / sprite.bounds.size.x, heights[i] / sprite.bounds.size.y, 1);
        }
        // Tall edge framing and a few roofline accents, following the user's reference.
        float[] xs = { 70, 190, 380, 540, 680, 850, 1060, 1240, 1430, 1620, 1870, 2010 };
        float[] treeHeights = { 760, 475, 650, 455, 550, 435, 430, 600, 670, 460, 620, 480 };
        var near = forest.Find("Near trees");
        for (int i = 0; i < Mathf.Min(near.childCount, xs.Length); i++)
        {
            var tree = near.GetChild(i);
            tree.position = new Vector3(xs[i], 327, 0);
            var renderer = tree.GetComponent<SpriteRenderer>();
            tree.localScale = Vector3.one * (treeHeights[i] / renderer.sprite.bounds.size.y);
            renderer.color = new Color(0.9f, 0.96f, 0.91f, 1);
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("REFERENCE_FOREST_READY: even dense treeline, new texture, edge framing; props unchanged.");
        BuildBestStop.Build();
    }

    [MenuItem("Best Stop/Improve distant forest visibility")]
    public static void ImproveVisibility()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BestStop.unity");
        var forest = GameObject.Find("Environment").transform.Find("Forest");
        string[] names = { "Far forest", "Middle forest" };
        float[] heights = { 680, 590 };
        for (int i = 0; i < names.Length; i++)
        {
            var layer = forest.Find(names[i]);
            var renderer = layer.GetComponent<SpriteRenderer>();
            Vector3 scale = layer.localScale;
            scale.y = heights[i] / renderer.sprite.bounds.size.y;
            layer.localScale = scale;
            renderer.enabled = true;
            layer.gameObject.SetActive(true);
        }
        EditorSceneManager.SaveScene(scene);
        Debug.Log("DISTANT_FOREST_VISIBILITY_FIXED");
        BuildBestStop.Build();
    }
}
