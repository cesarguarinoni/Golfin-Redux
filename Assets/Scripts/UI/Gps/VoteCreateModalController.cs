// gps_gifts_votes — CREATE: a question, a Yes/No pair, an expiry.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Golfin.Net;
using Golfin.Social;
using Golfin.UI.Modals;
using Golfin.UI.Toast;
using TMPro;
using Golfin.UI.Polish;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Creates a vote.
    ///
    /// <para>
    /// v1 offers a QUESTION and an EXPIRY and nothing else: the options are fixed YES / NO
    /// (SPEC § Goal). That is not a placeholder for an option editor — a free-form option list
    /// would need a moderation story the vote router does not have, and the multi-option card
    /// exists to RENDER polls created elsewhere, not to author them here.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoteCreateModalController : ModalController
    {
        private const string Tag = "[VoteCreate]";

        /// <summary>The three expiry choices, in hours, parallel to the three buttons.</summary>
        public static readonly int[] ExpiryHours = { 24, 24 * 3, 24 * 7 };

        [Header("Create vote")]
        [SerializeField] private TMP_InputField? _question;
        [SerializeField] private TextMeshProUGUI? _status;
        [SerializeField] private Button[] _expiryButtons = new Button[0];
        [SerializeField] private GameObject[] _expiryRoots = new GameObject[0];
        [SerializeField] private Button? _submitButton;
        [SerializeField] private Button? _cancelButton;

        private int _expiryIndex = 1;      // 3 days — the middle choice, and the node's own default
        private bool _inFlight;

        /// <summary>The scope that draws the wait on CREATE (gps_polish §D6).</summary>
        private PendingSpend? _pending;
        private bool _wired;
        private Action<VoteDto>? _onCreated;

        /// <summary>Open the modal. <paramref name="onCreated"/> receives the created vote so the
        /// screen can prepend its card without a second round trip.</summary>
        public void Open(Action<VoteDto> onCreated)
        {
            _onCreated = onCreated;
            WireOnce();
            _inFlight = false;
            _pending?.Dispose();
            _pending = null;
            _expiryIndex = 1;
            if (_question != null) _question.text = string.Empty;
            Show();
            SetStatus(string.Empty);
            Repaint();
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < _expiryButtons.Length; i++)
            {
                int index = i;
                if (_expiryButtons[i] == null) continue;
                _expiryButtons[i].onClick.AddListener(() =>
                {
                    if (_inFlight) return;
                    _expiryIndex = index;
                    Repaint();
                });
            }
            if (_submitButton != null) _submitButton.onClick.AddListener(OnSubmit);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(Hide);
            if (_question != null) _question.onValueChanged.AddListener(_ => Repaint());

            // gps_polish §D9 — the question field is near the modal's bottom edge and is under
            // the iOS keyboard while it is being typed into. The whole modal panel lifts, so the
            // header and the expiry row travel with the field. No-op in the Editor.
            KeyboardInsetBinder.Attach(_question, modalPanel != null
                ? modalPanel.transform as RectTransform
                : null);
        }

        private void Repaint()
        {
            for (int i = 0; i < _expiryRoots.Length; i++)
            {
                if (_expiryRoots[i] == null) continue;
                Transform? sel = _expiryRoots[i].transform.Find("Selected");
                if (sel != null) sel.gameObject.SetActive(i == _expiryIndex);
            }
            if (_submitButton != null)
                _submitButton.interactable = !_inFlight && Question().Length > 0;
        }

        private string Question() => (_question != null ? _question.text : string.Empty).Trim();

        private void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }

        private void OnSubmit()
        {
            if (_inFlight) return;
            string q = Question();
            if (q.Length == 0) return;

            _inFlight = true;
            // gps_polish §D6 — CREATE posts a vote; CANCEL goes with it so the modal cannot be
            // dismissed out from under an in-flight write.
            _pending?.Dispose();
            _pending = _cancelButton != null
                ? PendingSpend.BeginOn(_submitButton, _cancelButton)
                : PendingSpend.BeginOn(_submitButton);
            Repaint();
            SetStatus(LocalizationManager.Get("GPS_VOTE_CREATE_SENDING"));

            // The labels are the LOCALIZED yes/no the card's bars are drawn with, so a card
            // created in Japanese reads はい / いいえ on its own bars rather than Yes / No.
            var options = new List<string>
            {
                LocalizationManager.Get("GPS_VOTE_YES"),
                LocalizationManager.Get("GPS_VOTE_NO"),
            };

            // ISO-8601 with an explicit Z: the column is timestamptz, and a naive local string
            // would be read as UTC and land the expiry hours off wherever the player is.
            string expires = DateTime.UtcNow.AddHours(ExpiryHours[Mathf.Clamp(_expiryIndex, 0, ExpiryHours.Length - 1)])
                                    .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

            ApiClient.Instance.Run(VoteService.Instance.Create(q, options, expires, OnResult));
        }

        private void OnResult(ApiResult<VoteDto> result)
        {
            _inFlight = false;
            // Dispose FIRST, before the result is acted on: restoring means "put back what was
            // there before the tap", and the handler below overwrites that with the new truth.
            _pending?.Dispose();
            _pending = null;

            if (result == null || !result.Success || result.Data == null)
            {
                Debug.LogWarning($"{Tag} /vote/create failed: {result}");
                SetStatus(LocalizationManager.Get("GPS_VOTE_CREATE_FAILED"));
                Repaint();
                return;
            }

            Debug.Log($"{Tag} created vote {result.Data.Id} — \"{result.Data.Question}\".");
            if (ToastController.Instance != null)
                ToastController.Instance.Show(LocalizationManager.Get("GPS_VOTE_CREATED"));

            _onCreated?.Invoke(result.Data);
            Hide();
        }
    }
}
