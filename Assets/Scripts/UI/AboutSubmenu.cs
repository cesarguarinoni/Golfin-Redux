using UnityEngine;
using TMPro;
using System.Text;

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
        }

        /// <summary>
        /// Get the current app version string.
        /// </summary>
        private string GetVersionString()
        {
            if (useApplicationVersion)
            {
                return Application.version;
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
