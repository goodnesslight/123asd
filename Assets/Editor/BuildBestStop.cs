using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using BestStop;

public static class BuildBestStop
{
    [MenuItem("Best Stop/Build Windows (keep scene layout)")]
    public static void Build()
    {
        foreach (string name in new[] { "BestStopFlatProps", "Cloud", "Traveler", "TravelerBack" })
        {
            string path = "Assets/Resources/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
        // Build the saved scene as authored. Never recreate it or reset prop transforms.
        if (!File.Exists("Assets/Scenes/BestStop.unity"))
            throw new Exception("Open Best Stop/Prepare editable scene first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/BestStop.unity", true) };
        PlayerSettings.companyName = "Forest Road";
        PlayerSettings.productName = "Best Stop";
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Builds/Windows");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/BestStop.unity" },
            locationPathName = "Builds/Windows/BestStop.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Build failed: " + report.summary.result);
        Debug.Log("BEST_STOP_BUILD_PASS");
    }
}
