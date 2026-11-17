using UnityAtoms.BaseAtoms;
using UnityEngine;

namespace Game.Atoms
{
    /// <summary>Keeps this GameObject in a runtime set while it is enabled, so others find it without searching the scene.</summary>
    public sealed class RegisterInList : MonoBehaviour
    {
        [Required, SerializeField] private GameObjectValueList _list;

        private void OnEnable() => _list.Add(gameObject);
        private void OnDisable() => _list.Remove(gameObject);
    }
}
