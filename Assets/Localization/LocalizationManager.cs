using System;
using System.Collections.Generic;
using UnityEngine;

public static class LocalizationManager
{
    private static LocalizationTextTable _textTable;
    private static Dictionary<string, LocalizedTextRow> _textMap;

    public static Language CurrentLanguage { get; private set; } = Language.English;

    /// <summary>
    /// True once <see cref="Initialize"/> has built the table. Read by
    /// <c>Golfin.Content.ContentService</c> to ASSERT its own execution order: the overlay must be
    /// merged AFTER Initialize, because Initialize rebuilds _textMap from scratch and would wipe
    /// it. A silent wipe is invisible — the game just shows bundled strings — so the invariant is
    /// checked rather than trusted to [DefaultExecutionOrder] staying correct.
    /// </summary>
    public static bool IsInitialized => _textMap != null;

    // Fired when language changes (for runtime refresh)
    public static event Action OnLanguageChanged;

    public static void Initialize(LocalizationTextTable table, Language defaultLanguage)
    {
        _textTable = table;
        CurrentLanguage = defaultLanguage;

        _textMap = new Dictionary<string, LocalizedTextRow>();
        foreach (var row in _textTable.rows)
        {
            if (!string.IsNullOrEmpty(row.key))
                _textMap[row.key] = row;
        }

        // Any LocalizedText whose OnEnable ran before this point refreshed against a null map
        // (Get() returns the key itself) or against the pre-Initialize default language, and
        // SetLanguage would never reach it because CurrentLanguage already equals the startup
        // language. Firing here makes the boot language independent of script execution order.
        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// Merge admin-published rows over the bundled table. Keys not in the overlay are untouched;
    /// unknown keys are added and harmlessly unused. No-op on null/empty.
    ///
    /// <para>
    /// This is the client end of the content pipeline (CONTENT_PIPELINE_PLAN §4). The bundled
    /// table is the FLOOR and is never replaced (I1) — this only ever writes over individual keys,
    /// so airplane mode on a fresh install behaves exactly as it did before the feature existed.
    /// </para>
    /// <para>
    /// <b>A row whose <c>english</c> is empty is skipped.</b> A blank string is worse than the
    /// bundled one, and <see cref="Get"/>'s Japanese→English fallback depends on <c>english</c>
    /// being present — an empty one would render a Japanese player a blank label rather than the
    /// English text they would otherwise have seen.
    /// </para>
    /// <para>
    /// Deactivated rows never reach here: <c>ContentTextsMapper</c> drops them, so the bundled
    /// string stays (I6 — nothing is ever deleted, only deactivated).
    /// </para>
    /// <para>
    /// MUST run after <see cref="Initialize"/> — see <see cref="IsInitialized"/>.
    /// </para>
    /// </summary>
    /// <returns>
    /// How many rows were actually merged. Zero fires no event. The count is returned rather than
    /// void (as the spec sketched it) so the caller can log what it really applied instead of what
    /// it was handed — the two differ exactly when a row was skipped, which is the case worth
    /// seeing in a log.
    /// </returns>
    public static int ApplyOverlay(IReadOnlyDictionary<string, LocalizedTextRow> overlay)
    {
        if (overlay == null || overlay.Count == 0) return 0;

        // Defensive: an overlay applied before Initialize would be wiped by it. ContentService
        // asserts the order loudly; this keeps the merge itself from NREing if anything else ever
        // calls in early.
        _textMap ??= new Dictionary<string, LocalizedTextRow>();

        int applied = 0;
        foreach (var pair in overlay)
        {
            if (string.IsNullOrEmpty(pair.Key)) continue;

            LocalizedTextRow row = pair.Value;
            if (row == null || string.IsNullOrEmpty(row.english)) continue;

            _textMap[pair.Key] = row;
            applied++;
        }

        // Only when something actually changed. The refresh is what makes an already-open screen
        // repaint; firing it for a no-op overlay would be churn for nothing.
        if (applied > 0) OnLanguageChanged?.Invoke();

        return applied;
    }

    public static void SetLanguage(Language language)
    {
        if (CurrentLanguage == language)
            return;

        CurrentLanguage = language;
        OnLanguageChanged?.Invoke();
    }

    public static string Get(string key)
    {
        if (_textMap == null || !_textMap.TryGetValue(key, out var row))
            return key; // fallback: key

        return CurrentLanguage switch
        {
            Language.Japanese => string.IsNullOrEmpty(row.japanese) ? row.english : row.japanese,
            _ => row.english
        };
    }
}
