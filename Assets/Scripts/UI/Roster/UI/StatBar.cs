using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster
{
    /// <summary>
    /// Visual component for displaying a character stat (Strength, Club Control, etc.)
    /// Shows icon, label, progress bar, and current/max values
    /// </summary>
    public class StatBar : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image fillBar;
        [SerializeField] private TextMeshProUGUI valueText;
        
        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.6f, 1f, 1f); // Blue
        [SerializeField] private Color criticalColor = new Color(1f, 0.4f, 0.2f, 1f); // Orange/Red
        [SerializeField] private Color maxColor = new Color(0.2f, 1f, 0.4f, 1f); // Green
        
        /// <summary>
        /// Set the stat display values
        /// </summary>
        /// <param name="statName">Name of the stat (e.g., "STRENGTH")</param>
        /// <param name="current">Current stat value</param>
        /// <param name="max">Maximum stat value</param>
        public void SetStat(string statName, int current, int max)
        {
            if (label != null)
                label.text = statName;
            
            if (valueText != null)
                valueText.text = $"{current}/{max}";
            
            if (fillBar != null)
            {
                float fillAmount = max > 0 ? (float)current / max : 0f;
                fillBar.fillAmount = fillAmount;
                
                // Color based on fill percentage
                if (fillAmount >= 1f)
                {
                    fillBar.color = maxColor; // Green when maxed
                }
                else if (fillAmount < 0.33f)
                {
                    fillBar.color = criticalColor; // Orange/Red when critical
                }
                else
                {
                    fillBar.color = normalColor; // Blue for normal
                }
            }
        }
        
        /// <summary>
        /// Set the stat icon sprite
        /// </summary>
        public void SetIcon(Sprite iconSprite)
        {
            if (icon != null && iconSprite != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = true;
            }
        }
        
        /// <summary>
        /// Animate the fill bar to a new value (smooth transition)
        /// </summary>
        public void AnimateTo(int newCurrent, int max, float duration = 0.3f)
        {
            if (fillBar != null)
            {
                float targetFill = max > 0 ? (float)newCurrent / max : 0f;
                StartCoroutine(AnimateFillCoroutine(targetFill, duration));
            }
            
            if (valueText != null)
                valueText.text = $"{newCurrent}/{max}";
        }
        
        private System.Collections.IEnumerator AnimateFillCoroutine(float targetFill, float duration)
        {
            float startFill = fillBar.fillAmount;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                fillBar.fillAmount = Mathf.Lerp(startFill, targetFill, t);
                yield return null;
            }
            
            fillBar.fillAmount = targetFill;
        }
    }
}
