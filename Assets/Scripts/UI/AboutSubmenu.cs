using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using GolfinRedux.UI.BuildInfo;

namespace Golfin.UI
{
    /// <summary>
    /// About submenu showing app version and license information.
    /// Displayed as an expandable accordion item (not a modal).
    /// </summary>
    public class AboutSubmenu : MonoBehaviour
    {
        [Header("Version Info")]
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private TextMeshProUGUI licensesText;
        
        [Header("Settings")]
        [SerializeField] private string appName = "GOLFIN Redux";
        [SerializeField] private bool useApplicationVersion = true;
        [SerializeField] private string fallbackVersion = "1.0.0";

        private void Start()
        {
            UpdateContent();
        }

        private void OnEnable()
        {
            // Refresh content when submenu is shown
            UpdateContent();
        }

        /// <summary>
        /// Update the submenu content with current version and licenses.
        /// </summary>
        private void UpdateContent()
        {
            // Update version text
            if (versionText != null)
            {
                string version = GetVersionString();
                versionText.text = $"{appName}\nVersion {version}";
            }
            
            // Update licenses text
            if (licensesText != null)
            {
                licensesText.text = GetLicensesText();
            }

            ResizeToContent();
        }

        /// <summary>
        /// Size each text slot to the text it actually holds, then tell the parent accordion item
        /// how tall to open.
        ///
        /// The version string and licence list are built here at runtime (and the section headings
        /// are localised), so the height authored in the scene is only ever a rough guess — baking
        /// it clipped the licence list on some builds and left dead space on others.
        /// </summary>
        private void ResizeToContent()
        {
            var container = (RectTransform)transform;
            var layout = GetComponent<VerticalLayoutGroup>();
            if (layout == null) return;

            float width = container.rect.width - layout.padding.left - layout.padding.right;
            float total = layout.padding.top + layout.padding.bottom;
            int shown = 0;

            foreach (RectTransform child in container)
            {
                if (!child.gameObject.activeSelf) continue;

                var element = child.GetComponent<LayoutElement>();
                if (element == null) continue;

                var label = child.GetComponent<TextMeshProUGUI>();
                if (label != null && !string.IsNullOrEmpty(label.text))
                {
                    float needed = label.GetPreferredValues(label.text, width, 0f).y;
                    element.preferredHeight = Mathf.Max(element.minHeight, Mathf.Ceil(needed) + 8f);
                }

                total += element.preferredHeight;
                shown++;
            }

            if (shown > 1) total += layout.spacing * (shown - 1);

            container.sizeDelta = new Vector2(container.sizeDelta.x, total);

            var item = GetComponentInParent<SettingsMenuItem>();
            if (item != null) item.SetSubmenuHeight(total);
        }

        /// <summary>
        /// Get the current app version string.
        /// Uses the FULL version — marketing version plus build number, e.g.
        /// "0.1.0 (2201)" — since the build number is what identifies a specific
        /// binary in TestFlight / App Store Connect. See <see cref="AppVersion"/>;
        /// it degrades to the marketing version alone when no build number is baked.
        /// </summary>
        private string GetVersionString()
        {
            if (useApplicationVersion)
            {
                return AppVersion.Full;
            }
            return fallbackVersion;
        }

        /// <summary>
        /// Get the licenses text.
        /// Simple list format matching reference design.
        /// </summary>
        private string GetLicensesText()
        {
            var sb = new StringBuilder();
            
            // Simple license list (matching reference image format)
            sb.AppendLine("MIT License");
            sb.AppendLine("GPL (GNU General Public License)");
            sb.AppendLine("Apache License 2.0");
            sb.AppendLine("BSD License");
            sb.AppendLine();
            sb.Append($"© 2024-{System.DateTime.Now.Year} Wonderwall Inc.");
            
            return sb.ToString();
        }

        /// <summary>
        /// Set custom version text (useful for testing or special builds).
        /// </summary>
        public void SetVersion(string version)
        {
            fallbackVersion = version;
            useApplicationVersion = false;
            UpdateContent();
        }

        /// <summary>
        /// Set custom licenses text.
        /// </summary>
        public void SetLicenses(string licenses)
        {
            if (licensesText != null)
            {
                licensesText.text = licenses;
            }
        }

        /// <summary>
        /// Load licenses from a text file (optional advanced feature).
        /// </summary>
        public void LoadLicensesFromFile(string filePath)
        {
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    string content = System.IO.File.ReadAllText(filePath);
                    SetLicenses(content);
                    Debug.Log($"[AboutSubmenu] Loaded licenses from {filePath}");
                }
                else
                {
                    Debug.LogWarning($"[AboutSubmenu] License file not found: {filePath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AboutSubmenu] Failed to load licenses: {e.Message}");
            }
        }
    }
}
