using UnityEngine;
using TMPro;
using System.Text;

namespace Golfin.UI.Modals
{
    /// <summary>
    /// About modal showing app version and license information.
    /// </summary>
    public class AboutModal : ModalController
    {
        [Header("About Content")]
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private TextMeshProUGUI licensesText;
        
        [Header("Version Info")]
        [SerializeField] private string appName = "GOLFIN Redux";
        [SerializeField] private bool useApplicationVersion = true;
        [SerializeField] private string fallbackVersion = "1.0.0";

        protected override void Awake()
        {
            base.Awake();
            
            // Initialize content
            UpdateContent();
        }

        /// <summary>
        /// Update the modal content with current version and licenses.
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
        /// For now, hardcoded. Can be loaded from a file later.
        /// </summary>
        private string GetLicensesText()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("Third-Party Licenses:");
            sb.AppendLine();
            
            // Unity
            sb.AppendLine("• Unity Technologies");
            sb.AppendLine("  © Unity Technologies");
            sb.AppendLine();
            
            // TextMeshPro
            sb.AppendLine("• TextMesh Pro");
            sb.AppendLine("  © Unity Technologies");
            sb.AppendLine();
            
            // Add your actual licenses here
            sb.AppendLine("• [Other libraries as needed]");
            sb.AppendLine();
            
            sb.AppendLine("This application is provided as-is.");
            sb.AppendLine($"© 2024-{System.DateTime.Now.Year} Wonderwall Inc.");
            
            return sb.ToString();
        }

        protected override void OnShow()
        {
            base.OnShow();
            
            // Refresh content every time modal opens
            UpdateContent();
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
                    Debug.Log($"[AboutModal] Loaded licenses from {filePath}");
                }
                else
                {
                    Debug.LogWarning($"[AboutModal] License file not found: {filePath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AboutModal] Failed to load licenses: {e.Message}");
            }
        }
    }
}
