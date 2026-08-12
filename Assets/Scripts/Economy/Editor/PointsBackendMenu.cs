// Order: reward_points_backend Slice 1 — editor-only flag toggle + the manual acceptance probe.
using Golfin.Auth;
using Golfin.Economy;
using Golfin.Net;
using UnityEditor;
using UnityEngine;

namespace Golfin.Economy.EditorTools
{
    /// <summary>
    /// Editor-side switch and probe for <c>PointsBackendEnabled</c>.
    ///
    /// Deliberately editor-only. The spec allows a debug-panel toggle, but the obvious host
    /// (<c>Debug/RewardPointsDebugPanel</c>) is a <c>RewardPointsManager</c> call site, and this slice is
    /// under a hard "do not touch RewardPointsManager or its call sites" constraint. A menu item needs no
    /// scene, no prefab and no production file.
    ///
    /// For a DEVICE build the toggle is the <c>GOLFIN_POINTS_BACKEND</c> scripting define
    /// (Player Settings → Other Settings → Scripting Define Symbols) — PlayerPrefs set here do not travel
    /// to the phone. See <see cref="PointsBackendFlag"/>.
    ///
    /// Everything here follows the no-editor-popups rule: results go to the Console, never a dialog.
    /// </summary>
    internal static class PointsBackendMenu
    {
        private const string ToggleItem = "GOLFIN/Points Backend/Enabled (PointsBackendEnabled)";
        private const string RefreshItem = "GOLFIN/Points Backend/Log Server Balance Now";
        private const string QueueItem = "GOLFIN/Points Backend/Log Pending Ops Queue";
        private const string ResetItem = "GOLFIN/Points Backend/Reset Flag To Compiled Default";

        [MenuItem(ToggleItem, priority = 100)]
        private static void ToggleFlag() => PointsBackendFlag.Enabled = !PointsBackendFlag.Enabled;

        [MenuItem(ToggleItem, validate = true)]
        private static bool ToggleFlagValidate()
        {
            Menu.SetChecked(ToggleItem, PointsBackendFlag.Enabled);
            return true;
        }

        [MenuItem(ResetItem, priority = 101)]
        private static void ResetFlag()
        {
            PointsBackendFlag.ResetToDefault();
            Debug.Log($"[PointsBackendFlag] Reset — now {PointsBackendFlag.Enabled} " +
                      $"(compiled default {PointsBackendFlag.CompiledDefault}).");
        }

        /// <summary>
        /// SPEC §4 Slice 1 manual acceptance: flag ON + signed in → the test account's REAL server
        /// balance appears in the Console. Requires play mode, because the call is a coroutine on a
        /// runtime host and the Supabase session is a runtime object.
        /// </summary>
        [MenuItem(RefreshItem, priority = 120)]
        private static void LogBalance()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[PointsBackend] Enter play mode first — the API call needs a runtime coroutine host.");
                return;
            }

            if (!PointsBackendFlag.Enabled)
            {
                Debug.LogWarning($"[PointsBackend] PointsBackendEnabled is OFF — enable it via '{ToggleItem}' first.");
                return;
            }

            if (!AuthService.Instance.Session.IsAuthenticated)
            {
                Debug.LogWarning("[PointsBackend] Not signed in — log in first; an unauthenticated call returns 403.");
                return;
            }

            Debug.Log($"[PointsBackend] GET {Endpoints.PointsBalance} …");
            PointsService.Instance.RefreshBalanceAsync(result =>
            {
                if (result != null && result.Success && result.Data != null)
                    Debug.Log($"[PointsBackend] ✅ Server balance for {AuthService.Instance.Session.Email}: {result.Data}");
                else
                    Debug.LogError($"[PointsBackend] ❌ Balance call failed: {result}");
            });
        }

        [MenuItem(QueueItem, priority = 121)]
        private static void LogQueue()
        {
            var store = new FilePendingOpsStore(FilePendingOpsStore.DefaultPath);
            var queue = new PendingOpsQueue(store);
            queue.Load();

            Debug.Log($"[PointsBackend] Pending ops file: {FilePendingOpsStore.DefaultPath}\n" +
                      $"[PointsBackend] {queue.Count} pending op(s).");

            for (int i = 0; i < queue.Count; i++)
                Debug.Log($"[PointsBackend]   #{i}: {queue.Items[i]}");
        }
    }
}
