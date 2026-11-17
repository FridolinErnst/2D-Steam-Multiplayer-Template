using Game.Atoms;
using Unity.Netcode;
using UnityAtoms.BaseAtoms;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Publishes the number of connected players from the server and mirrors it into the <c>ConnectedPlayers</c>
    /// Atom on every machine (D12: copy on spawn, then follow changes, reset on despawn). Single writer of that Atom.
    /// </summary>
    public sealed class PlayerCountMirror : NetworkBehaviour
    {
        [Required, SerializeField] private IntVariable _connectedPlayers;

        private readonly NetworkVariable<int> _count = new NetworkVariable<int>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.OnConnectionEvent += OnConnectionEvent;
                _count.Value = NetworkManager.ConnectedClientsIds.Count;
            }

            _count.OnValueChanged += OnCountChanged;
            _connectedPlayers.Value = _count.Value;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NetworkManager.OnConnectionEvent -= OnConnectionEvent;
            _count.OnValueChanged -= OnCountChanged;
            _connectedPlayers.Value = 0;
        }

        private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            if (data.EventType != ConnectionEvent.ClientConnected && data.EventType != ConnectionEvent.ClientDisconnected) return;
            _count.Value = manager.ConnectedClientsIds.Count;
        }

        private void OnCountChanged(int previous, int current)
        {
            _connectedPlayers.Value = current;
        }
    }
}
