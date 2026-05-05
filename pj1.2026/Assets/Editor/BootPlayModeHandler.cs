using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

[UnityEditor.InitializeOnLoadAttribute]
public static class BootPlayModeStarter
{
    const string EditorPrefsKey = "BootPlayModeStarter_OriginalScenes";
    const string EditorPrefsBuildKey = "BootPlayModeStarter_OriginalBuildSettings";
    const string EditorPrefsFlag = "BootPlayModeStarter_Active";
    const string EditorPrefsBuildFlag = "BootPlayModeStarter_BuildModified";

    static BootPlayModeStarter()
    {
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        switch (state)
        {
            case UnityEditor.PlayModeStateChange.ExitingEditMode:
                TryPrepareForPlay();
                break;
            case UnityEditor.PlayModeStateChange.EnteredPlayMode:
                // Delay the runtime scene loads until the play mode has fully started
                UnityEditor.EditorApplication.delayCall += OnEnteredPlayMode;
                break;
            case UnityEditor.PlayModeStateChange.ExitingPlayMode:
            case UnityEditor.PlayModeStateChange.EnteredEditMode:
                Cleanup();
                break;
        }
    }

    static void TryPrepareForPlay()
    {
        // Record currently open scenes in the Editor so we can restore them in PlayMode
        var sceneCount = UnityEditor.SceneManagement.EditorSceneManager.sceneCount;
        var paths = new List<string>();
        for (int i = 0; i < sceneCount; i++)
        {
            var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
            if (!string.IsNullOrEmpty(s.path))
                paths.Add(s.path);
        }

        var wrapper = new SceneList { paths = paths.ToArray() };
        var json = JsonUtility.ToJson(wrapper);
        UnityEditor.EditorPrefs.SetString(EditorPrefsKey, json);

        // Save original EditorBuildSettings.scenes so we can restore them later if we need to modify them
        var originalBuild = UnityEditor.EditorBuildSettings.scenes;
        var buildWrapper = new BuildSceneList { scenes = new BuildScene[originalBuild.Length] };
        for (int i = 0; i < originalBuild.Length; i++)
        {
            buildWrapper.scenes[i] = new BuildScene { path = originalBuild[i].path, enabled = originalBuild[i].enabled };
        }
        UnityEditor.EditorPrefs.SetString(EditorPrefsBuildKey, JsonUtility.ToJson(buildWrapper));

        // Find a scene asset named exactly "_Boot" (case-insensitive)
        string bootPath = null;
        var guids = UnityEditor.AssetDatabase.FindAssets("t:Scene");
        foreach (var guid in guids)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(name, "_Boot", StringComparison.OrdinalIgnoreCase))
            {
                bootPath = path;
                break;
            }
        }

        if (bootPath != null)
        {
            var sceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(bootPath);
            if (sceneAsset != null)
            {
                UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = sceneAsset;
                UnityEditor.EditorPrefs.SetInt(EditorPrefsFlag, 1);
                // Ensure recorded scenes are present in Build Settings so they can be loaded at runtime by name
                TryEnsureScenesInBuildSettings(paths);
                UnityEditor.EditorPrefs.SetInt(EditorPrefsBuildFlag, 1);
                Debug.Log("[BootPlayModeStarter] Set playModeStartScene to _Boot and recorded open scenes.");
                return;
            }
        }

        Debug.LogWarning("[BootPlayModeStarter] Could not find a scene named '_Boot' in the project. PlayMode will proceed normally.");
    }

    static void OnEnteredPlayMode()
    {
        // Only proceed if we had set playModeStartScene during the last ExitingEditMode
        if (UnityEditor.EditorPrefs.GetInt(EditorPrefsFlag, 0) == 0)
            return;

        var json = UnityEditor.EditorPrefs.GetString(EditorPrefsKey, "");
        if (string.IsNullOrEmpty(json))
            return;

        var wrapper = JsonUtility.FromJson<SceneList>(json);
        if (wrapper?.paths == null || wrapper.paths.Length == 0)
            return;

        // Load recorded scenes into Play Mode.
        // Load the first valid recorded scene in Single mode to replace the _Boot scene,
        // then load the remaining recorded scenes additively. This avoids trying to
        // unload the last loaded scene (which is unsupported).
        bool loadedAny = false;
        for (int i = 0; i < wrapper.paths.Length; i++)
        {
            var path = wrapper.paths[i];
            try
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name))
                    continue;

                // Avoid reloading _Boot itself
                if (string.Equals(name, "_Boot", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!loadedAny)
                {
                    // Replace the currently loaded _Boot scene
                    UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(name, UnityEngine.SceneManagement.LoadSceneMode.Single);
                    loadedAny = true;
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(name, UnityEngine.SceneManagement.LoadSceneMode.Additive);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarningFormat("[BootPlayModeStarter] Failed to load scene {0}: {1}", path, ex.Message);
            }
        }

        // Reset the playModeStartScene and cleanup stored data so future Play presses behave normally
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
        UnityEditor.EditorPrefs.DeleteKey(EditorPrefsKey);
        // if we modified build settings, restore original list
        if (UnityEditor.EditorPrefs.GetInt(EditorPrefsBuildFlag, 0) != 0)
        {
            RestoreOriginalBuildSettings();
            UnityEditor.EditorPrefs.DeleteKey(EditorPrefsBuildFlag);
            UnityEditor.EditorPrefs.DeleteKey(EditorPrefsBuildKey);
        }
        UnityEditor.EditorPrefs.DeleteKey(EditorPrefsFlag);
        Debug.Log("[BootPlayModeStarter] Loaded original scenes additively and unloaded _Boot.");
    }

    static void Cleanup()
    {
        if (UnityEditor.EditorPrefs.GetInt(EditorPrefsFlag, 0) != 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
            UnityEditor.EditorPrefs.DeleteKey(EditorPrefsKey);
            UnityEditor.EditorPrefs.DeleteKey(EditorPrefsFlag);
            // restore build settings if we modified them
            if (UnityEditor.EditorPrefs.GetInt(EditorPrefsBuildFlag, 0) != 0)
            {
                RestoreOriginalBuildSettings();
                UnityEditor.EditorPrefs.DeleteKey(EditorPrefsBuildFlag);
                UnityEditor.EditorPrefs.DeleteKey(EditorPrefsBuildKey);
            }
            Debug.Log("[BootPlayModeStarter] Cleanup after PlayMode ended or canceled.");
        }
    }

    static void TryEnsureScenesInBuildSettings(List<string> scenePaths)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var buildScenes = UnityEditor.EditorBuildSettings.scenes;
        foreach (var bs in buildScenes)
            existing.Add(bs.path);

        var modified = false;
        var newList = new List<UnityEditor.EditorBuildSettingsScene>(buildScenes);
        foreach (var p in scenePaths)
        {
            if (string.IsNullOrEmpty(p)) continue;
            if (!existing.Contains(p))
            {
                newList.Add(new UnityEditor.EditorBuildSettingsScene(p, true));
                modified = true;
            }
        }

        if (modified)
            UnityEditor.EditorBuildSettings.scenes = newList.ToArray();
    }

    static void RestoreOriginalBuildSettings()
    {
        var json = UnityEditor.EditorPrefs.GetString(EditorPrefsBuildKey, "");
        if (string.IsNullOrEmpty(json))
            return;
        var wrapper = JsonUtility.FromJson<BuildSceneList>(json);
        if (wrapper?.scenes == null) return;

        var arr = new UnityEditor.EditorBuildSettingsScene[wrapper.scenes.Length];
        for (int i = 0; i < wrapper.scenes.Length; i++)
        {
            arr[i] = new UnityEditor.EditorBuildSettingsScene(wrapper.scenes[i].path, wrapper.scenes[i].enabled);
        }
        UnityEditor.EditorBuildSettings.scenes = arr;
    }

    [Serializable]
    class SceneList { public string[] paths; }

    [Serializable]
    class BuildScene { public string path; public bool enabled; }

    [Serializable]
    class BuildSceneList { public BuildScene[] scenes; }
}