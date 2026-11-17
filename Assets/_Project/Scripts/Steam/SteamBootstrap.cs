#if !DISABLESTEAMWORKS
using Game.Atoms;
using Game.Data;
using Steamworks;
using UnityAtoms.BaseAtoms;
using UnityEngine;

namespace Game.Steam
{
    /// <summary>
    /// Starts and stops the Steam API for this process and pumps its callbacks every frame. Single writer of
    /// <c>IsSteamRunning</c> and <c>LocalPlayerName</c>. Replaces the Steamworks.NET SteamManager singleton (D3).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class SteamBootstrap : MonoBehaviour
    {
        [Required, SerializeField] private SessionSettings _settings;
        [Required, SerializeField] private BoolVariable _isSteamRunning;
        [Required, SerializeField] private StringVariable _localPlayerName;

        private bool _initialized;

        private void Awake()
        {
            _isSteamRunning.Value = false;
            _localPlayerName.Value = "Player";
            if (!_settings.UseSteam) return;

            if (!Application.isEditor && SteamAPI.RestartAppIfNecessary(new AppId_t(_settings.SteamAppId)))
            {
                Application.Quit();
                return;
            }

            ESteamAPIInitResult result = SteamAPI.InitEx(out string error);
            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
            {
                Debug.LogWarning($"{nameof(SteamBootstrap)}: Steam not available ({result}: {error}). Using direct connection.", this);
                return;
            }

            _initialized = true;
            SteamNetworkingUtils.InitRelayNetworkAccess();
            _localPlayerName.Value = SteamFriends.GetPersonaName();
            _isSteamRunning.Value = true;
        }

        private void Update()
        {
            if (_initialized) SteamAPI.RunCallbacks();
        }

        private void OnDestroy()
        {
            // No Atom writes here: listeners may already be destroyed during unload (AGENTS.md §8).
            if (!_initialized) return;
            SteamAPI.Shutdown();
            _initialized = false;
        }
    }
}
#endif
