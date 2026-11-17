using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Session-wide network configuration: Steam app id, player limit, and which transport to use where.
    /// The single place to change 480 → the real app id, or 6 → more players.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Session Settings")]
    public sealed class SessionSettings : ScriptableObject
    {
        [Tooltip("Steam app id. 480 (Spacewar) until the game has its own.")]
        [SerializeField] private uint _steamAppId = 480;

        [Tooltip("Most players in one session, host included.")]
        [SerializeField, Min(1)] private int _maxPlayers = 6;

        [Tooltip("Use the Steam transport in builds when Steam is running.")]
        [SerializeField] private bool _useSteamInBuilds = true;

        [Tooltip("Use the Steam transport in the Editor. Off by default so Multiplayer Play Mode uses localhost.")]
        [SerializeField] private bool _useSteamInEditor;

        [Tooltip("Address clients connect to with Unity Transport (no Steam).")]
        [SerializeField] private string _directAddress = "127.0.0.1";

        [Tooltip("Port for Unity Transport (no Steam).")]
        [SerializeField] private ushort _directPort = 7777;

        /// <summary>Steam app id.</summary>
        public uint SteamAppId => _steamAppId;

        /// <summary>Most players in one session, host included.</summary>
        public int MaxPlayers => _maxPlayers;

        /// <summary>Whether the Steam transport should be used in the current context (Editor or build).</summary>
        public bool UseSteam => Application.isEditor ? _useSteamInEditor : _useSteamInBuilds;

        /// <summary>Address for Unity Transport.</summary>
        public string DirectAddress => _directAddress;

        /// <summary>Port for Unity Transport.</summary>
        public ushort DirectPort => _directPort;
    }
}
