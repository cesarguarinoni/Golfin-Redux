// gps device pass 2026-09-03 — the gift catalog reads Japanese in an English build.
#nullable enable
namespace Golfin.Social
{
    /// <summary>
    /// The display name of a gift catalog row, in the player's language.
    ///
    /// <para>
    /// <c>gift_items</c> is the ONE catalog that never joined the content pipeline: it lives only
    /// on the server, carries a single <c>name</c> column, and is not mirrored into a repo CSV — so
    /// nothing ever exported it, diffed it, or asked it for a second language. Its 21 rows were
    /// seeded in Japanese, and every other catalog (clubs, balls, bags, items, characters) is
    /// English, which is why this was the only strip in the game that stayed Japanese after the
    /// player switched back. The names are not stale translations; they were never translated.
    /// </para>
    /// <para>
    /// The key is the first eight hex digits of the row's uuid rather than a hand-kept id→slug
    /// table, so a NEW catalog row is fixable by publishing one text row — no client build. The
    /// trade is an opaque key in the admin, which the Japanese column makes readable again.
    /// </para>
    /// <para>
    /// A miss falls back to the server's own name. That is deliberate: <see
    /// cref="LocalizationManager.Get"/> returns the KEY when a row is missing, and rendering
    /// <c>GIFT_ITEM_8CBC1D6B</c> in the strip would be worse than rendering ベーシックキャップ.
    /// The fallback is visibly wrong in English, which is the point — it reads as untranslated
    /// content rather than as a broken label.
    /// </para>
    /// </summary>
    public static class GiftItemName
    {
        public const string Prefix = "GIFT_ITEM_";

        /// <summary>The key a catalog row's name is published under, or null when it has no id.</summary>
        public static string? KeyFor(string? itemId)
        {
            if (string.IsNullOrEmpty(itemId) || itemId!.Length < 8) return null;
            return Prefix + itemId.Substring(0, 8).ToUpperInvariant();
        }

        /// <summary>Localized name, falling back to the raw catalog name.</summary>
        public static string Of(GiftItemDto? item)
        {
            if (item == null) return string.Empty;

            string raw = item.Name ?? string.Empty;
            string? key = KeyFor(item.Id);
            if (key == null) return raw;

            string resolved = LocalizationManager.Get(key);
            return resolved == key ? raw : resolved;   // Get() echoes the key on a miss
        }
    }
}
