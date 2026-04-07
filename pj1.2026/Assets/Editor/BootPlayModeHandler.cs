using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;

// This editor-only helper ensures the following sequence when Play is pressed in the Editor:
// 1) Set the scene named "_Boot" as the Play Mode start scene so it is loaded first.
// 2) Remember the currently active scene (the one the user had open).
// 3) When entering Play Mode, load the remembered scene additively into the player and then unload _Boot.
// This runs purely in the Editor (no runtime code is added to scenes) and does not modify other project scripts.
[InitializeOnLoad]
public static class BootPlayModeHandler
{
    private const string SessionKeyInitial = "BootPlayModeHandler.initialScenePath";
    private const string SessionKeyBoot = "BootPlayModeHandler.bootScenePath";

    private static string bootScenePath;
    private static string initialScenePath;
    // Pending paths used when waiting for the additive scene to finish loading
    private static string pendingInitialPath;
    private static string pendingBootPath;

    static BootPlayModeHandler()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // About to enter Play Mode: remember the active scene and set the Boot scene as the start scene
            var active = EditorSceneManager.GetActiveScene();
            initialScenePath = active.path;

            if (string.IsNullOrEmpty(initialScenePath))
            {
                Debug.LogWarning("BootPlayModeHandler: A cena ativa não está salva. Sequência de _Boot -> cena inicial foi cancelada.");
                bootScenePath = null;
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            bootScenePath = FindScenePathByName("_Boot");
            if (string.IsNullOrEmpty(bootScenePath))
            {
                Debug.LogWarning("BootPlayModeHandler: Não foi encontrada nenhuma cena chamada '_Boot'. Nada a fazer.");
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            if (initialScenePath == bootScenePath)
            {
                // If the active scene is already _Boot, leave default behavior.
                Debug.Log("BootPlayModeHandler: A cena ativa já é '_Boot'. Mantendo comportamento padrão.");
                EditorSceneManager.playModeStartScene = null;
                bootScenePath = null;
                initialScenePath = null;
                SessionState.SetString(SessionKeyBoot, "");
                SessionState.SetString(SessionKeyInitial, "");
                return;
            }

            // Set the Boot scene as the play-mode start scene.
            var bootAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(bootScenePath);
            if (bootAsset == null)
            {
                Debug.LogWarning($"BootPlayModeHandler: não foi possível carregar o asset de cena em '{bootScenePath}'.");
                EditorSceneManager.playModeStartScene = null;
                bootScenePath = null;
                initialScenePath = null;
                return;
            }

            EditorSceneManager.playModeStartScene = bootAsset;
            // Persist the chosen initial and boot scene paths across possible domain reloads
            // that happen when entering Play Mode. SessionState survives domain reloads.
            SessionState.SetString(SessionKeyBoot, bootScenePath);
            SessionState.SetString(SessionKeyInitial, initialScenePath);
            Debug.Log($"BootPlayModeHandler: Definida cena de boot '{bootScenePath}' como Play Mode Start Scene. Cena inicial lembrada: '{initialScenePath}'.");
        }
        else if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // We're in Play Mode: load the initial scene additively in the player and then unload the boot scene.
            // Attempt to recover paths persisted in SessionState in case a domain reload cleared
            // our static fields during the transition into Play Mode.
            if (string.IsNullOrEmpty(initialScenePath))
                initialScenePath = SessionState.GetString(SessionKeyInitial, "");
            if (string.IsNullOrEmpty(bootScenePath))
                bootScenePath = SessionState.GetString(SessionKeyBoot, "");

            if (!string.IsNullOrEmpty(initialScenePath) && !string.IsNullOrEmpty(bootScenePath))
            {
                Debug.Log($"BootPlayModeHandler: Carregando cena inicial '{initialScenePath}' em modo de jogo (aditivo)...");

                // Load the initial scene in play mode additively. We must wait until the scene
                // is fully loaded before unloading the boot scene; otherwise the player can end up
                // without any loaded scenes. EditorSceneManager.LoadSceneInPlayMode loads by path,
                // which works even if the scene is not in Build Settings.
                var loadParams = new LoadSceneParameters(LoadSceneMode.Additive);
                EditorSceneManager.LoadSceneInPlayMode(initialScenePath, loadParams);

                // Defer unloading of the boot scene until the additive scene finished loading.
                pendingInitialPath = initialScenePath;
                pendingBootPath = bootScenePath;
                // Register an update callback to poll for scene load completion.
                EditorApplication.update += PollForLoadedScene;
            }

            // Reset the editor start-scene setting so subsequent Play presses behave normally
            EditorSceneManager.playModeStartScene = null;
            // Clear remembered paths
            initialScenePath = null;
            bootScenePath = null;
            SessionState.SetString(SessionKeyBoot, "");
            SessionState.SetString(SessionKeyInitial, "");
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            // Clean up state when exiting play mode
            initialScenePath = null;
            bootScenePath = null;
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetString(SessionKeyBoot, "");
            SessionState.SetString(SessionKeyInitial, "");
        }
    }

    private static string FindScenePathByName(string sceneName)
    {
        // Search all Scene assets and match by filename (without extension).
        // Use case-insensitive comparison to avoid issues with filesystem case.
        var guids = AssetDatabase.FindAssets("t:Scene");
        var comparison = StringComparison.OrdinalIgnoreCase;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path)?.Trim();
            if (string.IsNullOrEmpty(fileName))
                continue;

            if (string.Equals(fileName, sceneName, comparison))
                return path;
        }

        return null;
    }

    // Polling helper: called on EditorApplication.update after entering Play Mode until
    // the additive initial scene is loaded, then unloads the boot scene and cleans up.
    private static void PollForLoadedScene()
    {
        if (string.IsNullOrEmpty(pendingInitialPath))
            return;

        var loaded = SceneManager.GetSceneByPath(pendingInitialPath);
        if (!loaded.IsValid() || !loaded.isLoaded)
            return; // still loading, wait

        // Initial scene is loaded; attempt to unload boot.
        if (!string.IsNullOrEmpty(pendingBootPath))
        {
            // Before unloading the boot scene, ensure any player components that cached
            // a reference to the boot camera get updated to use the camera from the
            // freshly loaded initial scene. This avoids a common problem where the
            // player finds the Boot scene's MainCamera during Awake and then that
            // camera gets destroyed when the Boot scene is unloaded, leaving the
            // player with a missing camera reference and unable to move.

            // Find the camera GameObject in the newly loaded initial scene.
            var rootObjects = loaded.GetRootGameObjects();
            GameObject initialMainCamera = null;
            foreach (var go in rootObjects)
            {
                if (go.CompareTag("MainCamera"))
                {
                    initialMainCamera = go;
                    break;
                }
            }

            // If we found the initial scene's main camera, assign it to any
            // ThirdPersonController instances (private field _mainCamera) via reflection.
            if (initialMainCamera != null)
            {
                foreach (var go in rootObjects)
                {
                    // Look for components named ThirdPersonController (namespace aware)
                    var components = go.GetComponentsInChildren<Component>(true);
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        var type = comp.GetType();
                        if (type.Name == "ThirdPersonController")
                        {
                            var field = type.GetField("_mainCamera", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (field != null)
                            {
                                field.SetValue(comp, initialMainCamera);
                                Debug.Log($"BootPlayModeHandler: Assigned initial scene camera to ThirdPersonController on '{comp.gameObject.name}'.");
                            }
                        }
                    }
                }
            }

            var bootScene = SceneManager.GetSceneByPath(pendingBootPath);
            if (bootScene.IsValid())
            {
                SceneManager.UnloadSceneAsync(bootScene);
                Debug.Log($"BootPlayModeHandler: Descarregando cena de boot '{pendingBootPath}'.");
            }
            else
            {
                Debug.LogWarning($"BootPlayModeHandler: A cena de boot '{pendingBootPath}' não estava válida no player; não foi possível descarregá-la.");
            }
        }

        // Cleanup
        pendingInitialPath = null;
        pendingBootPath = null;
        SessionState.SetString(SessionKeyBoot, "");
        SessionState.SetString(SessionKeyInitial, "");
        EditorSceneManager.playModeStartScene = null;

        // Unsubscribe
        EditorApplication.update -= PollForLoadedScene;
    }
}

