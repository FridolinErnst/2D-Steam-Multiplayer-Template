using System.Linq;
using Game.Editor.Setup;
using Game.UI;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Session.Tests
{
    /// <summary>What the session setup step (10) builds: Main first in Build Settings, its session objects, the Player prefab.</summary>
    public sealed class SessionContentTests
    {
        [Test]
        public void MainScene_IsFirstInBuildSettings()
        {
            Assert.IsNotEmpty(EditorBuildSettings.scenes);
            Assert.AreEqual(SessionSetup.ScenePath, EditorBuildSettings.scenes[0].path);
        }

        [Test]
        public void MainScene_HasSessionObjects_AndMenus()
        {
            bool wasOpen = SceneManager.GetSceneByPath(SessionSetup.ScenePath).isLoaded;
            Scene scene = EditorSceneManager.OpenScene(SessionSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                string[] roots = scene.GetRootGameObjects().Select(r => r.name).ToArray();
                foreach (string owned in SessionSetup.OwnedRoots) CollectionAssert.Contains(roots, owned);

                GameObject ui = scene.GetRootGameObjects().First(r => r.name == "UI");
                Assert.IsTrue(ui.TryGetComponent(out MainMenu _), "UI has the main menu");
                Assert.IsTrue(ui.TryGetComponent(out PauseMenu _), "UI has the in-game menu");

                GameObject network = scene.GetRootGameObjects().First(r => r.name == "Network");
                Assert.IsTrue(network.TryGetComponent(out NetworkManager manager));
                Assert.AreEqual(AssetDatabase.LoadAssetAtPath<GameObject>(SessionSetup.PlayerPrefabPath), manager.NetworkConfig.PlayerPrefab);
            }
            finally
            {
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void PlayerPrefab_IsANetworkObject()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(SessionSetup.PlayerPrefabPath);
            Assert.IsNotNull(player, SessionSetup.PlayerPrefabPath);
            Assert.IsTrue(player.TryGetComponent(out NetworkObject _));
        }
    }
}
