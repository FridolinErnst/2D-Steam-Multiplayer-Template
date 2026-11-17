using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Atoms;
using Game.Data;
using Game.Networking;
using Game.Session;
using Game.Steam;
using Game.UI;
using TMPro;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityAtoms.BaseAtoms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor.Setup
{
    /// <summary>
    /// Builds the multiplayer session layer: session Atoms, SessionSettings, the (empty) networked Player prefab, the
    /// network prefab list, and in the Main scene the network systems (NGO, Unity + Steam transports, Steam, session
    /// control, pause, network conditions, player count), the main menu and the in-game menu. Creates Main from the
    /// URP 2D scene template if missing; on every run it rebuilds only its own root objects (<see cref="OwnedRoots"/>),
    /// so content other steps or people add to the scene survives. The Player prefab is created only when missing.
    /// </summary>
    [GameSetupStep(10)]
    public static class SessionSetup
    {
        private const string AtomsRoot = "Assets/_Project/Atoms";
        private const string DataRoot = "Assets/_Project/Data/Session";
        internal const string PrefabRoot = "Assets/_Project/Prefabs/Session";
        internal const string PlayerPrefabPath = PrefabRoot + "/Player.prefab";
        internal const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string SceneTemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";

        /// <summary>Root objects of the Main scene this step owns and rebuilds on every run.</summary>
        internal static readonly string[] OwnedRoots = { "Network", "SessionState", "SpawnPoint", "UI", "EventSystem" };

        private static readonly Color MenuBackground = new Color(0.08f, 0.09f, 0.11f, 1f);
        private static readonly Color OverlayBackground = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color MessageColor = new Color(1f, 0.75f, 0.4f, 1f);

        private static readonly MethodInfo NetworkObjectOnValidate =
            typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);

        internal sealed class Atoms
        {
            public BoolVariable IsSteamRunning, IsUsingSteam, IsSessionActive, IsLocalSession, IsConnected, IsPauseMenuOpen, IsPaused;
            public StringVariable LocalPlayerName, DisconnectMessage;
            public IntVariable ConnectedPlayers;
            public VoidEvent HostRequested, JoinRequested, JoinFriendRequested, LocalRequested, LeaveRequested, InviteRequested;
            public GameObjectValueList Players;
            public SessionSettings Settings;
        }

        [MenuItem("Game/Setup/10 Session")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "SKIPPED: save or discard changes in the open scenes first (this step reopens the Main scene).";

            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                return "Imported TextMeshPro Essential Resources (default font). Run 'Game/Setup/Run All' again once Unity has finished importing.";
            }

            // Open the scene first: opening unloads unused assets, which would invalidate assets loaded before it.
            Scene scene = OpenOrCreateMainScene(out bool sceneCreated);
            RemoveOwnedRoots(scene);

            var log = new System.Text.StringBuilder();
            Atoms atoms = CreateAtoms(log);
            GameObject player = LoadOrCreatePlayerPrefab(atoms, log);
            NetworkPrefabsList prefabList = BuildPrefabList(player);

            var spawn = new GameObject("SpawnPoint");
            BuildNetworkSystems(atoms, player, prefabList, spawn.transform);
            BuildUi(atoms);
            SaveSceneWithNetworkIds(scene, ScenePath);
            AddSceneToBuild();
            log.Append($"Scene: {ScenePath} ({(sceneCreated ? "created from the URP 2D template" : "session objects rebuilt")}, first in Build Settings).\n");

            SetupUtil.SaveAndRefresh();
            return log.ToString();
        }

        // ---------------------------------------------------------------- atoms & data

        internal static Atoms CreateAtoms(System.Text.StringBuilder log)
        {
            string vars = $"{AtomsRoot}/Variables/Session", events = $"{AtomsRoot}/Events/Session";
            var a = new Atoms
            {
                IsSteamRunning = SetupUtil.LoadOrCreate<BoolVariable>($"{vars}/IsSteamRunning.asset"),
                IsUsingSteam = SetupUtil.LoadOrCreate<BoolVariable>($"{vars}/IsUsingSteam.asset"),
                IsSessionActive = SetupUtil.LoadOrCreate<BoolVariable>($"{vars}/IsSessionActive.asset"),
                IsLocalSession = SetupUtil.LoadOrCreate<BoolVariable>($"{vars}/IsLocalSession.asset"),
                IsConnected = SetupUtil.LoadOrCreate<BoolVariable>($"{vars}/IsConnected.asset"),
                IsPauseMenuOpen = SetupUtil.LoadOrCreate<BoolVariable>($"{vars}/IsPauseMenuOpen.asset"),
                IsPaused = SetupUtil.LoadOrCreate<BoolVariable>($"{vars}/IsPaused.asset"),
                LocalPlayerName = SetupUtil.LoadOrCreate<StringVariable>($"{vars}/LocalPlayerName.asset"),
                DisconnectMessage = SetupUtil.LoadOrCreate<StringVariable>($"{vars}/DisconnectMessage.asset"),
                ConnectedPlayers = SetupUtil.LoadOrCreate<IntVariable>($"{vars}/ConnectedPlayers.asset"),
                HostRequested = SetupUtil.LoadOrCreate<VoidEvent>($"{events}/HostRequested.asset"),
                JoinRequested = SetupUtil.LoadOrCreate<VoidEvent>($"{events}/JoinRequested.asset"),
                JoinFriendRequested = SetupUtil.LoadOrCreate<VoidEvent>($"{events}/JoinFriendRequested.asset"),
                LocalRequested = SetupUtil.LoadOrCreate<VoidEvent>($"{events}/LocalRequested.asset"),
                LeaveRequested = SetupUtil.LoadOrCreate<VoidEvent>($"{events}/LeaveRequested.asset"),
                InviteRequested = SetupUtil.LoadOrCreate<VoidEvent>($"{events}/InviteRequested.asset"),
                Players = SetupUtil.LoadOrCreate<GameObjectValueList>($"{AtomsRoot}/Lists/Session/Players.asset"),
                Settings = SetupUtil.LoadOrCreate<SessionSettings>($"{DataRoot}/SessionSettings.asset"),
            };

            foreach (VoidEvent e in new[] { a.HostRequested, a.JoinRequested, a.JoinFriendRequested, a.LocalRequested, a.LeaveRequested, a.InviteRequested })
                SetupUtil.SetReplayBufferSize(e, 0);

            log.Append("Atoms: Variables/Session (Steam, session, connection, pause, player name/count, disconnect message), " +
                       "Events/Session (Host, Join, JoinFriend, Local, Leave, Invite), Lists/Session/Players, Data/Session/SessionSettings.\n");
            return a;
        }

        /// <summary>The networked Player prefab: a NetworkObject that lists itself in <c>Players</c>. Games add their components to it.</summary>
        private static GameObject LoadOrCreatePlayerPrefab(Atoms atoms, System.Text.StringBuilder log)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (existing != null)
            {
                log.Append($"Player prefab: kept {PlayerPrefabPath}.\n");
                return existing;
            }

            var root = new GameObject("Player");
            root.AddComponent<NetworkObject>();
            var register = root.AddComponent<RegisterInList>();
            SetupUtil.SetRef(register, "_list", atoms.Players);

            SetupUtil.EnsureFolder(PrefabRoot);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);

            // NGO computes GlobalObjectIdHash in OnValidate, which needs the saved asset's GUID.
            NetworkObjectOnValidate?.Invoke(prefab.GetComponent<NetworkObject>(), null);
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssetIfDirty(prefab);
            log.Append($"Player prefab: created {PlayerPrefabPath}.\n");
            return prefab;
        }

        internal static NetworkPrefabsList BuildPrefabList(params GameObject[] prefabs)
        {
            var list = SetupUtil.LoadOrCreate<NetworkPrefabsList>($"{DataRoot}/NetworkPrefabs.asset");
            foreach (GameObject prefab in prefabs)
                if (!list.Contains(prefab)) list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            return list;
        }

        // ---------------------------------------------------------------- scene

        private static Scene OpenOrCreateMainScene(out bool created)
        {
            created = !File.Exists(ScenePath);
            if (created)
            {
                SetupUtil.EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));
                if (!AssetDatabase.CopyAsset(SceneTemplatePath, ScenePath))
                    throw new System.InvalidOperationException($"Could not copy {SceneTemplatePath} to {ScenePath}.");
            }
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void RemoveOwnedRoots(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (OwnedRoots.Contains(root.name)) Object.DestroyImmediate(root);
        }

        /// <summary>Network systems: NGO, both transports, Steam, session control, pause, network conditions, player count.</summary>
        internal static void BuildNetworkSystems(Atoms atoms, GameObject player, NetworkPrefabsList prefabList, Transform spawn)
        {
            var network = new GameObject("Network");
            var manager = network.AddComponent<NetworkManager>();
            var direct = network.AddComponent<UnityTransport>();
            var steamTransport = network.AddComponent<SteamSocketsTransport>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = direct;
            manager.NetworkConfig.PlayerPrefab = player;
            manager.NetworkConfig.ConnectionApproval = true;
            // Only our list: NGO auto-adds its DefaultNetworkPrefabs on AddComponent, which would register every prefab twice.
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            var bootstrap = network.AddComponent<SteamBootstrap>();
            SetupUtil.SetRef(bootstrap, "_settings", atoms.Settings);
            SetupUtil.SetRef(bootstrap, "_isSteamRunning", atoms.IsSteamRunning);
            SetupUtil.SetRef(bootstrap, "_localPlayerName", atoms.LocalPlayerName);

            var selector = network.AddComponent<TransportSelector>();
            SetupUtil.SetRef(selector, "_networkManager", manager);
            SetupUtil.SetRef(selector, "_directTransport", direct);
            SetupUtil.SetRef(selector, "_steamTransport", steamTransport);
            SetupUtil.SetRef(selector, "_settings", atoms.Settings);
            SetupUtil.SetVariableRef(selector, "_isSteamRunning", atoms.IsSteamRunning);
            SetupUtil.SetRef(selector, "_isLocalSession", atoms.IsLocalSession);
            SetupUtil.SetRef(selector, "_isUsingSteam", atoms.IsUsingSteam);

            var starter = network.AddComponent<SessionStarter>();
            SetupUtil.SetRef(starter, "_networkManager", manager);
            SetupUtil.SetRef(starter, "_settings", atoms.Settings);
            SetupUtil.SetRef(starter, "_spawnPoint", spawn);
            SetupUtil.SetEventRef(starter, "_hostRequested", atoms.HostRequested);
            SetupUtil.SetEventRef(starter, "_joinRequested", atoms.JoinRequested);
            SetupUtil.SetEventRef(starter, "_localRequested", atoms.LocalRequested);
            SetupUtil.SetEventRef(starter, "_leaveRequested", atoms.LeaveRequested);
            SetupUtil.SetRef(starter, "_isSessionActive", atoms.IsSessionActive);
            SetupUtil.SetRef(starter, "_isLocalSession", atoms.IsLocalSession);
            SetupUtil.SetRef(starter, "_isConnected", atoms.IsConnected);
            SetupUtil.SetRef(starter, "_disconnectMessage", atoms.DisconnectMessage);

            var lobby = network.AddComponent<SteamLobby>();
            SetupUtil.SetRef(lobby, "_networkManager", manager);
            SetupUtil.SetRef(lobby, "_transport", steamTransport);
            SetupUtil.SetRef(lobby, "_settings", atoms.Settings);
            SetupUtil.SetVariableRef(lobby, "_isSteamRunning", atoms.IsSteamRunning);
            SetupUtil.SetEventRef(lobby, "_inviteRequested", atoms.InviteRequested);
            SetupUtil.SetEventRef(lobby, "_joinFriendRequested", atoms.JoinFriendRequested);
            SetupUtil.SetRef(lobby, "_joinRequested", atoms.JoinRequested);

            var pause = network.AddComponent<SessionPause>();
            SetupUtil.SetRef(pause, "_isPauseMenuOpen", atoms.IsPauseMenuOpen);
            SetupUtil.SetRef(pause, "_isLocalSession", atoms.IsLocalSession);
            SetupUtil.SetRef(pause, "_isPaused", atoms.IsPaused);

            // Multiplayer Play Mode: main Editor hosts, clones join (Editor only, off until ticked on the component).
            var autoSession = network.AddComponent<EditorAutoSession>();
            SetupUtil.SetRef(autoSession, "_hostRequested", atoms.HostRequested);
            SetupUtil.SetRef(autoSession, "_joinRequested", atoms.JoinRequested);
            SetupUtil.SetRef(autoSession, "_isSessionActive", atoms.IsSessionActive);
            SetupUtil.SetRef(autoSession, "_isConnected", atoms.IsConnected);

            AddNetworkConditions(network);

            var sessionState = new GameObject("SessionState");
            sessionState.AddComponent<NetworkObject>();
            var mirror = sessionState.AddComponent<PlayerCountMirror>();
            SetupUtil.SetRef(mirror, "_connectedPlayers", atoms.ConnectedPlayers);
        }

        /// <summary>
        /// Dev tool on the Network object: Unity's Network Simulator plus the picker panel that switches simulated
        /// connection profiles during play (Editor and development builds only). Adds whatever is missing.
        /// </summary>
        public static void AddNetworkConditions(GameObject network)
        {
            if (!network.TryGetComponent(out NetworkSimulator simulator)) simulator = network.AddComponent<NetworkSimulator>();
            if (!network.TryGetComponent(out NetworkConditionsPicker picker)) picker = network.AddComponent<NetworkConditionsPicker>();
            SetupUtil.SetRef(picker, "_simulator", simulator);
        }

        // ---------------------------------------------------------------- UI

        /// <summary>Canvas with the main menu and the in-game menu (plain placeholder look), plus the EventSystem.</summary>
        internal static void BuildUi(Atoms atoms)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var ui = new GameObject("UI", typeof(RectTransform));
            var canvas = ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            ui.AddComponent<GraphicRaycaster>();

            // Main menu
            GameObject mainPanel = Panel(ui.transform, "MainMenuPanel", MenuBackground);
            Transform mainColumn = Column(mainPanel.transform);
            TMP_Text title = Label(mainColumn, "Title", "Game", 72f, FontStyles.Bold, Color.white, 120f);
            TMP_Text message = Label(mainColumn, "Message", string.Empty, 30f, FontStyles.Normal, MessageColor, 60f);
            Button host = MenuButton(mainColumn, "HostButton", "Host Online Game");
            Button join = MenuButton(mainColumn, "JoinButton", "Join Friend");
            Button local = MenuButton(mainColumn, "LocalButton", "Play Local");
            Button quit = MenuButton(mainColumn, "QuitButton", "Quit");

            var mainMenu = ui.AddComponent<MainMenu>();
            SetupUtil.SetRef(mainMenu, "_panel", mainPanel);
            SetupUtil.SetRef(mainMenu, "_title", title);
            SetupUtil.SetRef(mainMenu, "_message", message);
            SetupUtil.SetRef(mainMenu, "_host", host);
            SetupUtil.SetRef(mainMenu, "_join", join);
            SetupUtil.SetRef(mainMenu, "_local", local);
            SetupUtil.SetRef(mainMenu, "_quit", quit);
            SetupUtil.SetRef(mainMenu, "_hostRequested", atoms.HostRequested);
            SetupUtil.SetRef(mainMenu, "_joinRequested", atoms.JoinRequested);
            SetupUtil.SetRef(mainMenu, "_joinFriendRequested", atoms.JoinFriendRequested);
            SetupUtil.SetRef(mainMenu, "_localRequested", atoms.LocalRequested);
            SetupUtil.SetRef(mainMenu, "_isSessionActive", atoms.IsSessionActive);
            SetupUtil.SetVariableRef(mainMenu, "_isUsingSteam", atoms.IsUsingSteam);
            SetupUtil.SetRef(mainMenu, "_disconnectMessage", atoms.DisconnectMessage);

            // In-game menu
            GameObject pausePanel = Panel(ui.transform, "PauseMenuPanel", OverlayBackground);
            Transform pauseColumn = Column(pausePanel.transform);
            TMP_Text pauseTitle = Label(pauseColumn, "Title", "Paused", 60f, FontStyles.Bold, Color.white, 100f);
            Button resume = MenuButton(pauseColumn, "ResumeButton", "Resume");
            Button invite = MenuButton(pauseColumn, "InviteButton", "Invite Friends");
            Button leave = MenuButton(pauseColumn, "LeaveButton", "Leave to Main Menu");
            pausePanel.SetActive(false);

            var pauseMenu = ui.AddComponent<PauseMenu>();
            SetupUtil.SetRef(pauseMenu, "_panel", pausePanel);
            SetupUtil.SetRef(pauseMenu, "_title", pauseTitle);
            SetupUtil.SetRef(pauseMenu, "_resume", resume);
            SetupUtil.SetRef(pauseMenu, "_invite", invite);
            SetupUtil.SetRef(pauseMenu, "_leave", leave);
            SetupUtil.SetRef(pauseMenu, "_pause", RequireAction("Player/Pause"));
            SetupUtil.SetRef(pauseMenu, "_inviteRequested", atoms.InviteRequested);
            SetupUtil.SetRef(pauseMenu, "_leaveRequested", atoms.LeaveRequested);
            SetupUtil.SetRef(pauseMenu, "_isPauseMenuOpen", atoms.IsPauseMenuOpen);
            SetupUtil.SetRef(pauseMenu, "_isSessionActive", atoms.IsSessionActive);
            SetupUtil.SetVariableRef(pauseMenu, "_isLocalSession", atoms.IsLocalSession);
            SetupUtil.SetVariableRef(pauseMenu, "_isUsingSteam", atoms.IsUsingSteam);
        }

        private static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Transform Column(Transform parent)
        {
            var go = new GameObject("Content", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(640f, 760f);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return go.transform;
        }

        private static TMP_Text Label(Transform parent, string name, string text, float size, FontStyles style, Color color, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = 640f;
            element.preferredHeight = height;
            return label;
        }

        private static Button MenuButton(Transform parent, string name, string text)
        {
            var resources = new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            };
            GameObject go = TMP_DefaultControls.CreateButton(resources);
            go.name = name;
            go.transform.SetParent(parent, false);
            TMP_Text label = go.GetComponentInChildren<TMP_Text>();
            label.text = text;
            label.fontSize = 36f;
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = 520f;
            element.preferredHeight = 80f;
            return go.GetComponent<Button>();
        }

        // ---------------------------------------------------------------- saving

        /// <summary>Saves the scene, then reopens it so in-scene NetworkObjects get their GlobalObjectIdHash, and saves again.</summary>
        internal static void SaveSceneWithNetworkIds(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);

            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (NetworkObject no in Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
            {
                NetworkObjectOnValidate?.Invoke(no, null);
                EditorUtility.SetDirty(no);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Puts Main first in Build Settings and drops entries whose scene file no longer exists.</summary>
        private static void AddSceneToBuild()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(s => s.path != ScenePath && File.Exists(s.path))
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static UnityEngine.InputSystem.InputActionReference RequireAction(string mapSlashAction)
        {
            var action = SetupUtil.LoadInputAction(mapSlashAction);
            if (action == null) throw new System.InvalidOperationException($"Input action '{mapSlashAction}' not found in {SetupUtil.InputActionsPath}; run step 02 first.");
            return action;
        }
    }
}
