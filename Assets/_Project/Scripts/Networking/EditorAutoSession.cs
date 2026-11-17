using System.Collections;
using Game.Atoms;
using UnityAtoms.BaseAtoms;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Editor-only convenience for Multiplayer Play Mode: when switched on, the main Editor hosts and every virtual
    /// player (clone) joins as soon as Play Mode starts, skipping the main menu, retrying until connected. Raises the
    /// same request events as the main menu, so it never touches networking code. Does nothing in builds or after the
    /// first successful connection (Leave stays left). Off by default so Play shows the main menu.
    /// </summary>
    public sealed class EditorAutoSession : MonoBehaviour
    {
        [SerializeField] private bool _autoStart;
        [SerializeField] private float _joinRetrySeconds = 1.5f;
        [SerializeField] private int _maxJoinAttempts = 20;

        [Required, SerializeField] private VoidEvent _hostRequested;
        [Required, SerializeField] private VoidEvent _joinRequested;
        [Required, SerializeField] private BoolVariable _isSessionActive;
        [Required, SerializeField] private BoolVariable _isConnected;

        private bool _done;

#if UNITY_EDITOR
        private void Start()
        {
            if (!_autoStart) return;

            if (Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor)
            {
                _done = true;
                _hostRequested.Raise();
                return;
            }

            _isConnected.Changed.Register(OnConnectedChanged);
            StartCoroutine(JoinUntilConnected());
        }

        private void OnDestroy()
        {
            _isConnected.Changed.Unregister(OnConnectedChanged);
        }

        private void OnConnectedChanged(bool connected)
        {
            if (!connected) return;
            _done = true;
            _isConnected.Changed.Unregister(OnConnectedChanged);
        }

        private IEnumerator JoinUntilConnected()
        {
            var wait = new WaitForSeconds(_joinRetrySeconds);
            yield return wait; // give the main Editor a moment to start hosting

            for (int attempt = 0; attempt < _maxJoinAttempts && !_done; attempt++)
            {
                if (!_isSessionActive.Value) _joinRequested.Raise();
                yield return wait;
            }
        }
#endif
    }
}
