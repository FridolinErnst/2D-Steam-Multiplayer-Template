#if !DISABLESTEAMWORKS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Game.Core.Net;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace Game.Steam
{
    /// <summary>
    /// NGO transport over Steam networking sockets (P2P through Valve's relay). The server listens on virtual port 0;
    /// a client connects to <see cref="HostSteamId"/>. Maps Steam connections to NGO client ids and copies received
    /// messages into a reused buffer, releasing each native message immediately.
    /// </summary>
    public sealed class SteamSocketsTransport : NetworkTransport
    {
        private const int MaxMessagesPerReceive = 64;

        private readonly TransportIdMap<HSteamNetConnection> _ids = new TransportIdMap<HSteamNetConnection>();
        private readonly Queue<(NetworkEvent Type, ulong ClientId)> _events = new Queue<(NetworkEvent, ulong)>();
        private readonly IntPtr[] _received = new IntPtr[MaxMessagesPerReceive];
        private readonly List<HSteamNetConnection> _scratch = new List<HSteamNetConnection>();

        private Callback<SteamNetConnectionStatusChangedCallback_t> _statusChanged;
        private HSteamListenSocket _listenSocket = HSteamListenSocket.Invalid;
        private HSteamNetPollGroup _pollGroup = HSteamNetPollGroup.Invalid;
        private HSteamNetConnection _serverConnection = HSteamNetConnection.Invalid;
        private bool _isServer;

        private int _receivedCount;
        private int _receivedIndex;
        private byte[] _receiveBuffer = new byte[1500];
        private IntPtr _sendBuffer = IntPtr.Zero;
        private int _sendBufferSize;

        /// <summary>Steam id of the host to connect to. Set by the lobby before starting a client.</summary>
        public ulong HostSteamId { get; set; }

        /// <inheritdoc />
        public override ulong ServerClientId => 0;

        /// <inheritdoc />
        public override void Initialize(NetworkManager networkManager = null)
        {
            if (_statusChanged == null)
                _statusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
        }

        /// <inheritdoc />
        public override bool StartServer()
        {
            _isServer = true;
            _listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
            _pollGroup = SteamNetworkingSockets.CreatePollGroup();
            return _listenSocket != HSteamListenSocket.Invalid && _pollGroup != HSteamNetPollGroup.Invalid;
        }

        /// <inheritdoc />
        public override bool StartClient()
        {
            if (HostSteamId == 0)
            {
                Debug.LogError($"{nameof(SteamSocketsTransport)}: no host Steam id set; join through a Steam lobby.", this);
                return false;
            }

            _isServer = false;
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(HostSteamId));
            _serverConnection = SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0, null);
            return _serverConnection != HSteamNetConnection.Invalid;
        }

        /// <inheritdoc />
        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            if (!TryGetConnection(clientId, out HSteamNetConnection connection) || payload.Count == 0) return;

            EnsureSendBuffer(payload.Count);
            Marshal.Copy(payload.Array, payload.Offset, _sendBuffer, payload.Count);
            SteamNetworkingSockets.SendMessageToConnection(connection, _sendBuffer, (uint)payload.Count, SendFlags(networkDelivery), out _);
        }

        /// <inheritdoc />
        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            receiveTime = Time.realtimeSinceStartup;
            payload = default;

            if (_events.Count > 0)
            {
                (NetworkEvent type, ulong id) = _events.Dequeue();
                clientId = id;
                return type;
            }

            if (_receivedIndex >= _receivedCount && !ReceiveBatch())
            {
                clientId = 0;
                return NetworkEvent.Nothing;
            }

            IntPtr pointer = _received[_receivedIndex++];
            SteamNetworkingMessage_t message = SteamNetworkingMessage_t.FromIntPtr(pointer);
            int size = message.m_cbSize;
            if (_receiveBuffer.Length < size) _receiveBuffer = new byte[Mathf.NextPowerOfTwo(size)];
            Marshal.Copy(message.m_pData, _receiveBuffer, 0, size);
            HSteamNetConnection connection = message.m_conn;
            SteamNetworkingMessage_t.Release(pointer);

            if (!_isServer) clientId = ServerClientId;
            else if (!_ids.TryGetId(connection, out clientId)) return NetworkEvent.Nothing;

            payload = new ArraySegment<byte>(_receiveBuffer, 0, size);
            return NetworkEvent.Data;
        }

        /// <inheritdoc />
        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (!_ids.TryGetHandle(clientId, out HSteamNetConnection connection)) return;
            _ids.Remove(connection);
            // Linger, so reliable messages already sent (such as NGO's disconnect reason) still arrive.
            SteamNetworkingSockets.CloseConnection(connection, 0, "Disconnected by host", true);
        }

        /// <inheritdoc />
        public override void DisconnectLocalClient()
        {
            if (_serverConnection == HSteamNetConnection.Invalid) return;
            SteamNetworkingSockets.CloseConnection(_serverConnection, 0, "Left", false);
            _serverConnection = HSteamNetConnection.Invalid;
        }

        /// <inheritdoc />
        public override ulong GetCurrentRtt(ulong clientId)
        {
            if (!TryGetConnection(clientId, out HSteamNetConnection connection)) return 0;
            var status = new SteamNetConnectionRealTimeStatus_t();
            var lanes = new SteamNetConnectionRealTimeLaneStatus_t();
            SteamNetworkingSockets.GetConnectionRealTimeStatus(connection, ref status, 0, ref lanes);
            return (ulong)Mathf.Max(0, status.m_nPing);
        }

        /// <inheritdoc />
        public override void Shutdown()
        {
            _ids.CopyHandles(_scratch);
            foreach (HSteamNetConnection connection in _scratch)
                SteamNetworkingSockets.CloseConnection(connection, 0, "Shutdown", false);
            _ids.Clear();
            DisconnectLocalClient();

            if (_listenSocket != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(_listenSocket);
            if (_pollGroup != HSteamNetPollGroup.Invalid) SteamNetworkingSockets.DestroyPollGroup(_pollGroup);
            _listenSocket = HSteamListenSocket.Invalid;
            _pollGroup = HSteamNetPollGroup.Invalid;

            ReleaseUnread();
            _events.Clear();
            _isServer = false;
            _statusChanged?.Dispose();
            _statusChanged = null;
        }

        private void OnDestroy()
        {
            if (_sendBuffer == IntPtr.Zero) return;
            Marshal.FreeHGlobal(_sendBuffer);
            _sendBuffer = IntPtr.Zero;
            _sendBufferSize = 0;
        }

        // ---------------------------------------------------------------- internals

        private void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t data)
        {
            HSteamNetConnection connection = data.m_hConn;
            switch (data.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    if (_isServer && data.m_info.m_hListenSocket == _listenSocket &&
                        SteamNetworkingSockets.AcceptConnection(connection) != EResult.k_EResultOK)
                        SteamNetworkingSockets.CloseConnection(connection, 0, "Accept failed", false);
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (_isServer)
                    {
                        SteamNetworkingSockets.SetConnectionPollGroup(connection, _pollGroup);
                        _events.Enqueue((NetworkEvent.Connect, _ids.GetOrAdd(connection)));
                    }
                    else if (connection == _serverConnection)
                    {
                        _events.Enqueue((NetworkEvent.Connect, ServerClientId));
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    if (_isServer)
                    {
                        if (_ids.TryGetId(connection, out ulong id))
                        {
                            _ids.Remove(connection);
                            _events.Enqueue((NetworkEvent.Disconnect, id));
                        }
                    }
                    else if (connection == _serverConnection)
                    {
                        _serverConnection = HSteamNetConnection.Invalid;
                        _events.Enqueue((NetworkEvent.Disconnect, ServerClientId));
                    }
                    SteamNetworkingSockets.CloseConnection(connection, 0, null, false);
                    break;
            }
        }

        private bool ReceiveBatch()
        {
            _receivedIndex = 0;
            if (_isServer && _pollGroup != HSteamNetPollGroup.Invalid)
                _receivedCount = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(_pollGroup, _received, MaxMessagesPerReceive);
            else if (!_isServer && _serverConnection != HSteamNetConnection.Invalid)
                _receivedCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(_serverConnection, _received, MaxMessagesPerReceive);
            else
                _receivedCount = 0;

            if (_receivedCount < 0) _receivedCount = 0;
            return _receivedCount > 0;
        }

        private void ReleaseUnread()
        {
            for (int i = _receivedIndex; i < _receivedCount; i++) SteamNetworkingMessage_t.Release(_received[i]);
            _receivedIndex = 0;
            _receivedCount = 0;
        }

        private bool TryGetConnection(ulong clientId, out HSteamNetConnection connection)
        {
            if (!_isServer)
            {
                connection = _serverConnection;
                return connection != HSteamNetConnection.Invalid;
            }
            return _ids.TryGetHandle(clientId, out connection);
        }

        private void EnsureSendBuffer(int size)
        {
            if (_sendBufferSize >= size) return;
            if (_sendBuffer != IntPtr.Zero) Marshal.FreeHGlobal(_sendBuffer);
            _sendBufferSize = Mathf.NextPowerOfTwo(size);
            _sendBuffer = Marshal.AllocHGlobal(_sendBufferSize);
        }

        private static int SendFlags(NetworkDelivery delivery)
        {
            switch (delivery)
            {
                case NetworkDelivery.Unreliable:
                case NetworkDelivery.UnreliableSequenced:
                    return Constants.k_nSteamNetworkingSend_UnreliableNoNagle;
                default:
                    return Constants.k_nSteamNetworkingSend_ReliableNoNagle;
            }
        }
    }
}
#endif
