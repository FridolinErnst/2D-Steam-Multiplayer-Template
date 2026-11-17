using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.Build
{
    /// <summary>
    /// Keeps test-only scenes (under <see cref="TestScenesFolder"/>) out of player builds. They stay in Build Settings
    /// so NGO scene sync can bring a client into them while playing in the Editor. The Build button drops them from
    /// the scene list; a build that includes one anyway (e.g. a scripted <c>BuildPipeline.BuildPlayer</c>) fails.
    /// </summary>
    [InitializeOnLoad]
    public sealed class TestScenesBuildFilter : IProcessSceneWithReport
    {
        /// <summary>Scenes in this folder (or below) are for testing only.</summary>
        public const string TestScenesFolder = "Assets/_Project/Scenes/Tests/";

        static TestScenesBuildFilter() => BuildPlayerWindow.RegisterBuildPlayerHandler(BuildWithoutTestScenes);

        /// <inheritdoc />
        public int callbackOrder => 0;

        /// <summary>True if <paramref name="scenePath"/> is a test-only scene.</summary>
        public static bool IsTestScene(string scenePath) =>
            !string.IsNullOrEmpty(scenePath) && scenePath.Replace('\\', '/').StartsWith(TestScenesFolder, StringComparison.Ordinal);

        /// <summary><paramref name="scenes"/> without the test-only scenes, in the same order.</summary>
        public static string[] WithoutTestScenes(string[] scenes)
        {
            if (scenes == null) return Array.Empty<string>();
            var kept = new List<string>(scenes.Length);
            foreach (string scene in scenes)
                if (!IsTestScene(scene)) kept.Add(scene);
            return kept.ToArray();
        }

        private static void BuildWithoutTestScenes(BuildPlayerOptions options)
        {
            string[] kept = WithoutTestScenes(options.scenes);
            int dropped = (options.scenes?.Length ?? 0) - kept.Length;
            if (dropped > 0) Debug.Log($"{nameof(TestScenesBuildFilter)}: left {dropped} test scene(s) out of the build.");
            options.scenes = kept;
            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        }

        /// <inheritdoc />
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !IsTestScene(scene.path)) return; // null report = entering Play Mode, not a build
            throw new BuildFailedException(
                $"Test scene '{scene.path}' is in the player build. Build through the Build button, or leave scenes under {TestScenesFolder} out of the scene list.");
        }
    }
}
