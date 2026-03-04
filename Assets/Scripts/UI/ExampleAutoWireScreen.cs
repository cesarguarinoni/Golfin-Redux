using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.Utilities;

namespace GolfinRedux.UI
{
    /// <summary>
    /// EXAMPLE: How to use UIAutoWire for new UI screens.
    /// Delete this file once you understand the pattern.
    /// </summary>
    public class ExampleAutoWireScreen : MonoBehaviour
    {
        // Option 1: Keep public fields for optional manual override in Inspector
        public GameObject panel;
        public GameObject background;
        public Button closeButton;
        public Button confirmButton;
        public TMPro.TextMeshProUGUI titleText;
        public Image iconImage;

        private void Awake()
        {
            // Auto-wire all components by name convention
            AutoWireComponents();
            
            // Wire button click events
            AutoWireButtons();
        }

        /// <summary>
        /// Auto-find components by GameObject name.
        /// Only wires if the field is null (allows manual override in Inspector).
        /// </summary>
        private void AutoWireComponents()
        {
            // Basic usage: Find by exact name
            if (panel == null)
                panel = UIAutoWire.FindGameObject(transform, "Panel");

            if (background == null)
                background = UIAutoWire.FindGameObject(transform, "Background");

            // Find by path (for nested objects)
            if (closeButton == null)
                closeButton = UIAutoWire.FindComponent<Button>(transform, "Panel/CloseButton");

            if (confirmButton == null)
                confirmButton = UIAutoWire.FindComponent<Button>(transform, "Panel/ConfirmButton");

            if (titleText == null)
                titleText = UIAutoWire.FindComponent<TMPro.TextMeshProUGUI>(transform, "Panel/Title");

            if (iconImage == null)
                iconImage = UIAutoWire.FindComponent<Image>(transform, "Panel/Icon");
        }

        /// <summary>
        /// Auto-wire button onClick listeners.
        /// </summary>
        private void AutoWireButtons()
        {
            // Method 1: Wire buttons individually
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseClicked);

            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);

            // Method 2: Wire multiple buttons at once
            // UIAutoWire.WireButtons(transform, new Dictionary<string, UnityAction>
            // {
            //     { "Panel/CloseButton", OnCloseClicked },
            //     { "Panel/ConfirmButton", OnConfirmClicked }
            // });
        }

        private void OnCloseClicked()
        {
            Debug.Log("Close button clicked");
            if (panel != null)
                panel.SetActive(false);
        }

        private void OnConfirmClicked()
        {
            Debug.Log("Confirm button clicked");
            // Do something
        }
    }

    // ============================================================================
    // ALTERNATIVE APPROACH: Fully auto-wired (no public fields)
    // ============================================================================
    /*
    public class ExampleFullyAutoWired : MonoBehaviour
    {
        // Private fields - cannot be manually overridden
        private GameObject panel;
        private Button closeButton;
        private Button confirmButton;

        private void Awake()
        {
            // Find everything automatically
            panel = UIAutoWire.FindGameObject(transform, "Panel");
            closeButton = UIAutoWire.WireButton(transform, "Panel/CloseButton", OnClose);
            confirmButton = UIAutoWire.WireButton(transform, "Panel/ConfirmButton", OnConfirm);
        }

        private void OnClose() { panel.SetActive(false); }
        private void OnConfirm() { Debug.Log("Confirmed"); }
    }
    */

    // ============================================================================
    // NAMING CONVENTIONS (IMPORTANT!)
    // ============================================================================
    /*
    To use UIAutoWire effectively, follow these GameObject naming conventions:
    
    Hierarchy example:
    MyScreen (has ExampleAutoWireScreen script)
    ├── Background
    └── Panel
        ├── Title (TextMeshProUGUI)
        ├── Icon (Image)
        ├── CloseButton (Button)
        │   └── Label (Text)
        └── ConfirmButton (Button)
            └── Label (Text)
    
    Then in script:
    - FindGameObject(transform, "Panel") → finds Panel
    - FindComponent<Button>(transform, "Panel/CloseButton") → finds CloseButton's Button component
    - FindComponent<TMPro.TextMeshProUGUI>(transform, "Panel/Title") → finds Title's TextMeshProUGUI
    
    Tips:
    - Use descriptive names: "Panel", "Background", "CloseButton" (not "GameObject1")
    - Nest logically: "Panel/CloseButton" is clearer than "CloseButton" at root
    - Consistent suffix: All buttons end with "Button", all texts with "Text", etc.
    */
}
