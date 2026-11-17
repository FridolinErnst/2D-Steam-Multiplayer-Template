using System.Linq;
using Game.Editor.Build;
using NUnit.Framework;
using UnityEditor;

namespace Game.Build.Tests
{
    public sealed class TestScenesBuildFilterTests
    {
        [Test]
        public void WithoutTestScenes_ListWithTestScene_KeepsOthersInOrder()
        {
            string[] scenes =
            {
                "Assets/_Project/Scenes/Main.unity",
                "Assets/_Project/Scenes/Tests/MenuTest.unity",
                "Assets/_Project/Scenes/Other.unity",
            };

            CollectionAssert.AreEqual(
                new[] { "Assets/_Project/Scenes/Main.unity", "Assets/_Project/Scenes/Other.unity" },
                TestScenesBuildFilter.WithoutTestScenes(scenes));
        }

        [Test]
        public void IsTestScene_SceneOnlyNamedTest_IsFalse()
        {
            Assert.IsFalse(TestScenesBuildFilter.IsTestScene("Assets/_Project/Scenes/MenuTest.unity"));
            Assert.IsFalse(TestScenesBuildFilter.IsTestScene(null));
        }

        [Test]
        public void BuildSettings_TestScenes_AreOnlyUnderTestsFolder()
        {
            string[] misplaced = EditorBuildSettings.scenes
                .Select(s => s.path)
                .Where(p => p.Contains("Test") && !TestScenesBuildFilter.IsTestScene(p))
                .ToArray();

            Assert.IsEmpty(misplaced, $"Move test scenes into {TestScenesBuildFilter.TestScenesFolder} so they stay out of player builds.");
        }

        [Test]
        public void PlayerBuildScenes_IncludeMain()
        {
            string[] built = TestScenesBuildFilter.WithoutTestScenes(EditorBuildSettings.scenes.Select(s => s.path).ToArray());

            CollectionAssert.Contains(built, "Assets/_Project/Scenes/Main.unity");
        }
    }
}
