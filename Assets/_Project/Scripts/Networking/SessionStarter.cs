using Game.Atoms;
using Game.Data;
using Unity.Netcode;
using UnityAtoms.BaseAtoms;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Starts and stops the local NGO session on request (host online, join, play local, leave), approves connections
    /// up to the player limit (a local session accepts nobody else) and places new players at the spawn point. When a
    /// session ends without the local player leaving, it says why in <c>DisconnectMessage</c>. Single writer of
    /// <c>IsSessionActive</c>, <c>IsLocalSession</c>, <c>IsConnected</c> and <c>DisconnectMessage</c>.
    /// </summary>
    public sealed class SessionStarter : MonoBehaviour
    {
        [Required, SerializeField] private NetworkManager _networkManager;
        [Required, SerializeField] private SessionSettings _settings;
        [Required, SerializeField] private Transform _spawnPoint;
        [SerializeField] private float _spawnSpacing = 1.5f;

        [Required, SerializeField] private VoidBaseEventReference _hostRequested;
        [Required, SerializeField] private VoidBaseEventReference _joinRequested;
        [Required, SerializeField] private VoidBaseEventReference _localRequested;
        [Required, SerializeField] private VoidBaseEventReference _leaveRequested;

        [Required, SerializeField] private BoolVariable _isSessionActive;
        [Tooltip("True while the session is a local (same-screen) game. Set before the session starts so the transport follows it.")]
        [Required, SerializeField] private BoolVariable _isLocalSession;
        [Tooltip("True once this machine is connected and approved (right away for a host).")]
        [Required, SerializeField] private BoolVariable _isConnected;
        [Tooltip("Why the last session ended, shown in the main menu. Empty when the player left on purpose.")]
        [Required, SerializeField] private StringVariable _disconnectMessage;

        [Header("Messages")]
        [SerializeField] private string _hostLeftMessage = "Host left the game.";
        [SerializeField] private string _sessionFullMessage = "The game is full.";
        [SerializeField] private string _connectionLostMessage = "Connection lost.";
        [SerializeField] private string _couldNotConnectMessage = "Couldn't connect to the host.";

        private bool _leaving;

        private void OnEnable()
        {
            _isSessionActive.Value = false;
            _isLocalSession.Value = false;
            _isConnected.Value = false;
            _hostRequested.Event.Register(OnHostRequested, false);
            _joinRequested.Event.Register(OnJoinRequested, false);
            _localRequested.Event.Register(OnLocalRequested, false);
            _leaveRequested.Event.Register(OnLeaveRequested, false);
            _networkManager.ConnectionApprovalCallback = Approve;
            _networkManager.OnClientConnectedCallback += OnClientConnected;
            _networkManager.OnClientStopped += OnSessionStopped;
        }

        private void OnDisable()
        {
            _hostRequested.Event.Unregister(OnHostRequested);
            _joinRequested.Event.Unregister(OnJoinRequested);
            _localRequested.Event.Unregister(OnLocalRequested);
            _leaveRequested.Event.Unregister(OnLeaveRequested);
            if (_networkManager == null) return;
            _networkManager.ConnectionApprovalCallback = null;
            _networkManager.OnClientConnectedCallback -= OnClientConnected;
            _networkManager.OnClientStopped -= OnSessionStopped;
        }

        private void OnHostRequested(UnityAtoms.Void _) => Begin(local: false, host: true);

        private void OnJoinRequested(UnityAtoms.Void _) => Begin(local: false, host: false);

        private void OnLocalRequested(UnityAtoms.Void _) => Begin(local: true, host: true);

        private void Begin(bool local, bool host)
        {
            if (_networkManager.IsListening || _networkManager.ShutdownInProgress) return;
            _leaving = false;
            _disconnectMessage.Value = string.Empty;
            _isLocalSession.Value = local; // the transport selector switches transports on this, synchronously
            _networkManager.NetworkConfig.ConnectionApproval = true;
            bool started = host ? _networkManager.StartHost() : _networkManager.StartClient();
            _isSessionActive.Value = started;
            if (!started) _isLocalSession.Value = false;
        }

        private void OnLeaveRequested(UnityAtoms.Void _)
        {
            if (!_networkManager.IsListening) return;
            _leaving = true;
            if (_networkManager.IsServer)
            {
                // Tell everyone else why first; NGO flushes the reason before it drops each client.
                var clients = new ulong[_networkManager.ConnectedClientsIds.Count];
                for (int i = 0; i < clients.Length; i++) clients[i] = _networkManager.ConnectedClientsIds[i];
                foreach (ulong client in clients)
                    if (client != NetworkManager.ServerClientId) _networkManager.DisconnectClient(client, _hostLeftMessage);
            }
            _networkManager.Shutdown();
            _isSessionActive.Value = false; // the rest is reset once NGO has finished shutting down (OnSessionStopped)
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId == _networkManager.LocalClientId) _isConnected.Value = true;
        }

        private void OnSessionStopped(bool wasHost)
        {
            if (!_leaving && !wasHost) _disconnectMessage.Value = MessageFor(_networkManager.DisconnectReason, _isConnected.Value);
            _leaving = false;
            _isSessionActive.Value = false;
            _isConnected.Value = false;
            _isLocalSession.Value = false;
        }

        /// <summary>Menu message for a session that ended by itself: the host's reason if it is one we know, else what went wrong.</summary>
        internal string MessageFor(string reason, bool wasConnected)
        {
            if (reason == _hostLeftMessage || reason == _sessionFullMessage) return reason;
            return wasConnected ? _connectionLostMessage : _couldNotConnectMessage;
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool isHostItself = request.ClientNetworkId == NetworkManager.ServerClientId;
            int players = _networkManager.ConnectedClientsIds.Count;
            response.Approved = isHostItself || (!_isLocalSession.Value && players < _settings.MaxPlayers);
            response.Reason = response.Approved ? null : _sessionFullMessage;
            response.CreatePlayerObject = response.Approved && _networkManager.NetworkConfig.PlayerPrefab != null;
            response.Position = _spawnPoint.position + _spawnPoint.right * (players * _spawnSpacing);
            response.Rotation = _spawnPoint.rotation;
        }
    }
}
