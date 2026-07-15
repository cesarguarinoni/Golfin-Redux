// Assets/Scripts/UI/Gacha/GachaHistoryScreenController.cs
// Stage 1 screen controller for the Gacha History / pull log screen.
// Spawns row prefabs (GachaHistoryRow / GachaHistoryRowBall) into the scroll content,
// one per GachaHistoryRecord. Inserts a divider between entries.
// CLOSE button navigates to ScreenId.GeneralShop.
#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Attached to the root of GachaHistoryScreen.prefab (or its scene instance).
    /// Drives the history scroll list; shows all history records sorted newest-first.
    /// </summary>
    public class GachaHistoryScreenController : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject? _clubRowPrefab;
        [SerializeField] private GameObject? _ballRowPrefab;
        [SerializeField] private GameObject? _dividerPrefab;

        [Header("Scroll")]
        [SerializeField] private RectTransform? _scrollContent;

        [Header("Close")]
        [SerializeField] private Button? _closeButton;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnClose);
        }

        private void OnEnable()
        {
            RebuildList();
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnClose);
        }

        // ── List population ────────────────────────────────────────────────────

        private void RebuildList()
        {
            if (_scrollContent == null)
            {
                Debug.LogWarning("[GachaHistoryScreenController] _scrollContent not wired.");
                return;
            }

            // Destroy existing dynamic rows (keep any authored children that are NOT rows).
            // Safest: just clear everything and respawn.
            foreach (Transform child in _scrollContent)
                Destroy(child.gameObject);

            var records = GachaHistoryStore.All;
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                SpawnRow(record);

                // Divider between entries (not after the last one)
                if (i < records.Count - 1 && _dividerPrefab != null)
                    Instantiate(_dividerPrefab, _scrollContent);
            }
        }

        private void SpawnRow(GachaHistoryRecord record)
        {
            switch (record.RewardType)
            {
                case GachaRewardType.Club:
                {
                    if (_clubRowPrefab == null)
                    {
                        Debug.LogWarning("[GachaHistoryScreenController] _clubRowPrefab not wired.");
                        return;
                    }
                    var go = Instantiate(_clubRowPrefab, _scrollContent);
                    var row = go.GetComponent<GachaHistoryRow>();
                    if (row == null) row = go.AddComponent<GachaHistoryRow>();
                    row.Bind(record);
                    break;
                }
                case GachaRewardType.Ball:
                {
                    if (_ballRowPrefab == null)
                    {
                        Debug.LogWarning("[GachaHistoryScreenController] _ballRowPrefab not wired.");
                        return;
                    }
                    var go = Instantiate(_ballRowPrefab, _scrollContent);
                    var row = go.GetComponent<GachaHistoryRowBall>();
                    if (row == null) row = go.AddComponent<GachaHistoryRowBall>();
                    row.Bind(record);
                    break;
                }
                default:
                    Debug.Log($"[GachaHistoryScreenController] Skipping unsupported reward type: {record.RewardType}");
                    break;
            }
        }

        // ── Close ──────────────────────────────────────────────────────────────

        private void OnClose()
        {
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.ShowScreen(ScreenId.GeneralShop);
            else
                Debug.LogWarning("[GachaHistoryScreenController] ScreenManager not found.");
        }
    }
}
