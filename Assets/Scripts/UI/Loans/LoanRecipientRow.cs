// asset_loans §4.2 step 5 — one row of the LEND TO list (Figma 14183:107512).
#nullable enable
using System;
using Golfin.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Loans
{
    /// <summary>
    /// A 732×96 selectable row: avatar circle, display name, "Lv {avatar_level}".
    ///
    /// <para>
    /// THE AVATAR IS THE PLACEHOLDER CIRCLE TODAY. <c>avatar_url</c> arrives on the wire and is
    /// kept on the DTO, but there is no remote-avatar loader in the client — <c>CatalogArtCache</c>
    /// only serves catalog art, and nothing in the game reads <c>UserDetailDto.AvatarUrl</c> either
    /// (grepped 2026-09-09). Rather than invent a second image-fetch path inside a lending task,
    /// the row draws the #38597F circle the Figma node specifies as the placeholder and
    /// <see cref="AvatarUrl"/> is carried so the loader, when it exists, has one call site to fill.
    /// Flagged as a deviation in the report.
    /// </para>
    /// </summary>
    public sealed class LoanRecipientRow : MonoBehaviour
    {
        [Header("Prefab-authored")]
        [SerializeField] private Image? background;
        [SerializeField] private Image? avatar;
        [SerializeField] private TextMeshProUGUI? nameText;
        [SerializeField] private TextMeshProUGUI? levelText;
        [SerializeField] private Button? button;

        // ── Selection: a SPRITE SWAP, not an Outline component ────────────────
        //
        // PIPELINE_HARDENING C5 / the UI fidelity linter: an `Outline` is four offset copies of the
        // graphic, not a crisp N-px border, and it fails the render-health check. The selected
        // state's 3 px #2775DD stroke is baked into S_LoanRowSelected.png
        // (Docs/Scripts/make_loan_sprites.py), so selecting a row swaps one sprite for another and
        // the border is exactly three pixels at any DPI.
        //
        // The UNSELECTED sprite is white and TINTED to the node's #050F1F @ 60 %; the SELECTED one
        // carries its own colours and is drawn untinted.
        [Header("Selection (Figma: unselected #050F1F @60 %, selected #2775DD @35 % + 3 px stroke)")]
        [SerializeField] private Sprite? unselectedSprite;
        [SerializeField] private Sprite? selectedSprite;
        [SerializeField] private Color unselectedFill = new Color(0.0196f, 0.0588f, 0.1216f, 0.60f);

        /// <summary>The user id this row lends to. Empty on a loading placeholder row.</summary>
        public string UserId { get; private set; } = "";

        public string DisplayName { get; private set; } = "";
        public string? AvatarUrl { get; private set; }

        public event Action<LoanRecipientRow>? Clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public void Bind(FollowedUserDto user)
        {
            UserId      = user?.Id ?? "";
            DisplayName = user?.DisplayName ?? "PLAYER";
            AvatarUrl   = user?.AvatarUrl;

            if (nameText  != null) nameText.text  = DisplayName;
            if (levelText != null) levelText.text = $"Lv {(user?.AvatarLevel ?? 1)}";
            if (button    != null) button.interactable = !string.IsNullOrEmpty(UserId);

            SetSelected(false);
        }

        /// <summary>
        /// The loading state: em-dash rows, not interactable — the same shape the GPS screens use
        /// so a list that is still arriving reads as "coming" rather than as "empty".
        /// </summary>
        public void BindPlaceholder()
        {
            UserId = "";
            DisplayName = "";
            AvatarUrl = null;
            if (nameText  != null) nameText.text  = "—";
            if (levelText != null) levelText.text = "—";
            if (button    != null) button.interactable = false;
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (background == null) return;

            if (selected)
            {
                if (selectedSprite != null) background.sprite = selectedSprite;
                background.color = Color.white;   // the sprite carries its own fill and stroke
            }
            else
            {
                if (unselectedSprite != null) background.sprite = unselectedSprite;
                background.color = unselectedFill;
            }
        }
    }
}
