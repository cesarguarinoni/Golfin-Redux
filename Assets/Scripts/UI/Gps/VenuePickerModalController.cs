// score_upload_flow §2 — CHOOSE MANUALLY: a course list over VenueService.List, as a ModalController.
#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Net;
using Golfin.UI.Modals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// The fallback for the case the whole GPS step exists to handle badly: the player IS at a
    /// course and the server cannot name it. <c>/venue/list</c> is open (no auth) and returns every
    /// venue, so the filter is client-side — there is no search endpoint and adding one is a
    /// backend task.
    ///
    /// <para>
    /// Picking here changes the NAME the flow shows and the venue it talks about; it does not
    /// change the coordinates that go up. That asymmetry is the point: a hand-picked course is a
    /// claim, a measured fix is evidence, and the server is the one that decides whether they
    /// agree (<c>_verify_gps</c>).
    /// </para>
    /// <para>
    /// Rows are pooled from a single authored template — the list is every golf course in the
    /// database and instantiating a prefab per row would spike on the first open.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VenuePickerModalController : ModalController
    {
        private const string Tag = "[VenuePicker]";

        /// <summary>Rows built at once. The list is scrollable and unfiltered it is the whole
        /// country; a player looking for a course types two characters long before scrolling past
        /// sixty rows.</summary>
        private const int MaxRows = 60;

        [Header("Venue picker")]
        [SerializeField] private TMP_InputField? _search;

        [Tooltip("Parent of the pooled rows. Needs a vertical layout group in the prefab.")]
        [SerializeField] private RectTransform? _rowsParent;

        [Tooltip("One authored row, INACTIVE in the prefab, cloned into the pool.")]
        [SerializeField] private GameObject? _rowTemplate;

        [Tooltip("Shown while /venue/list is in flight and when it returns nothing.")]
        [SerializeField] private TextMeshProUGUI? _statusLabel;

        [Tooltip("SU_NO_COURSE — post with no venue at all.")]
        [SerializeField] private Button? _skipButton;

        private readonly List<GameObject> _pool = new List<GameObject>();
        private readonly List<VenueDto> _all = new List<VenueDto>();
        private Action<VenueDto?>? _onPicked;
        private bool _loaded;
        private bool _wired;

        /// <summary>
        /// Open the picker. <paramref name="onPicked"/> gets the chosen venue, or null when the
        /// player skipped — never nothing, so the caller can always re-enable its button.
        /// </summary>
        public void Open(Action<VenueDto?> onPicked)
        {
            _onPicked = onPicked;
            WireOnce();
            Show();

            if (_statusLabel != null)
            {
                _statusLabel.gameObject.SetActive(true);
                _statusLabel.text = LocalizationManager.Get("SU_LOCATING");
            }

            // Re-fetched per open rather than cached for the session: the list grows every time
            // anybody's auto-register creates a course, and this is the screen where that matters.
            _loaded = false;
            ApiClient.Instance.Run(VenueService.Instance.List(LanguageCode(), OnList));
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            if (_search != null) _search.onValueChanged.AddListener(_ => Rebuild());
            if (_skipButton != null) _skipButton.onClick.AddListener(() => Pick(null));
            if (_rowTemplate != null) _rowTemplate.SetActive(false);
        }

        /// <summary>The server localizes venue names off this. Mirrors what the rest of the app
        /// asks for, so a Japanese player gets Japanese course names.</summary>
        private static string LanguageCode()
            => LocalizationManager.CurrentLanguage == Language.Japanese ? "ja" : "en";

        private void OnList(ApiResult<List<VenueDto>> result)
        {
            _all.Clear();

            if (result == null || !result.Success || result.Data == null)
            {
                Debug.LogWarning($"{Tag} /venue/list failed ({result?.ErrorKind}) — the picker shows nothing.");
            }
            else
            {
                foreach (VenueDto v in result.Data)
                    if (v != null && !string.IsNullOrWhiteSpace(v.Name)) _all.Add(v);
            }

            _loaded = true;
            Rebuild();
        }

        private void Rebuild()
        {
            string query = (_search != null ? _search.text : string.Empty) ?? string.Empty;
            query = query.Trim();

            int shown = 0;
            for (int i = 0; i < _all.Count && shown < MaxRows; i++)
            {
                VenueDto v = _all[i];
                if (query.Length > 0 &&
                    v.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                Bind(RowAt(shown), v);
                shown++;
            }

            for (int i = shown; i < _pool.Count; i++) _pool[i].SetActive(false);

            if (_statusLabel != null)
            {
                bool empty = shown == 0;
                _statusLabel.gameObject.SetActive(empty);
                _statusLabel.text = _loaded
                    ? LocalizationManager.Get("SU_COURSE_NONE")
                    : LocalizationManager.Get("SU_LOCATING");
            }
        }

        private GameObject RowAt(int index)
        {
            while (_pool.Count <= index)
            {
                if (_rowTemplate == null || _rowsParent == null) break;
                GameObject clone = Instantiate(_rowTemplate, _rowsParent);
                clone.name = "VenueRow" + _pool.Count;
                _pool.Add(clone);
            }
            GameObject row = _pool[index];
            row.SetActive(true);
            return row;
        }

        private void Bind(GameObject row, VenueDto venue)
        {
            var label = row.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            if (label != null) label.text = venue.Name;

            var button = row.GetComponent<Button>();
            if (button == null) button = row.GetComponentInChildren<Button>(includeInactive: true);
            if (button != null)
            {
                // RemoveAllListeners, not AddListener: a pooled row is re-bound to a different
                // venue on every keystroke, and a stack of stale listeners would pick the course
                // the row USED to show.
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Pick(venue));
            }
        }

        private void Pick(VenueDto? venue)
        {
            Action<VenueDto?>? cb = _onPicked;
            _onPicked = null;
            Hide();
            cb?.Invoke(venue);
        }
    }
}
