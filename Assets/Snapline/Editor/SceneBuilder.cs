using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Snapline.App;

namespace Snapline.EditorTools
{
    /// <summary>
    /// Creates the one scene the game needs.
    ///
    /// The scene contains a single GameObject with Bootstrap on it and nothing else; the entire
    /// interface is built in code at runtime. Regenerating it is therefore always safe, and the
    /// scene asset never becomes something that has to be hand-maintained.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Snapline/Scenes/Game.unity";

        [MenuItem("Snapline/Rebuild Game Scene")]
        public static void BuildScene()
        {
            string dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Bootstrap");
            go.AddComponent<Bootstrap>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Snapline] Scene rebuilt at {ScenePath}");
        }

        /// <summary>Make sure the game scene is the first (and only) enabled scene in the build.</summary>
        public static void RegisterInBuildSettings()
        {
            var scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = scenes;
        }

        /// <summary>Batch-mode entry point. Also serves as a compile gate in CI.</summary>
        public static void BuildSceneCLI()
        {
            BuildScene();
            Debug.Log("[Snapline] SceneBuilder.BuildSceneCLI complete.");
        }

        [MenuItem("Snapline/Open Game Scene")]
        public static void OpenGameScene()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[Snapline] {ScenePath} is missing. Use Snapline > Rebuild Game Scene.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }

    /// <summary>
    /// Opens the game scene when the editor starts up on an empty Untitled scene.
    ///
    /// This project has exactly one scene and everything in it is built at runtime, so an editor
    /// sitting on Untitled means Play does nothing at all and looks broken. Unity only restores a
    /// previously-opened scene, and a project that has so far only been driven from the command
    /// line has no such record.
    /// </summary>
    [InitializeOnLoad]
    internal static class OpenSceneOnFirstLoad
    {
        private const string SessionKey = "Snapline.GameSceneAutoOpened";

        static OpenSceneOnFirstLoad()
        {
            EditorApplication.delayCall += TryOpen;
        }

        private static void TryOpen()
        {
            // SessionState survives script recompiles but resets when the editor restarts, so this
            // runs once per session and never yanks you out of a scene you opened deliberately.
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Scene active = SceneManager.GetActiveScene();

            // Only act on the genuinely empty Untitled scene the editor creates on a cold start.
            if (!string.IsNullOrEmpty(active.path)) return;
            if (active.rootCount > 0) return;

            if (!File.Exists(SceneBuilder.ScenePath)) return;

            EditorSceneManager.OpenScene(SceneBuilder.ScenePath, OpenSceneMode.Single);
            Debug.Log("[Snapline] Opened the game scene. Press Play.");
        }
    }
}
