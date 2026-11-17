using Game.Session;
using Game.Support.Tests;
using NUnit.Framework;
using UnityAtoms.BaseAtoms;
using UnityEngine;

namespace Game.Session.Tests
{
    public sealed class SessionPauseTests
    {
        private TestScene _scene;
        private AtomFactory _atoms;
        private BoolVariable _menuOpen, _local, _paused;
        private SessionPause _pause;
        private bool _lastPaused;

        // In Edit Mode Variable.Value reads InitialValue: inputs set both, outputs are observed through Changed.
        private static void Set(BoolVariable v, bool value)
        {
            v.InitialValue = value;
            v.Value = value;
        }

        private void OnPausedChanged(bool paused) => _lastPaused = paused;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScene.New();
            _atoms = new AtomFactory();
            _menuOpen = _atoms.Bool();
            _local = _atoms.Bool();
            _paused = _atoms.Bool();
            _pause = _scene.Add<SessionPause>("Pause");
            TestScene.Set(_pause, "_isPauseMenuOpen", _menuOpen);
            TestScene.Set(_pause, "_isLocalSession", _local);
            TestScene.Set(_pause, "_isPaused", _paused);
            _lastPaused = false;
            _paused.Changed.Register(OnPausedChanged);
            TestScene.CallPrivate(_pause, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            TestScene.CallPrivate(_pause, "OnDisable");
            _paused.Changed.Unregister(OnPausedChanged);
            _scene.Dispose();
            _atoms.DestroyAll();
            Time.timeScale = 1f;
        }

        [Test]
        public void LocalGame_MenuOpen_Pauses()
        {
            Set(_local, true);
            Set(_menuOpen, true);

            Assert.IsTrue(_lastPaused);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void OnlineGame_MenuOpen_KeepsRunning()
        {
            Set(_menuOpen, true);

            Assert.IsFalse(_lastPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void LocalGame_MenuClosed_Resumes()
        {
            Set(_local, true);
            Set(_menuOpen, true);
            Set(_menuOpen, false);

            Assert.IsFalse(_lastPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void LocalSessionEnds_WhileMenuOpen_Resumes()
        {
            Set(_local, true);
            Set(_menuOpen, true);
            Set(_local, false);

            Assert.IsFalse(_lastPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void Disabled_IgnoresMenu()
        {
            TestScene.CallPrivate(_pause, "OnDisable");
            Set(_local, true);
            Set(_menuOpen, true);

            Assert.IsFalse(_lastPaused);
            Assert.AreEqual(1f, Time.timeScale);
            TestScene.CallPrivate(_pause, "OnEnable"); // TearDown disables again
        }
    }
}
