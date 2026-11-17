using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Editor.Setup
{
    /// <summary>
    /// Adds the template's own actions to the Player map of the project's input actions asset, if missing:
    /// Pause (Esc / gamepad Start), which opens the in-game menu. Existing actions and bindings are left untouched, so
    /// rebinding in the asset survives. Runs before step 10, which wires Pause into the in-game menu.
    /// </summary>
    [GameSetupStep(2)]
    public static class InputActionsSetup
    {
        private static readonly (string Action, string Path, string Group)[] Bindings =
        {
            ("Pause", "<Keyboard>/escape", "Keyboard&Mouse"),
            ("Pause", "<Gamepad>/start", "Gamepad"),
        };

        [MenuItem("Game/Setup/02 Input Actions")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            string path = SetupUtil.InputActionsPath;
            if (!File.Exists(path)) return $"SKIPPED: {path} not found.";

            InputActionAsset asset = InputActionAsset.FromJson(File.ReadAllText(path));
            try
            {
                InputActionMap map = asset.FindActionMap("Player");
                if (map == null) return "SKIPPED: no 'Player' action map.";

                var log = new System.Text.StringBuilder();
                var existedBefore = new System.Collections.Generic.HashSet<string>();
                foreach ((string actionName, _, _) in Bindings)
                    if (map.FindAction(actionName) != null) existedBefore.Add(actionName);

                foreach ((string actionName, string bindingPath, string group) in Bindings)
                {
                    if (existedBefore.Contains(actionName)) continue; // never touch an action that is already there
                    InputAction action = map.FindAction(actionName) ?? map.AddAction(actionName, InputActionType.Button);
                    action.AddBinding(bindingPath).WithGroup(group);
                    log.Append($"Added Player/{actionName} ({bindingPath}).\n");
                }

                if (log.Length == 0) return "Input actions: nothing to add.";
                File.WriteAllText(path, asset.ToJson());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return log.ToString();
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
