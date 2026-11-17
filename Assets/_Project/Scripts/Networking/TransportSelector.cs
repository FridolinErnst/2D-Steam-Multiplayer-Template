using Game.Atoms;
using Game.Data;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityAtoms.BaseAtoms;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Picks the NGO transport between sessions: a local game uses Unity Transport listening on this machine only; an
    /// online game uses Steam when the settings allow it and Steam is running, otherwise Unity Transport on the direct
    /// address (Editor, Multiplayer Play Mode, no Steam). Follows <c>IsLocalSession</c>, which the session starter sets
    /// just before starting and resets once a session has fully stopped. Single writer of <c>IsUsingSteam</c>.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class TransportSelector : MonoBehaviour
    {
        private const string Loopback = "127.0.0.1";

        [Required, SerializeField] private NetworkManager _networkManager;
        [Required, SerializeField] private UnityTransport _directTransport;
        [Required, SerializeField] private NetworkTransport _steamTransport;
        [Required, SerializeField] private SessionSettings _settings;
        [Required, SerializeField] private BoolReference _isSteamRunning;
        [Required, SerializeField] private BoolVariable _isLocalSession;
        [Required, SerializeField] private BoolVariable _isUsingSteam;

        private void OnEnable()
        {
            _isLocalSession.Changed.Register(Apply);
            Apply(_isLocalSession.Value);
        }

        private void OnDisable()
        {
            _isLocalSession.Changed.Unregister(Apply);
        }

        private void Apply(bool local)
        {
            if (_networkManager.IsListening) return; // never swap transports under a running session

            bool useSteam = !local && _settings.UseSteam && _isSteamRunning.Value;
            _networkManager.NetworkConfig.NetworkTransport = useSteam ? _steamTransport : _directTransport;
            if (local) _directTransport.SetConnectionData(Loopback, _settings.DirectPort, Loopback);
            else if (!useSteam) _directTransport.SetConnectionData(_settings.DirectAddress, _settings.DirectPort);
            _isUsingSteam.Value = useSteam;
        }
    }
}
