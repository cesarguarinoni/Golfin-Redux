using UnityEngine;
using UnityEngine.InputSystem;

public class WalkCamera : MonoBehaviour
{
    public float moveSpeed = 20f;
    public float lookSpeed = 2f;
    public float height = 2f;
    public float aerialHeight = 300f;

    private float yaw = 0f;
    private float pitch = 0f;
    private bool cursorLocked = false;
    private bool aerialMode = false;
    private float aerialYaw = 0f;

    // Saved walk-mode state for returning from aerial
    private Vector3 savedPosition;
    private float savedYaw;
    private float savedPitch;

    // Aerial view player marker
    private GameObject playerMarker;
    private LineRenderer facingLine;

    void Start()
    {
        LockCursor();
        CreatePlayerMarker();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        // Toggle cursor lock
        if (keyboard[Key.Escape].wasPressedThisFrame)
            UnlockCursor();
        if (mouse.leftButton.wasPressedThisFrame && !cursorLocked)
            LockCursor();

        // Toggle aerial view with Tab
        if (keyboard[Key.Tab].wasPressedThisFrame)
            ToggleAerial();

        if (aerialMode)
            UpdateAerial(keyboard, mouse);
        else
            UpdateWalk(keyboard, mouse);

        UpdatePlayerMarker();
    }

    private void UpdateWalk(Keyboard keyboard, Mouse mouse)
    {
        // Mouse look (only when locked)
        if (cursorLocked)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            yaw += mouseDelta.x * lookSpeed * 0.1f;
            pitch -= mouseDelta.y * lookSpeed * 0.1f;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // WASD movement
        float h = 0f, v = 0f;
        if (keyboard[Key.W].isPressed || keyboard[Key.UpArrow].isPressed) v += 1f;
        if (keyboard[Key.S].isPressed || keyboard[Key.DownArrow].isPressed) v -= 1f;
        if (keyboard[Key.D].isPressed || keyboard[Key.RightArrow].isPressed) h += 1f;
        if (keyboard[Key.A].isPressed || keyboard[Key.LeftArrow].isPressed) h -= 1f;

        // Sprint with shift
        float speed = moveSpeed;
        if (keyboard[Key.LeftShift].isPressed) speed *= 3f;

        Vector3 move = transform.right * h + transform.forward * v;
        move.y = 0f;
        if (move.sqrMagnitude > 0f)
            transform.position += move.normalized * speed * Time.deltaTime;

        // Stick to terrain height + offset
        if (Terrain.activeTerrain != null)
        {
            float terrainY = Terrain.activeTerrain.SampleHeight(transform.position);
            float terrainBase = Terrain.activeTerrain.transform.position.y;
            transform.position = new Vector3(
                transform.position.x,
                terrainBase + terrainY + height,
                transform.position.z
            );
        }
    }

    private void UpdateAerial(Keyboard keyboard, Mouse mouse)
    {
        // Pan with WASD (XZ plane)
        float h = 0f, v = 0f;
        if (keyboard[Key.W].isPressed || keyboard[Key.UpArrow].isPressed) v += 1f;
        if (keyboard[Key.S].isPressed || keyboard[Key.DownArrow].isPressed) v -= 1f;
        if (keyboard[Key.D].isPressed || keyboard[Key.RightArrow].isPressed) h += 1f;
        if (keyboard[Key.A].isPressed || keyboard[Key.LeftArrow].isPressed) h -= 1f;

        float speed = moveSpeed * 2f;
        if (keyboard[Key.LeftShift].isPressed) speed *= 3f;

        Vector3 move = new Vector3(h, 0f, v);
        if (move.sqrMagnitude > 0f)
            transform.position += move.normalized * speed * Time.deltaTime;

        // Scroll wheel to adjust altitude
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float newY = transform.position.y + scroll * 5f;
            newY = Mathf.Clamp(newY, 50f, 1000f);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        // Q/E to rotate view
        float rot = 0f;
        if (keyboard[Key.Q].isPressed) rot -= 1f;
        if (keyboard[Key.E].isPressed) rot += 1f;
        if (Mathf.Abs(rot) > 0f)
            aerialYaw += rot * 90f * Time.deltaTime;

        transform.rotation = Quaternion.Euler(90f, aerialYaw, 0f);
    }

    private void ToggleAerial()
    {
        aerialMode = !aerialMode;

        if (aerialMode)
        {
            // Save walk state
            savedPosition = transform.position;
            savedYaw = yaw;
            savedPitch = pitch;

            // Move up, look straight down, face player's direction
            aerialYaw = savedYaw;
            transform.position = new Vector3(
                transform.position.x,
                aerialHeight,
                transform.position.z
            );
            transform.rotation = Quaternion.Euler(90f, aerialYaw, 0f);
        }
        else
        {
            // Restore walk state
            transform.position = savedPosition;
            yaw = savedYaw;
            pitch = savedPitch;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    private void CreatePlayerMarker()
    {
        // Disc marker
        playerMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        playerMarker.name = "PlayerMarker";
        playerMarker.transform.localScale = new Vector3(6f, 0.2f, 6f);
        var markerRenderer = playerMarker.GetComponent<Renderer>();
        var markerMat = new Material(Shader.Find("Standard"));
        markerMat.color = Color.cyan;
        markerRenderer.sharedMaterial = markerMat;
        // Remove collider so it doesn't interfere
        var col = playerMarker.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        playerMarker.SetActive(false);

        // Facing line
        var lineGO = new GameObject("FacingLine");
        lineGO.transform.SetParent(playerMarker.transform, false);
        facingLine = lineGO.AddComponent<LineRenderer>();
        facingLine.positionCount = 2;
        facingLine.startWidth = 1.5f;
        facingLine.endWidth = 0.3f;
        facingLine.material = new Material(Shader.Find("Sprites/Default"));
        facingLine.startColor = Color.yellow;
        facingLine.endColor = new Color(1f, 1f, 0f, 0.2f);
        facingLine.useWorldSpace = true;
    }

    private void UpdatePlayerMarker()
    {
        if (playerMarker == null) return;

        bool show = aerialMode;
        if (playerMarker.activeSelf != show)
            playerMarker.SetActive(show);

        if (!show) return;

        // Position at saved walk position
        float markerY = savedPosition.y + 10f; // float above terrain
        playerMarker.transform.position = new Vector3(savedPosition.x, markerY, savedPosition.z);

        // Facing line from marker outward in saved yaw direction
        float lineLength = 30f;
        float rad = savedYaw * Mathf.Deg2Rad;
        Vector3 forward = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        Vector3 lineStart = playerMarker.transform.position;
        Vector3 lineEnd = lineStart + forward * lineLength;
        facingLine.SetPosition(0, lineStart);
        facingLine.SetPosition(1, lineEnd);
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cursorLocked = true;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorLocked = false;
    }
}
