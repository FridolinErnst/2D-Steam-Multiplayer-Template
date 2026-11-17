using Game.Atoms;
using TMPro;
using UnityAtoms.BaseAtoms;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The main menu: shown whenever no session is running. Host, Join, Play Local and Quit raise request events
    /// (it never touches networking code); Join opens the Steam friends list when online play uses Steam, otherwise it
    /// joins the direct address (Editor / Multiplayer Play Mode). Shows why the last session ended, if it ended by itself.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [Required, SerializeField] private GameObject _panel;
        [Required, SerializeField] private TMP_Text _title;
        [Required, SerializeField] private TMP_Text _message;
        [Required, SerializeField] private Button _host;
        [Required, SerializeField] private Button _join;
        [Required, SerializeField] private Button _local;
        [Required, SerializeField] private Button _quit;

        [Required, SerializeField] private VoidEvent _hostRequested;
        [Required, SerializeField] private VoidEvent _joinRequested;
        [Required, SerializeField] private VoidEvent _joinFriendRequested;
        [Required, SerializeField] private VoidEvent _localRequested;

        [Required, SerializeField] private BoolVariable _isSessionActive;
        [Required, SerializeField] private BoolReference _isUsingSteam;
        [Required, SerializeField] private StringVariable _disconnectMessage;

        private void Awake()
        {
            _title.text = Application.productName;
        }

        private void OnEnable()
        {
            _host.onClick.AddListener(OnHost);
            _join.onClick.AddListener(OnJoin);
            _local.onClick.AddListener(OnLocal);
            _quit.onClick.AddListener(OnQuit);
            _isSessionActive.Changed.Register(OnSessionActiveChanged);
            _disconnectMessage.Changed.Register(OnMessageChanged);
            OnMessageChanged(_disconnectMessage.Value);
            OnSessionActiveChanged(_isSessionActive.Value);
        }

        private void OnDisable()
        {
            _host.onClick.RemoveListener(OnHost);
            _join.onClick.RemoveListener(OnJoin);
            _local.onClick.RemoveListener(OnLocal);
            _quit.onClick.RemoveListener(OnQuit);
            _isSessionActive.Changed.Unregister(OnSessionActiveChanged);
            _disconnectMessage.Changed.Unregister(OnMessageChanged);
        }

        private void OnSessionActiveChanged(bool active)
        {
            _panel.SetActive(!active);
            if (!active && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_host.gameObject); // gamepad focus
        }

        private void OnMessageChanged(string message)
        {
            _message.text = message;
            _message.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void OnHost() => _hostRequested.Raise();

        private void OnJoin()
        {
            if (_isUsingSteam.Value) _joinFriendRequested.Raise();
            else _joinRequested.Raise();
        }

        private void OnLocal() => _localRequested.Raise();

        private static void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
