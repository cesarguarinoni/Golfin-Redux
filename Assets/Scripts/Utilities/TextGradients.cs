using TMPro;
using UnityEngine;

namespace Golfin.Utilities
{
    /// <summary>
    /// Reusable TMP vertex gradients for gold/silver text styling.
    /// Used across tabs, filters, settings, and any UI with gradient text.
    /// </summary>
    public static class TextGradients
    {
        // Silver: top #FFFFFF → bottom #818EA1
        public static readonly VertexGradient Silver = new VertexGradient(
            new Color32(255, 255, 255, 255),   // top-left
            new Color32(255, 255, 255, 255),   // top-right
            new Color32(129, 142, 161, 255),   // bottom-left
            new Color32(129, 142, 161, 255)    // bottom-right
        );

        // Gold: top #FCF195 → bottom #BB7F1D
        public static readonly VertexGradient Gold = new VertexGradient(
            new Color32(252, 241, 149, 255),
            new Color32(252, 241, 149, 255),
            new Color32(187, 127, 29, 255),
            new Color32(187, 127, 29, 255)
        );

        public static void ApplySilver(TextMeshProUGUI text)
        {
            if (text == null) return;
            text.enableVertexGradient = true;
            text.colorGradient = Silver;
        }

        public static void ApplyGold(TextMeshProUGUI text)
        {
            if (text == null) return;
            text.enableVertexGradient = true;
            text.colorGradient = Gold;
        }

        public static void ApplyFlat(TextMeshProUGUI text, Color color)
        {
            if (text == null) return;
            text.enableVertexGradient = false;
            text.color = color;
        }
    }
}
