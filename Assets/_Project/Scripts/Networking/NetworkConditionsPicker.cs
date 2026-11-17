using Game.Atoms;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Networking
{
    /// <summary>
    /// Dev-only tool: a small panel (top right of the Game view) that picks how bad this machine's simulated internet
    /// connection is, and applies it to the Network Simulator live, in or out of a session. F8 cycles profiles
    /// without freeing the cursor. Every Play starts on the default profile, so all windows match. Only affects the direct (Unity
    /// Transport) connection used in the Editor; hidden in release builds, where the simulator does nothing.
    /// </summary>
    public sealed class NetworkConditionsPicker : MonoBehaviour
    {
        [Required, SerializeField] private NetworkSimulator _simulator;

        [Tooltip("Delay, jitter and loss each machine adds to what it sends. Set the same profile on every machine.")]
        [SerializeField] private NetworkConditionProfile[] _profiles =
        {
            new NetworkConditionProfile("Off (same machine)", 0, 0, 0),
            new NetworkConditionProfile("Good day (~50 ms)", 25, 5, 0),
            new NetworkConditionProfile("Typical (~80 ms)", 40, 10, 0),
            new NetworkConditionProfile("Bad Wi-Fi (~140 ms+)", 70, 25, 2),
        };

        [Tooltip("Profile every window starts with when Play begins.")]
        [SerializeField] private int _defaultProfile = 2; // Typical

        [SerializeField] private float _referenceHeight = 720f;

        private int _selected;

        private void Awake()
        {
            if (!Debug.isDebugBuild || _profiles == null || _profiles.Length == 0)
            {
                enabled = false;
                return;
            }
            _selected = Mathf.Clamp(_defaultProfile, 0, _profiles.Length - 1);
        }

        private void Start()
        {
            Apply(_selected);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame) Apply((_selected + 1) % _profiles.Length);
        }

        private void Apply(int index)
        {
            _selected = index;
            NetworkConditionProfile p = _profiles[index];
            _simulator.ConnectionPreset = NetworkSimulatorPreset.Create(p.Name, string.Empty, p.DelayMs, p.JitterMs, 0, p.LossPercent);
        }

        private void OnGUI()
        {
            float scale = Screen.height / _referenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float width = 250f;
            float height = 52f + _profiles.Length * 22f;
            GUILayout.BeginArea(new Rect(Screen.width / scale - width - 12f, 12f, width, height), GUI.skin.box);
            GUILayout.Label("Network conditions (this window) · F8");
            for (int i = 0; i < _profiles.Length; i++)
            {
                bool on = GUILayout.Toggle(i == _selected, _profiles[i].Name);
                if (on && i != _selected) Apply(i);
            }
            GUILayout.Label("Pick the same profile in every window.");
            GUILayout.EndArea();
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
