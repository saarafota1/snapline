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
    }
}
