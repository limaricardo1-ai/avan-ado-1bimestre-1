using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// This editor-only helper ensures the following sequence when Play is pressed in the Editor:
// 1) Set the scene named "_Boot" as the Play Mode start scene so it is loaded first.
// 2) Remember the currently active scene (the one the user had open).
// 3) When entering Play Mode, load the remembered scene additively into the player and then unload _Boot.
// This runs purely in the Editor (no runtime code is added to scenes) and does not modify other project scripts.
[InitializeOnLoad]
public static class BootPlayModeHandler
{
    private static string bootScenePath;
    private static string initialScenePath;

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
            Debug.Log($"BootPlayModeHandler: Definida cena de boot '{bootScenePath}' como Play Mode Start Scene. Cena inicial lembrada: '{initialScenePath}'.");
        }
        else if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // We're in Play Mode: load the initial scene additively in the player and then unload the boot scene.
            if (!string.IsNullOrEmpty(initialScenePath) && !string.IsNullOrEmpty(bootScenePath))
            {
                Debug.Log($"BootPlayModeHandler: Carregando cena inicial '{initialScenePath}' em modo de jogo (aditivo)...");

                // Load the initial scene in play mode additively
                var loadParams = new LoadSceneParameters(LoadSceneMode.Additive);
                EditorSceneManager.LoadSceneInPlayMode(initialScenePath, loadParams);

                // Try to unload the boot scene from the player
                var bootScene = SceneManager.GetSceneByPath(bootScenePath);
                if (bootScene.IsValid())
                {
                    // Unload the boot scene from the player. Use SceneManager.UnloadSceneAsync which is
                    // available at runtime and works in play mode inside the Editor.
                    SceneManager.UnloadSceneAsync(bootScene);
                    Debug.Log($"BootPlayModeHandler: Descarregando cena de boot '{bootScenePath}'.");
                }
                else
                {
                    Debug.LogWarning($"BootPlayModeHandler: A cena de boot '{bootScenePath}' não estava válida no player; não foi possível descarregá-la.");
                }
            }

            // Reset the editor start-scene setting so subsequent Play presses behave normally
            EditorSceneManager.playModeStartScene = null;
            // Clear remembered paths
            initialScenePath = null;
            bootScenePath = null;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            // Clean up state when exiting play mode
            initialScenePath = null;
            bootScenePath = null;
            EditorSceneManager.playModeStartScene = null;
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
}

