#if !DISABLESTEAMWORKS
using Game.Atoms;
using Game.Data;
using Steamworks;
using Unity.Netcode;
using UnityAtoms;
using UnityAtoms.BaseAtoms;
using UnityEngine;

namespace Game.Steam
{
    /// <summary>
    /// Connects Steam lobbies to NGO sessions: creates a friends-only lobby when this player hosts over Steam, opens
    /// the invite overlay and the friends list (to join a friend's game), and when a friend's invite is accepted or
    /// "Join Game" is picked in Steam (in game or via <c>+connect_lobby</c>) joins the lobby, points the transport at
    /// its owner and raises <c>JoinRequested</c>.
    /// </summary>
    public sealed class SteamLobby : MonoBehaviour
    {
        private const string HostKey = "host";

        [Required, SerializeField] private NetworkManager _networkManager;
        [Required, SerializeField] private SteamSocketsTransport _transport;
        [Required, SerializeField] private SessionSettings _settings;
        [Required, SerializeField] private BoolReference _isSteamRunning;
        [Required, SerializeField] private VoidBaseEventReference _inviteRequested;
        [Required, SerializeField] private VoidBaseEventReference _joinFriendRequested;
        [Required, SerializeField] private VoidEvent _joinRequested;

        private Callback<GameLobbyJoinRequested_t> _lobbyJoinRequested;
        private Callback<LobbyEnter_t> _lobbyEntered;
        private CallResult<LobbyCreated_t> _lobbyCreated;
        private CSteamID _lobby = CSteamID.Nil;

        private bool SteamActive => _isSteamRunning.Value && _networkManager.NetworkConfig.NetworkTransport == _transport;

        private void OnEnable()
        {
            _inviteRequested.Event.Register(OnInviteRequested, false);
            _joinFriendRequested.Event.Register(OnJoinFriendRequested, false);
            _networkManager.OnServerStarted += OnServerStarted;
            _networkManager.OnClientStopped += OnSessionStopped;
            if (!_isSteamRunning.Value) return;

            _lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
        }

        private void OnDisable()
        {
            _inviteRequested.Event.Unregister(OnInviteRequested);
            _joinFriendRequested.Event.Unregister(OnJoinFriendRequested);
            _networkManager.OnServerStarted -= OnServerStarted;
            _networkManager.OnClientStopped -= OnSessionStopped;
            _lobbyJoinRequested?.Dispose();
            _lobbyEntered?.Dispose();
            _lobbyCreated?.Dispose();
            _lobbyJoinRequested = null;
            _lobbyEntered = null;
            _lobbyCreated = null;
        }

        private void Start()
        {
            if (!SteamActive) return;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "+connect_lobby" || !ulong.TryParse(args[i + 1], out ulong lobbyId)) continue;
                SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
                return;
            }
        }

        private void OnServerStarted()
        {
            if (!SteamActive) return;
            SteamAPICall_t call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _settings.MaxPlayers);
            _lobbyCreated.Set(call);
        }

        private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"{nameof(SteamLobby)}: lobby creation failed ({result.m_eResult}). Invites won't work.", this);
                return;
            }

            _lobby = new CSteamID(result.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(_lobby, HostKey, SteamUser.GetSteamID().m_SteamID.ToString());
            SetPresence();
        }

        private void OnInviteRequested(UnityAtoms.Void _)
        {
            if (!SteamActive || _lobby == CSteamID.Nil) return;
            SteamFriends.ActivateGameOverlayInviteDialog(_lobby);
        }

        private void OnJoinFriendRequested(UnityAtoms.Void _)
        {
            if (!_isSteamRunning.Value) return;
            SteamFriends.ActivateGameOverlay("friends");
        }

        private void OnLobbyJoinRequested(GameLobbyJoinRequested_t request)
        {
            if (_networkManager.IsListening) return; // already in a session
            SteamMatchmaking.JoinLobby(request.m_steamIDLobby);
        }

        private void OnLobbyEntered(LobbyEnter_t entered)
        {
            var lobby = new CSteamID(entered.m_ulSteamIDLobby);
            CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobby);
            if (owner == SteamUser.GetSteamID()) return; // the host's own lobby

            _lobby = lobby;
            _transport.HostSteamId = owner.m_SteamID;
            SetPresence();
            _joinRequested.Raise();
        }

        private void OnSessionStopped(bool wasHost)
        {
            if (!_isSteamRunning.Value) return;
            if (_lobby != CSteamID.Nil) SteamMatchmaking.LeaveLobby(_lobby);
            _lobby = CSteamID.Nil;
            _transport.HostSteamId = 0;
            SteamFriends.ClearRichPresence();
        }

        private void SetPresence()
        {
            string lobby = _lobby.m_SteamID.ToString();
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + lobby);
            SteamFriends.SetRichPresence("steam_player_group", lobby);
        }
    }
}
#endif
