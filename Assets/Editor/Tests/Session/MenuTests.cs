using Game.Support.Tests;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityAtoms.BaseAtoms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Session.Tests
{
    /// <summary>Main menu and in-game menu: visibility, which request each button raises, and the Invite button rule.</summary>
    public sealed class MenuTests
    {
        private TestScene _scene;
        private AtomFactory _atoms;
        private BoolVariable _sessionActive, _local, _usingSteam, _menuOpen;
        private StringVariable _message;
        private VoidEvent _host, _join, _joinFriend, _localRequested, _invite, _leave;
        private int _hostCount, _joinCount, _joinFriendCount, _localCount, _inviteCount, _leaveCount;
        private bool _lastMenuOpen;

        // In Edit Mode Variable.Value reads InitialValue: inputs set both, outputs are observed through Changed.
        private static void Set(BoolVariable v, bool value)
        {
            v.InitialValue = value;
            v.Value = value;
        }

        private void OnMenuOpenChanged(bool open) => _lastMenuOpen = open;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScene.New();
            _atoms = new AtomFactory();
            _sessionActive = _atoms.Bool();
            _local = _atoms.Bool();
            _usingSteam = _atoms.Bool();
            _menuOpen = _atoms.Bool();
            _message = _atoms.String();
            _host = Event(() => _hostCount++);
            _join = Event(() => _joinCount++);
            _joinFriend = Event(() => _joinFriendCount++);
            _localRequested = Event(() => _localCount++);
            _invite = Event(() => _inviteCount++);
            _leave = Event(() => _leaveCount++);
            _hostCount = _joinCount = _joinFriendCount = _localCount = _inviteCount = _leaveCount = 0;
            _lastMenuOpen = false;
            _menuOpen.Changed.Register(OnMenuOpenChanged);
        }

        [TearDown]
        public void TearDown()
        {
            _menuOpen.Changed.Unregister(OnMenuOpenChanged);
            _scene.Dispose();
            _atoms.DestroyAll();
        }

        private VoidEvent Event(System.Action onRaised)
        {
            var e = _atoms.New<VoidEvent>();
            AtomFactory.DisableReplay(e);
            e.Register(onRaised);
            return e;
        }

        private Button NewButton(string name) => _scene.Add<Button>(name);

        private TMP_Text NewText(string name) => _scene.Add<TextMeshProUGUI>(name);

        // ---------------------------------------------------------------- main menu

        private (MainMenu menu, GameObject panel, Button host, Button join, Button local, TMP_Text message) NewMainMenu()
        {
            GameObject panel = _scene.Go("Panel");
            Button host = NewButton("Host"), join = NewButton("Join"), local = NewButton("Local");
            TMP_Text message = NewText("Message");
            var menu = _scene.Add<MainMenu>("MainMenu");
            TestScene.Set(menu, "_panel", panel);
            TestScene.Set(menu, "_title", NewText("Title"));
            TestScene.Set(menu, "_message", message);
            TestScene.Set(menu, "_host", host);
            TestScene.Set(menu, "_join", join);
            TestScene.Set(menu, "_local", local);
            TestScene.Set(menu, "_quit", NewButton("Quit"));
            TestScene.Set(menu, "_hostRequested", _host);
            TestScene.Set(menu, "_joinRequested", _join);
            TestScene.Set(menu, "_joinFriendRequested", _joinFriend);
            TestScene.Set(menu, "_localRequested", _localRequested);
            TestScene.Set(menu, "_isSessionActive", _sessionActive);
            TestScene.SetVariableRef(menu, "_isUsingSteam", _usingSteam);
            TestScene.Set(menu, "_disconnectMessage", _message);
            TestScene.CallPrivate(menu, "OnEnable");
            return (menu, panel, host, join, local, message);
        }

        [Test]
        public void MainMenu_ShownOnlyWithoutSession()
        {
            var m = NewMainMenu();
            Assert.IsTrue(m.panel.activeSelf);

            Set(_sessionActive, true);
            Assert.IsFalse(m.panel.activeSelf);

            Set(_sessionActive, false);
            Assert.IsTrue(m.panel.activeSelf);
            TestScene.CallPrivate(m.menu, "OnDisable");
        }

        [Test]
        public void MainMenu_HostAndLocal_RaiseTheirRequests()
        {
            var m = NewMainMenu();
            m.host.onClick.Invoke();
            m.local.onClick.Invoke();

            Assert.AreEqual(1, _hostCount);
            Assert.AreEqual(1, _localCount);
            TestScene.CallPrivate(m.menu, "OnDisable");
        }

        [Test]
        public void MainMenu_Join_OpensFriendsList_WhenUsingSteam()
        {
            Set(_usingSteam, true);
            var m = NewMainMenu();
            m.join.onClick.Invoke();

            Assert.AreEqual(1, _joinFriendCount);
            Assert.AreEqual(0, _joinCount);
            TestScene.CallPrivate(m.menu, "OnDisable");
        }

        [Test]
        public void MainMenu_Join_JoinsDirectly_WithoutSteam()
        {
            var m = NewMainMenu();
            m.join.onClick.Invoke();

            Assert.AreEqual(1, _joinCount);
            Assert.AreEqual(0, _joinFriendCount);
            TestScene.CallPrivate(m.menu, "OnDisable");
        }

        [Test]
        public void MainMenu_Message_ShownOnlyWhenSet()
        {
            var m = NewMainMenu();
            Assert.IsFalse(m.message.gameObject.activeSelf);

            _message.Value = "Host left the game.";
            Assert.IsTrue(m.message.gameObject.activeSelf);
            Assert.AreEqual("Host left the game.", m.message.text);
            TestScene.CallPrivate(m.menu, "OnDisable");
        }

        [Test]
        public void MainMenu_Disabled_StopsFollowingSession()
        {
            var m = NewMainMenu();
            TestScene.CallPrivate(m.menu, "OnDisable");
            Set(_sessionActive, true);
            m.host.onClick.Invoke();

            Assert.IsTrue(m.panel.activeSelf);
            Assert.AreEqual(0, _hostCount);
        }

        // ---------------------------------------------------------------- in-game menu

        private (PauseMenu menu, GameObject panel, Button invite, Button leave, InputAction action) NewPauseMenu()
        {
            GameObject panel = _scene.Go("PausePanel");
            Button invite = NewButton("Invite"), leave = NewButton("Leave");
            InputActionAsset asset = _atoms.Track(ScriptableObject.CreateInstance<InputActionAsset>());
            InputAction action = asset.AddActionMap("Player").AddAction("Pause", InputActionType.Button);
            InputActionReference reference = _atoms.Track(InputActionReference.Create(action));
            var menu = _scene.Add<PauseMenu>("PauseMenu");
            TestScene.Set(menu, "_panel", panel);
            TestScene.Set(menu, "_title", NewText("PauseTitle"));
            TestScene.Set(menu, "_resume", NewButton("Resume"));
            TestScene.Set(menu, "_invite", invite);
            TestScene.Set(menu, "_leave", leave);
            TestScene.Set(menu, "_pause", reference);
            TestScene.Set(menu, "_inviteRequested", _invite);
            TestScene.Set(menu, "_leaveRequested", _leave);
            TestScene.Set(menu, "_isPauseMenuOpen", _menuOpen);
            TestScene.Set(menu, "_isSessionActive", _sessionActive);
            TestScene.SetVariableRef(menu, "_isLocalSession", _local);
            TestScene.SetVariableRef(menu, "_isUsingSteam", _usingSteam);
            TestScene.CallPrivate(menu, "OnEnable");
            return (menu, panel, invite, leave, action);
        }

        private static void Dispose((PauseMenu menu, GameObject panel, Button invite, Button leave, InputAction action) p)
        {
            TestScene.CallPrivate(p.menu, "OnDisable");
            p.action.Disable();
        }

        [Test]
        public void PauseMenu_DoesNotOpen_WithoutSession()
        {
            var p = NewPauseMenu();
            p.menu.Toggle();

            Assert.IsFalse(_lastMenuOpen);
            Assert.IsFalse(p.panel.activeSelf);
            Dispose(p);
        }

        [Test]
        public void PauseMenu_TogglesDuringSession()
        {
            Set(_sessionActive, true);
            var p = NewPauseMenu();

            p.menu.Toggle();
            Assert.IsTrue(_lastMenuOpen);
            Assert.IsTrue(p.panel.activeSelf);

            p.menu.Toggle();
            Assert.IsFalse(_lastMenuOpen);
            Assert.IsFalse(p.panel.activeSelf);
            Dispose(p);
        }

        [Test]
        public void PauseMenu_Invite_OnlyOnlineOverSteam()
        {
            Set(_sessionActive, true);
            var p = NewPauseMenu();

            Set(_usingSteam, true);
            p.menu.Toggle();
            Assert.IsTrue(p.invite.gameObject.activeSelf, "online over Steam");
            p.menu.Toggle();

            Set(_local, true);
            p.menu.Toggle();
            Assert.IsFalse(p.invite.gameObject.activeSelf, "local game");
            p.menu.Toggle();

            Set(_local, false);
            Set(_usingSteam, false);
            p.menu.Toggle();
            Assert.IsFalse(p.invite.gameObject.activeSelf, "online without Steam");
            Dispose(p);
        }

        [Test]
        public void PauseMenu_Leave_ClosesAndRequestsLeave()
        {
            Set(_sessionActive, true);
            var p = NewPauseMenu();
            p.menu.Toggle();
            p.leave.onClick.Invoke();

            Assert.IsFalse(_lastMenuOpen);
            Assert.AreEqual(1, _leaveCount);
            Dispose(p);
        }

        [Test]
        public void PauseMenu_ClosesWhenSessionEnds()
        {
            Set(_sessionActive, true);
            var p = NewPauseMenu();
            p.menu.Toggle();
            Set(_sessionActive, false);

            Assert.IsFalse(_lastMenuOpen);
            Assert.IsFalse(p.panel.activeSelf);
            Dispose(p);
        }
    }
}
