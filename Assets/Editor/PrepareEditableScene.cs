using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BestStop;

public static class PrepareEditableScene
{
    const string ScenePath = "Assets/Scenes/BestStop.unity";
    static readonly string[] Names = { "PhoneBooth", "IceMachine", "TrashBin", "YellowBollard" };

    [MenuItem("Best Stop/Prepare editable scene")]
    public static void Prepare()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ConfigureSprites();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        // Idempotent: existing authored instances, transforms and deletions are respected.
        if (GameObject.Find("Environment") == null)
        {
            var environment = new GameObject("Environment");
            var backdrop = new GameObject("Store background (no props)").AddComponent<SpriteRenderer>();
            backdrop.transform.SetParent(environment.transform, false);
            backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/StoreBackground.png");
            backdrop.sortingOrder = -10;
            backdrop.transform.localScale = new Vector3(BestStopGame.Width / backdrop.sprite.rect.width,
                BestStopGame.Height / backdrop.sprite.rect.height, 1);
            var props = new GameObject("Props").transform;
            props.SetParent(environment.transform, false);
            AddProp(0, new Vector2(299, 326), props);
            AddProp(1, new Vector2(406, 326), props);
            AddProp(2, new Vector2(506, 322), props);
            foreach (float x in new[] { 643f, 764f, 1259f, 1370f, 1490f })
                AddProp(3, new Vector2(x, 318), props);
        }
        if (Camera.main == null)
        {
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = BestStopGame.Height / 2;
            camera.transform.position = new Vector3(BestStopGame.Width / 2, BestStopGame.Height / 2, -10);
            camera.backgroundColor = new Color32(9, 22, 30, 255);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        if (!Application.isBatchMode && SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.in2DMode = true;
            SceneView.lastActiveSceneView.LookAt(new Vector3(1024, 576, 0), Quaternion.identity, 650, true);
        }
        Debug.Log("EDITABLE_PROPS_READY: 4 prefabs; scene layout preserved on subsequent setup/build.");
    }

    static void ConfigureSprites()
    {
        var bg = (TextureImporter)AssetImporter.GetAtPath("Assets/Art/StoreBackground.png");
        SetCommon(bg);
        bg.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings();
        bg.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = Vector2.zero;
        bg.SetTextureSettings(settings);
        bg.SaveAndReimport();

        string path = "Assets/Art/Props.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        SetCommon(importer);
        importer.SaveAndReimport();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        // Source atlas has generous margins. Find opaque bounds inside each known cell.
        float[] starts = { 0.04f, 0.30f, 0.59f, 0.83f };
        float[] ends = { 0.26f, 0.54f, 0.78f, 0.97f };
        var sprites = new SpriteMetaData[4];
        Color32[] pixels = texture.GetPixels32();
        for (int i = 0; i < 4; i++)
        {
            int left = texture.width, right = -1, bottom = texture.height, top = -1;
            for (int y = 0; y < texture.height; y++)
            for (int x = (int)(starts[i] * texture.width); x < (int)(ends[i] * texture.width); x++)
                if (pixels[y * texture.width + x].a > 64)
                {
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
            if (right < left) throw new Exception("Empty prop sprite: " + Names[i]);
            sprites[i] = new SpriteMetaData { name = Names[i], rect = new Rect(left, bottom, right - left + 1, top - bottom + 1),
                alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 0) };
        }
        // Persist each sliced sprite as its own asset (no Sprite Editor package required).
        Directory.CreateDirectory("Assets/Art/Sprites");
        var importedSprites = new Sprite[4];
        for (int i = 0; i < 4; i++)
        {
            string spritePath = "Assets/Art/Sprites/" + Names[i] + ".asset";
            importedSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (importedSprites[i] == null)
            {
                importedSprites[i] = Sprite.Create(texture, sprites[i].rect, new Vector2(0.5f, 0), 1);
                importedSprites[i].name = Names[i];
                AssetDatabase.CreateAsset(importedSprites[i], spritePath);
            }
        }
        float[] heights = { 158, 130, 74, 70 };
        Directory.CreateDirectory("Assets/Prefabs/Props");
        for (int i = 0; i < 4; i++)
        {
            string prefabPath = "Assets/Prefabs/Props/" + Names[i] + ".prefab";
            if (File.Exists(prefabPath)) continue;
            var prop = new GameObject(Names[i]);
            var renderer = prop.AddComponent<SpriteRenderer>();
            renderer.sprite = importedSprites.Single(s => s.name == Names[i]);
            prop.transform.localScale = Vector3.one * (heights[i] / renderer.sprite.bounds.size.y);
            prop.AddComponent<GroundSort>();
            PrefabUtility.SaveAsPrefabAsset(prop, prefabPath);
            UnityEngine.Object.DestroyImmediate(prop);
        }
    }

    static void SetCommon(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 1;
        importer.isReadable = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.npotScale = TextureImporterNPOTScale.None;
    }

    static void AddProp(int type, Vector2 position, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/" + Names[type] + ".prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.position = position;
    }

    public static void PrepareAndBuild()
    {
        Prepare();
        BuildBestStop.Build();
    }
}
