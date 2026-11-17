using Game.Atoms;
using TMPro;
using UnityAtoms.BaseAtoms;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The in-game menu, opened and closed with the Pause action during a session: Resume, Invite Friends (online over
    /// Steam only) and Leave to Main Menu. It only opens and closes; whether the game actually pauses is decided by
    /// <c>SessionPause</c> (local games only). Closes itself when the session ends. Single writer of <c>IsPauseMenuOpen</c>.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [Required, SerializeField] private GameObject _panel;
        [Required, SerializeField] private TMP_Text _title;
        [Required, SerializeField] private Button _resume;
        [Required, SerializeField] private Button _invite;
        [Required, SerializeField] private Button _leave;
        [Required, SerializeField] private InputActionReference _pause;

        [Required, SerializeField] private VoidEvent _inviteRequested;
        [Required, SerializeField] private VoidEvent _leaveRequested;

        [Required, SerializeField] private BoolVariable _isPauseMenuOpen;
        [Required, SerializeField] private BoolVariable _isSessionActive;
        [Required, SerializeField] private BoolReference _isLocalSession;
        [Required, SerializeField] private BoolReference _isUsingSteam;

        [SerializeField] private string _localTitle = "Paused";
        [SerializeField] private string _onlineTitle = "Menu";

        private bool _open;

        private void OnEnable()
        {
            _resume.onClick.AddListener(Close);
            _invite.onClick.AddListener(OnInvite);
            _leave.onClick.AddListener(OnLeave);
            _pause.action.performed += OnPausePressed;
            _pause.action.Enable();
            _isSessionActive.Changed.Register(OnSessionActiveChanged);
            _open = false;
            _isPauseMenuOpen.Value = false;
            _panel.SetActive(false);
        }

        private void OnDisable()
        {
            _resume.onClick.RemoveListener(Close);
            _invite.onClick.RemoveListener(OnInvite);
            _leave.onClick.RemoveListener(OnLeave);
            _pause.action.performed -= OnPausePressed;
            _isSessionActive.Changed.Unregister(OnSessionActiveChanged);
        }

        private void OnPausePressed(InputAction.CallbackContext _) => Toggle();

        /// <summary>Opens the menu during a session, or closes it when open.</summary>
        internal void Toggle()
        {
            if (_open) Close();
            else if (_isSessionActive.Value) Open();
        }

        private void Open()
        {
            bool local = _isLocalSession.Value;
            _title.text = local ? _localTitle : _onlineTitle;
            _invite.gameObject.SetActive(!local && _isUsingSteam.Value);
            _panel.SetActive(true);
            _open = true;
            _isPauseMenuOpen.Value = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_resume.gameObject); // gamepad focus
        }

        private void Close()
        {
            _panel.SetActive(false);
            _open = false;
            _isPauseMenuOpen.Value = false;
        }

        private void OnInvite() => _inviteRequested.Raise();

        private void OnLeave()
        {
            Close();
            _leaveRequested.Raise();
        }

        private void OnSessionActiveChanged(bool active)
        {
            if (!active && _open) Close();
        }
    }
}
