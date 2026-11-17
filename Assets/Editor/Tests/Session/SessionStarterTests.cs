using Game.Networking;
using Game.Support.Tests;
using NUnit.Framework;

namespace Game.Session.Tests
{
    /// <summary>The menu message chosen when a session ends without the local player leaving.</summary>
    public sealed class SessionStarterTests
    {
        private TestScene _scene;
        private SessionStarter _starter;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScene.New();
            _starter = _scene.Add<SessionStarter>("Starter");
        }

        [TearDown]
        public void TearDown() => _scene.Dispose();

        [Test]
        public void HostLeftReason_IsShownAsIs()
        {
            Assert.AreEqual("Host left the game.", _starter.MessageFor("Host left the game.", wasConnected: true));
        }

        [Test]
        public void SessionFullReason_IsShownAsIs()
        {
            Assert.AreEqual("The game is full.", _starter.MessageFor("The game is full.", wasConnected: false));
        }

        [Test]
        public void UnknownReason_WhileConnected_IsConnectionLost()
        {
            Assert.AreEqual("Connection lost.", _starter.MessageFor("[Disconnect Event][Client-1] transport", wasConnected: true));
        }

        [Test]
        public void NoReason_NeverConnected_IsCouldNotConnect()
        {
            Assert.AreEqual("Couldn't connect to the host.", _starter.MessageFor(null, wasConnected: false));
        }
    }
}
