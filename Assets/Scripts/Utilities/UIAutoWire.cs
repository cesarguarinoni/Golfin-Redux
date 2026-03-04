using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.Utilities
{
    /// <summary>
    /// Utility for auto-wiring UI components by name convention.
    /// Use this to avoid manual Inspector dragging for new UI screens.
    /// </summary>
    public static class UIAutoWire
    {
        /// <summary>
        /// Find a child GameObject by path.
        /// Example: FindGameObject(transform, "Panel/SubPanel/Background")
        /// </summary>
        public static GameObject FindGameObject(Transform root, string path)
        {
            Transform found = root.Find(path);
            if (found == null)
            {
                Debug.LogWarning($"[UIAutoWire] GameObject not found: {path} under {root.name}");
                return null;
            }
            return found.gameObject;
        }

        /// <summary>
        /// Find a child Component by path.
        /// Example: FindComponent<Button>(transform, "Panel/CloseButton")
        /// </summary>
        public static T FindComponent<T>(Transform root, string path) where T : Component
        {
            Transform found = root.Find(path);
            if (found == null)
            {
                Debug.LogWarning($"[UIAutoWire] GameObject not found: {path} under {root.name}");
                return null;
            }

            T component = found.GetComponent<T>();
            if (component == null)
            {
                Debug.LogWarning($"[UIAutoWire] Component {typeof(T).Name} not found on: {path}");
                return null;
            }

            return component;
        }

        /// <summary>
        /// Try to find a GameObject, returns true if found.
        /// Safe version that doesn't log warnings.
        /// </summary>
        public static bool TryFindGameObject(Transform root, string path, out GameObject result)
        {
            Transform found = root.Find(path);
            result = found?.gameObject;
            return found != null;
        }

        /// <summary>
        /// Try to find a Component, returns true if found.
        /// Safe version that doesn't log warnings.
        /// </summary>
        public static bool TryFindComponent<T>(Transform root, string path, out T result) where T : Component
        {
            Transform found = root.Find(path);
            if (found == null)
            {
                result = null;
                return false;
            }

            result = found.GetComponent<T>();
            return result != null;
        }

        /// <summary>
        /// Find all child components of a specific type (non-recursive).
        /// Example: FindComponents<Button>(transform, "ButtonContainer")
        /// </summary>
        public static T[] FindComponents<T>(Transform root, string parentPath = null) where T : Component
        {
            Transform parent = string.IsNullOrEmpty(parentPath) ? root : root.Find(parentPath);
            if (parent == null)
            {
                Debug.LogWarning($"[UIAutoWire] Parent not found: {parentPath} under {root.name}");
                return new T[0];
            }

            return parent.GetComponentsInChildren<T>(true);
        }

        /// <summary>
        /// Auto-wire a Button's onClick listener.
        /// Example: WireButton(transform, "CloseButton", OnCloseClicked)
        /// </summary>
        public static Button WireButton(Transform root, string path, UnityEngine.Events.UnityAction callback)
        {
            Button button = FindComponent<Button>(root, path);
            if (button != null)
            {
                button.onClick.AddListener(callback);
            }
            return button;
        }

        /// <summary>
        /// Auto-wire multiple buttons at once.
        /// Example:
        /// WireButtons(transform, new Dictionary<string, UnityAction>
        /// {
        ///     { "Panel/CloseButton", OnClose },
        ///     { "Panel/ConfirmButton", OnConfirm }
        /// });
        /// </summary>
        public static void WireButtons(Transform root, System.Collections.Generic.Dictionary<string, UnityEngine.Events.UnityAction> buttonMap)
        {
            foreach (var kvp in buttonMap)
            {
                WireButton(root, kvp.Key, kvp.Value);
            }
        }
    }
}
