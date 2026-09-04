using UnityEngine;
using Golfin.UI.Polish;
using UnityEngine.UI;
using System.Collections.Generic;
using Golfin.UI.Modals;

namespace Golfin.UI
{
    /// <summary>
    /// Controller for the Settings Screen.
    /// Manages expand/collapse of accordion menu items (only one open at a time),
    /// submenus, and modal integration (About, Log Out).
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        public static SettingsController Instance { get; private set; }

        [Header("Settings Panel")]
        public GameObject background;
        public GameObject settingsPanel;
        public Button closeButton;

        [Header("Menu Items with Accordion")]
        public SettingsMenuItem userProfileItem;
        public SettingsMenuItem soundSettingsItem;
        public SettingsMenuItem graphicsItem;
        public SettingsMenuItem languageItem;
        public SettingsMenuItem aboutItem;

        [Header("Simple Menu Buttons (No Accordion)")]
        public Button termsOfUseButton;
        public Button privacyPolicyButton;
        public Button faqButton;
        public Button contactButton;
        public Button logOutButton;

        [Header("Submenus")]
        public UserProfileSubmenu userProfileSubmenu;
        public SoundSettingsSubmenu soundSettingsSubmenu;
        public GraphicsSubmenu graphicsSubmenu;
        public LanguageSubmenu languageSubmenu;
        public AboutSubmenu aboutSubmenu;
        
        [Header("Modals (Phase 3)")]
        public ModalController logOutModal;

        private List<SettingsMenuItem> _accordionItems = new List<SettingsMenuItem>();
        private SettingsMenuItem _currentlyExpandedItem;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeAccordionItems();
            InitializeButtons();
            ApplyStandaloneLayout();
        }

        /// <summary>
        /// gps_standalone_shell §D5 — the shell's Settings layout, one flag rather than a second
        /// prefab.
        ///
        /// <para>KEPT: User Profile (display name), Language, About, the legal links (Terms,
        /// Privacy, FAQ, Contact) and Log Out — the whole account/legal half, which is exactly
        /// what a PLAYLIFE-only product still owes its players.</para>
        ///
        /// <para>HIDDEN: Graphics (a quality tier that only ever describes the 3D golf renderer;
        /// the shell ships no hole scenes to render) and Sound Settings (SFX/music mixers for
        /// gameplay audio the shell never plays). Hiding the ROW, not just its submenu, so the
        /// accordion has no dead entry — and de-registering it from the accordion group, because
        /// a hidden item left registered would still be the "currently expanded" one and swallow
        /// the one-open-at-a-time rule.</para>
        ///
        /// <para>Runs in Awake, after the two initializers, so it also covers the safety-net sweep
        /// in <see cref="InitializeAccordionItems"/> that registers unassigned rows. No-op in the
        /// game and in the demo.</para>
        /// </summary>
        private void ApplyStandaloneLayout()
        {
            if (!GolfinRedux.UI.StandaloneGate.Enabled) return;

            HideSettingsRow(graphicsItem);
            HideSettingsRow(soundSettingsItem);

            Debug.Log("[SettingsController] standalone shell layout — Graphics and Sound rows hidden.");
        }

        /// <summary>Take one row out of the accordion group and off the screen. Null-safe.</summary>
        private void HideSettingsRow(SettingsMenuItem item)
        {
            if (item == null) return;
            if (_accordionItems.Remove(item)) item.OnExpanded -= OnMenuItemExpanded;
            item.gameObject.SetActive(false);
        }

        private void Start()
        {
            // Start with settings closed
            if (background != null) background.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        /// <summary>
        /// Initialize accordion menu items and subscribe to events.
        ///
        /// The four Inspector slots below are the documented wiring, but registration does NOT
        /// depend on them: any <see cref="SettingsMenuItem"/> living under this controller is swept
        /// up as well. An unassigned slot used to silently drop a row out of the accordion group —
        /// that row then stayed open while others expanded, breaking "only one open at a time".
        /// (aboutItem was empty in ShellScene, so About behaved exactly that way.)
        /// </summary>
        private void InitializeAccordionItems()
        {
            RegisterAccordionItem(userProfileItem);
            RegisterAccordionItem(soundSettingsItem);
            RegisterAccordionItem(graphicsItem);
            RegisterAccordionItem(languageItem);
            RegisterAccordionItem(aboutItem);

            // Safety net: catch any row whose Inspector slot was never assigned.
            foreach (var item in GetComponentsInChildren<SettingsMenuItem>(true))
            {
                RegisterAccordionItem(item);
            }
        }

        /// <summary>
        /// Add a menu item to the accordion group exactly once.
        /// </summary>
        private void RegisterAccordionItem(SettingsMenuItem item)
        {
            if (item == null || _accordionItems.Contains(item)) return;

            _accordionItems.Add(item);
            item.OnExpanded += OnMenuItemExpanded;
        }

        /// <summary>
        /// Initialize button click handlers.
        /// </summary>
        private void InitializeButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseSettings);
            }

            // Simple buttons (no accordion)
            if (termsOfUseButton != null)
                termsOfUseButton.onClick.AddListener(OnTermsOfUseClick);

            if (privacyPolicyButton != null)
                privacyPolicyButton.onClick.AddListener(OnPrivacyPolicyClick);

            if (faqButton != null)
                faqButton.onClick.AddListener(OnFaqClick);

            if (contactButton != null)
                contactButton.onClick.AddListener(OnContactClick);

            if (logOutButton != null)
                logOutButton.onClick.AddListener(OnLogOutClick);
            
            // Note: About is now an accordion item (aboutItem), not a simple button
        }

        /// <summary>
        /// Called when any menu item is expanded.
        /// Ensures only one item is expanded at a time.
        /// </summary>
        private void OnMenuItemExpanded(SettingsMenuItem expandedItem)
        {
            Debug.Log($"[SettingsController] OnMenuItemExpanded called for: {expandedItem.gameObject.name}");
            Debug.Log($"[SettingsController] Total accordion items: {_accordionItems.Count}");
            
            // Collapse all other items
            foreach (var item in _accordionItems)
            {
                if (item != expandedItem && item.IsExpanded)
                {
                    Debug.Log($"[SettingsController] Auto-collapsing: {item.gameObject.name}");
                    item.ForceCollapse();
                }
            }

            _currentlyExpandedItem = expandedItem;
            Debug.Log($"[SettingsController] Expanded: {expandedItem.gameObject.name}");
        }

        /// <summary>
        /// True while the settings overlay is on screen. Used by ScreenManager's Android
        /// back handler (nav_back_memory §7) — Settings leaves the screen underneath enabled,
        /// so back has to close the overlay before it can mean "go back a screen".
        /// </summary>
        /// <summary>
        /// game_polish_a §D3 — STATE-DRIVEN, and it has to stay that way.
        ///
        /// <para>ScreenManager's Android back handler asks this to decide whether BACK means
        /// "close the overlay" or "go back a screen". With the close now animated, the panel stays
        /// active for <c>FadeDur</c> AFTER the player asked for it to go — so reading
        /// <c>activeSelf</c> would report the overlay as open during its own exit and swallow the
        /// next back press. It is true from the first frame of open and false from the first frame
        /// of close, which is what the caller actually means.</para>
        /// </summary>
        public bool IsOpen => _open;

        private bool _open;
        private Coroutine? _panelMotion;
        private Coroutine? _scrimMotion;

        /// <summary>
        /// Open the settings panel.
        /// </summary>
        public void OpenSettings()
        {
            // Settings is a modal overlay that predates ModalController, so it gets the shared
            // scrim guarantee explicitly: its authored Background was a 50%-alpha black, which in
            // linear space barely tints the screen at all (measured: top bar 26,72,113 → 16,50,81)
            // and read as "Settings has no dim". ModalScrim lifts it to the project floor and
            // keeps it raycast-blocking and canvas-sized.
            background = ModalScrim.Apply(transform, background, settingsPanel);

            _open = true;
            if (background != null) background.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(true);

            // game_polish_a §D3 — the scrim fades in and the panel pops (0.9 -> 1 with its own
            // alpha), instead of both appearing between one frame and the next.
            if (background != null)
                UiMotion.Run(this, ref _scrimMotion, UiMotion.Fade(Group(background), 0f, 1f));
            if (settingsPanel != null)
                UiMotion.Run(this, ref _panelMotion,
                             UiMotion.Pop(settingsPanel.transform as RectTransform, Group(settingsPanel)));

            Debug.Log("[SettingsController] Settings opened");
        }

        /// <summary>
        /// Close the settings panel and collapse all items.
        /// </summary>
        public void CloseSettings()
        {
            // Collapse all accordion items
            foreach (var item in _accordionItems)
            {
                if (item.IsExpanded)
                {
                    item.ForceCollapse();
                }
            }

            _currentlyExpandedItem = null;
            _open = false;

            // The reverse: the panel un-pops and the scrim fades, and BOTH deactivate only when
            // their tween finishes. UiMotion.Then composes the tail into the routine's finalizer,
            // so an interrupted or disabled close still deactivates — a scrim left active would
            // eat every tap on the screen underneath.
            if (settingsPanel != null)
            {
                GameObject panel = settingsPanel;
                UiMotion.Run(this, ref _panelMotion,
                    UiMotion.Then(UiMotion.Unpop(panel.transform as RectTransform, Group(panel)),
                                  () => { if (panel != null) panel.SetActive(false); }));
            }
            if (background != null)
            {
                GameObject scrim = background;
                UiMotion.Run(this, ref _scrimMotion,
                    UiMotion.Then(UiMotion.Fade(Group(scrim), Group(scrim).alpha, 0f),
                                  () => { if (scrim != null) scrim.SetActive(false); }));
            }

            Debug.Log("[SettingsController] Settings closed");
        }

        /// <summary>The CanvasGroup a fade needs, made on first use. An alpha-1 group is a no-op,
        /// so adding one cannot move a rest pixel — the same argument LayeredPush.EnsureGroup and
        /// GpsScreenTransition.EnsureGroup make.</summary>
        private static CanvasGroup Group(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        // Simple menu item handlers (Phase 3 features)

        /// <summary>
        /// Terms of Service (Google Doc). Served read-only: the shared link is an /edit link, but
        /// players have view access only, so Docs redirects them to the reader. /preview is used
        /// instead of /edit so the editor chrome never flashes up on the way there.
        /// </summary>
        private const string TermsOfServiceUrl =
            "https://docs.google.com/document/d/1g42eCJOtV4gI7NIYVnIOyL8wAIA-tzCJfyJGDOKgcMM/preview";

        /// <summary>Privacy Policy (Google Doc). Same read-only treatment as <see cref="TermsOfServiceUrl"/>.</summary>
        private const string PrivacyPolicyUrl =
            "https://docs.google.com/document/d/1kclGdUoDkCCPlW6h8Vff1sERmDbJYAHE3vBys1BG7Lc/preview";

        private void OnTermsOfUseClick()
        {
            Debug.Log("[SettingsController] Terms of Use clicked");
            OpenWebView(TermsOfServiceUrl);
        }

        private void OnPrivacyPolicyClick()
        {
            Debug.Log("[SettingsController] Privacy Policy clicked");
            OpenWebView(PrivacyPolicyUrl);
        }

        private void OnFaqClick()
        {
            Debug.Log("[SettingsController] FAQ clicked");
            OpenWebView("https://golfin.io/faq");
        }

        // Note: About is now handled as an accordion item (aboutItem + AboutSubmenu)
        // No OnAboutClick needed - the SettingsMenuItem handles expansion automatically

        private void OnContactClick()
        {
            Debug.Log("[SettingsController] Contact clicked");
            OpenWebView(BuildContactFormUrl());
        }

        /// <summary>
        /// Contact form (Google Forms). Base link as published by the form owner.
        /// </summary>
        private const string ContactFormUrl =
            "https://docs.google.com/forms/d/e/1FAIpQLSdcq3fyyWqykSph7u0JMZSx95drYhYH356F5cUnIhqimeLuvg/viewform?usp=publish-editor";

        /// <summary>
        /// Optional second prefill target for the player's email.
        ///
        /// The form's built-in email field (Settings → "Collect email addresses → Responder input")
        /// is already prefilled by the <c>emailAddress=</c> param that <see cref="BuildContactFormUrl"/>
        /// always appends — verified live 2026-08-18, the field lands pre-populated. That field is
        /// rendered by Google's JS and carries no <c>name</c> in the page source, so it is invisible
        /// to a plain HTML fetch; do not conclude from the source that the form has no email field.
        ///
        /// This constant is only needed if a SEPARATE short-answer "Email" question is ever added
        /// alongside it. Get the id from the form's ⋮ → "Get pre-filled link": submit a dummy value
        /// and copy the number out of the generated <c>entry.NNNNNNNNN=</c>. Empty = unused.
        /// </summary>
        private const string ContactFormEmailEntryId = "";

        /// <summary>
        /// Contact form URL with the player's email pre-filled when a session is available.
        /// Falls back to the bare form for signed-out players.
        /// </summary>
        private static string BuildContactFormUrl()
        {
            string email = null;
            try
            {
                // Instance is bootstrapped at app start; the getter lazily creates it as a fallback.
                email = Golfin.Auth.AuthService.Instance?.Session?.Email;
            }
            catch (System.Exception e)
            {
                // Never let a prefill lookup break the Contact button — fall back to the bare form.
                Debug.LogWarning($"[SettingsController] Could not read session email for contact prefill: {e.Message}");
            }

            if (string.IsNullOrEmpty(email))
            {
                Debug.Log("[SettingsController] No signed-in email — opening contact form without prefill.");
                return ContactFormUrl;
            }

            var encoded = System.Uri.EscapeDataString(email);
            var url = ContactFormUrl + "&emailAddress=" + encoded;

            if (!string.IsNullOrEmpty(ContactFormEmailEntryId))
                url += "&entry." + ContactFormEmailEntryId + "=" + encoded;

            return url;
        }

        private void OnLogOutClick()
        {
            // account_flow_wiring: direct sign-out for testing — clears the Supabase session and
            // returns to the Splash gate. (Phase 3 polish: confirmation modal via logOutModal.)
            Debug.Log("[SettingsController] Log Out clicked — clearing session, returning to Splash.");
            Golfin.Auth.AuthService.Instance.SignOut();
            CloseSettings();
            if (GolfinRedux.UI.ScreenManager.Instance != null)
                GolfinRedux.UI.ScreenManager.Instance.ShowScreen(GolfinRedux.UI.ScreenId.Splash);
            else
                Debug.LogError("[SettingsController] ScreenManager.Instance not found — cannot return to Splash.");
        }

        /// <summary>
        /// Open a URL in webview or external browser.
        /// </summary>
        private void OpenWebView(string url)
        {
            Debug.Log($"[SettingsController] Opening URL: {url}");
            
#if UNITY_ANDROID || UNITY_IOS
            Application.OpenURL(url);
#else
            Debug.LogWarning("Webview not supported on this platform. Opening in external browser.");
            Application.OpenURL(url);
#endif
        }

        /// <summary>
        /// Collapse all accordion items (useful for external control).
        /// </summary>
        public void CollapseAllItems()
        {
            foreach (var item in _accordionItems)
            {
                if (item.IsExpanded)
                {
                    item.Collapse();
                }
            }
            _currentlyExpandedItem = null;
        }

        /// <summary>
        /// Get the currently expanded menu item.
        /// </summary>
        public SettingsMenuItem GetCurrentlyExpandedItem()
        {
            return _currentlyExpandedItem;
        }
    }
}
