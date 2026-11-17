using Game.Atoms;
using UnityAtoms.BaseAtoms;
using UnityEngine;

namespace Game.Session
{
    /// <summary>
    /// Pauses the game while the in-game menu is open in a local game; online games never pause. Pausing stops
    /// game time (<c>Time.timeScale</c> = 0), so anything driven by scaled time stops. Single writer of <c>IsPaused</c>
    /// and of <c>Time.timeScale</c>.
    /// </summary>
    public sealed class SessionPause : MonoBehaviour
    {
        [Required, SerializeField] private BoolVariable _isPauseMenuOpen;
        [Required, SerializeField] private BoolVariable _isLocalSession;
        [Required, SerializeField] private BoolVariable _isPaused;

        private void OnEnable()
        {
            _isPauseMenuOpen.Changed.Register(OnChanged);
            _isLocalSession.Changed.Register(OnChanged);
            Refresh();
        }

        private void OnDisable()
        {
            _isPauseMenuOpen.Changed.Unregister(OnChanged);
            _isLocalSession.Changed.Unregister(OnChanged);
            Time.timeScale = 1f; // no Atom write here: listeners may already be gone during unload (AGENTS.md §8)
        }

        private void OnChanged(bool _) => Refresh();

        private void Refresh()
        {
            bool paused = _isPauseMenuOpen.Value && _isLocalSession.Value;
            _isPaused.Value = paused;
            Time.timeScale = paused ? 0f : 1f;
        }
    }
}
