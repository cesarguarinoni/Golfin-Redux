#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using Golfin.Roster;

/// <summary>
/// In-game debug panel for manually adjusting Reward Points.
/// Attach to any always-active GameObject (e.g. a DebugManager in the scene).
/// Toggle visibility with the backtick / tilde key (`~).
/// Only compiled in Editor and Development Builds — stripped from release.
/// </summary>
public class RewardPointsDebugPanel : MonoBehaviour
{
    private bool _visible = false;
    private string _inputText = "";

    // Panel layout
    private const float PanelW  = 280f;
    private const float PanelH  = 240f;
    private const float Padding = 10f;

    private Rect _panelRect;

    private void Start()
    {
        float x = Screen.width  - PanelW - 10;
        float y = 10;
        _panelRect = new Rect(x, y, PanelW, PanelH);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[Key.BackQuote].wasPressedThisFrame)
            _visible = !_visible;
    }

    private void OnGUI()
    {
        if (!_visible) return;

        // Allow panel to be dragged
        _panelRect = GUI.Window(9001, _panelRect, DrawPanel, "RP Debug");
    }

    private void DrawPanel(int windowId)
    {
        var rp = RewardPointsManager.Instance;

        float y      = 24f;
        float btnH   = 28f;
        float halfW  = (PanelW - Padding * 3) / 2f;

        // Current balance display
        int current = rp != null ? rp.GetPoints() : -1;
        GUI.Label(new Rect(Padding, y, PanelW - Padding * 2, 20),
            $"Current: <b>{current:N0} RP</b>");
        y += 24f;

        // Text field + Set button
        GUI.Label(new Rect(Padding, y, 50, 20), "Set to:");
        _inputText = GUI.TextField(new Rect(Padding + 52, y, halfW - 20, 22), _inputText);

        if (GUI.Button(new Rect(PanelW - Padding - 60, y, 60, 22), "SET"))
        {
            if (int.TryParse(_inputText, out int amount) && rp != null)
                rp.SetPoints(Mathf.Max(0, amount));
        }
        y += 30f;

        // Preset delta buttons — row 1
        if (GUI.Button(new Rect(Padding,            y, halfW, btnH), "+1 000"))    Adjust(rp,  1_000);
        if (GUI.Button(new Rect(Padding * 2 + halfW, y, halfW, btnH), "+10 000"))  Adjust(rp, 10_000);
        y += btnH + 4f;

        // Row 2
        if (GUI.Button(new Rect(Padding,             y, halfW, btnH), "-1 000"))   Adjust(rp,  -1_000);
        if (GUI.Button(new Rect(Padding * 2 + halfW, y, halfW, btnH), "-10 000"))  Adjust(rp, -10_000);
        y += btnH + 4f;

        // Row 3 — presets
        if (GUI.Button(new Rect(Padding,             y, halfW, btnH), "Set 0"))    rp?.SetPoints(0);
        if (GUI.Button(new Rect(Padding * 2 + halfW, y, halfW, btnH), "Set 50k"))  rp?.SetPoints(50_000);
        y += btnH + 8f;

        // Reset to default
        if (GUI.Button(new Rect(Padding, y, PanelW - Padding * 2, btnH), "Reset to Default"))
            rp?.ResetToDefault();

        // Make window draggable
        GUI.DragWindow(new Rect(0, 0, PanelW, 20));
    }

    private void Adjust(RewardPointsManager rp, int delta)
    {
        if (rp == null) return;
        rp.SetPoints(Mathf.Max(0, rp.GetPoints() + delta));
    }
}
#endif
