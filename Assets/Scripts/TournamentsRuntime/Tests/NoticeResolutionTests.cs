// ─────────────────────────────────────────────────────────────────────────────
// NoticeResolutionTests — the Home notice ladder (home_notices SPEC §6)
//
// ASSEMBLY: Golfin.TournamentsRuntime.Tests (named EditMode test asmdef)
//
// Same access pattern as BannerPolicyTests, and for the same reason: the
// production types live in Assembly-CSharp (Assets/Scripts/NoticesRuntime/),
// which an asmdef cannot reference. They are reached by REFLECTION; everything
// asserted is a primitive or a string, so the assertions need no casting games.
//
// COVERAGE
//   §1  Resolution ladder — expiry, per-field locale choice, drop conditions
//   §2  Wire parsing      — envelope vs bare payload, empty list, malformed body,
//                           expires_at kept verbatim for absolute-UTC parsing
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Tournaments.WireupTests
{
    /// <summary>Reflection handles onto the Assembly-CSharp notice types.</summary>
    internal static class NoticeProd
    {
        private static Type Find(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            throw new InvalidOperationException(
                $"Production type '{fullName}' not found. It should live in Assembly-CSharp " +
                "(Assets/Scripts/NoticesRuntime/, no asmdef).");
        }

        internal static readonly Type Service = Find("Golfin.Notices.NoticeService");

        /// <summary>The production ladder, called exactly as <c>RebuildPages</c> calls it.</summary>
        internal static (bool Kept, string Title, string Body) Resolve(
            string? titleEn, string? titleJa,
            string? bodyEn,  string? bodyJa,
            bool japanese, DateTime? expiresAtUtc, DateTime nowUtc)
        {
            var m = Service.GetMethod("TryResolve", BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("NoticeService.TryResolve not found.");

            object?[] args = { titleEn, titleJa, bodyEn, bodyJa, japanese, expiresAtUtc, nowUtc, null, null };
            bool kept = (bool)m.Invoke(null, args)!;
            return (kept, (string?)args[7] ?? "", (string?)args[8] ?? "");
        }

        internal static DateTime? ParseUtc(string? value)
        {
            var m = Service.GetMethod("ParseUtc", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (DateTime?)m.Invoke(null, new object?[] { value });
        }

        /// <summary>Deserialize a raw or unwrapped body; null when it did not map at all.</summary>
        private static object? Deserialize(string? json)
        {
            var m = Service.GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Static)!;
            // NoticeSource.DiskCache — the second arg is only used in the failure log.
            object source = Enum.Parse(Find("Golfin.Notices.NoticeSource"), "DiskCache");
            return m.Invoke(null, new object?[] { json, source });
        }

        /// <summary>True when the payload mapped at all (as opposed to returning null).</summary>
        internal static bool Maps(string? json) => Deserialize(json) != null;

        /// <summary>The rows, in order, as (titleEn, titleJa, bodyEn, bodyJa, expiresAt) tuples.</summary>
        internal static List<(string? TitleEn, string? TitleJa, string? BodyEn, string? BodyJa, string? ExpiresAt)>
            Rows(string? json)
        {
            var result = new List<(string?, string?, string?, string?, string?)>();

            object? dto = Deserialize(json);
            if (dto == null) return result;

            var list = (System.Collections.IList?)dto.GetType().GetField("Notices")!.GetValue(dto);
            if (list == null) return result;

            foreach (object? row in list)
            {
                if (row == null) continue;
                Type t = row.GetType();
                result.Add((
                    (string?)t.GetField("TitleEn")!.GetValue(row),
                    (string?)t.GetField("TitleJa")!.GetValue(row),
                    (string?)t.GetField("BodyEn")!.GetValue(row),
                    (string?)t.GetField("BodyJa")!.GetValue(row),
                    (string?)t.GetField("ExpiresAt")!.GetValue(row)));
            }
            return result;
        }

        /// <summary>True when the payload mapped AND carried a (possibly empty) notices array.</summary>
        internal static bool HasNoticesArray(string? json)
        {
            object? dto = Deserialize(json);
            if (dto == null) return false;
            return dto.GetType().GetField("Notices")!.GetValue(dto) != null;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §1  Resolution ladder
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class NoticeResolutionTests
    {
        private static readonly DateTime Now = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

        private Language _savedLanguage;

        [SetUp]
        public void SetUp() => _savedLanguage = LocalizationManager.CurrentLanguage;

        /// <summary>
        /// <c>TryResolve</c> takes the language as a plain bool and never touches
        /// <c>LocalizationManager</c>, so nothing here should be able to move it. Restored anyway:
        /// language is global static state shared with whatever UI is alive in the editor, and a
        /// test that ever starts touching it must not leak a JP switch into the next fixture.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (LocalizationManager.CurrentLanguage != _savedLanguage)
            {
                LocalizationManager.Initialize(
                    ScriptableObject.CreateInstance<LocalizationTextTable>(), _savedLanguage);
            }
        }

        [Test]
        public void Japanese_player_with_both_locales_gets_Japanese()
        {
            var r = NoticeProd.Resolve(
                "MAINTENANCE NOTICE", "メンテナンス情報",
                "Servers down 2026/08/28", "サーバー停止 2026/08/28",
                japanese: true, expiresAtUtc: null, nowUtc: Now);

            Assert.IsTrue(r.Kept);
            Assert.AreEqual("メンテナンス情報", r.Title);
            Assert.AreEqual("サーバー停止 2026/08/28", r.Body);
        }

        [Test]
        public void English_player_with_both_locales_gets_English()
        {
            var r = NoticeProd.Resolve(
                "MAINTENANCE NOTICE", "メンテナンス情報",
                "Servers down 2026/08/28", "サーバー停止 2026/08/28",
                japanese: false, expiresAtUtc: null, nowUtc: Now);

            Assert.IsTrue(r.Kept);
            Assert.AreEqual("MAINTENANCE NOTICE", r.Title);
            Assert.AreEqual("Servers down 2026/08/28", r.Body);
        }

        [Test]
        public void Japanese_player_falls_back_per_field_not_per_row()
        {
            // A Japanese heading over English copy is correct and better than dropping either half.
            var r = NoticeProd.Resolve(
                "MAINTENANCE NOTICE", null,
                "Servers down 2026/08/28", "サーバー停止 2026/08/28",
                japanese: true, expiresAtUtc: null, nowUtc: Now);

            Assert.IsTrue(r.Kept);
            Assert.AreEqual("MAINTENANCE NOTICE", r.Title, "title_ja is null → English title.");
            Assert.AreEqual("サーバー停止 2026/08/28", r.Body, "body_ja is present → Japanese body.");
        }

        [Test]
        public void Blank_is_treated_as_absent_not_as_copy()
        {
            var r = NoticeProd.Resolve(
                "MAINTENANCE NOTICE", "   ",
                "Servers down", "\t",
                japanese: true, expiresAtUtc: null, nowUtc: Now);

            Assert.IsTrue(r.Kept);
            Assert.AreEqual("MAINTENANCE NOTICE", r.Title);
            Assert.AreEqual("Servers down", r.Body);
        }

        [Test]
        public void English_player_never_falls_into_Japanese()
        {
            var r = NoticeProd.Resolve(
                null, "メンテナンス情報",
                null, "サーバー停止",
                japanese: false, expiresAtUtc: null, nowUtc: Now);

            Assert.IsFalse(r.Kept, "An English player must never be shown Japanese copy.");
        }

        [Test]
        public void English_player_keeps_the_row_when_only_one_English_field_exists()
        {
            var titleOnly = NoticeProd.Resolve(
                "MAINTENANCE NOTICE", "メンテナンス情報", null, "サーバー停止",
                japanese: false, expiresAtUtc: null, nowUtc: Now);

            Assert.IsTrue(titleOnly.Kept, "An English title alone is still a notice.");
            Assert.AreEqual("MAINTENANCE NOTICE", titleOnly.Title);
            Assert.AreEqual("", titleOnly.Body);

            var bodyOnly = NoticeProd.Resolve(
                null, "メンテナンス情報", "Servers down", "サーバー停止",
                japanese: false, expiresAtUtc: null, nowUtc: Now);

            Assert.IsTrue(bodyOnly.Kept);
            Assert.AreEqual("", bodyOnly.Title);
            Assert.AreEqual("Servers down", bodyOnly.Body);
        }

        [Test]
        public void Expired_is_dropped_in_either_language()
        {
            DateTime past = Now.AddMinutes(-1);

            foreach (bool japanese in new[] { true, false })
            {
                var r = NoticeProd.Resolve(
                    "MAINTENANCE NOTICE", "メンテナンス情報",
                    "Servers down", "サーバー停止",
                    japanese, expiresAtUtc: past, nowUtc: Now);

                Assert.IsFalse(r.Kept, $"Expired must drop regardless of language (japanese={japanese}).");
            }
        }

        [Test]
        public void Expiry_is_exclusive_at_the_boundary()
        {
            // end_at is EXCLUSIVE: at exactly end_at the notice is already over.
            Assert.IsFalse(
                NoticeProd.Resolve("T", null, "B", null, false, Now, Now).Kept,
                "now == expires_at must drop.");

            Assert.IsTrue(
                NoticeProd.Resolve("T", null, "B", null, false, Now.AddSeconds(1), Now).Kept,
                "One second before expiry the notice is still live.");
        }

        [Test]
        public void No_expiry_never_expires()
        {
            Assert.IsTrue(
                NoticeProd.Resolve("T", null, "B", null, false, null, Now.AddYears(50)).Kept);
        }

        [Test]
        public void Empty_title_and_body_is_dropped()
        {
            Assert.IsFalse(NoticeProd.Resolve(null, null, null, null, false, null, Now).Kept);
            Assert.IsFalse(NoticeProd.Resolve("", "", "", "", true, null, Now).Kept);
            Assert.IsFalse(NoticeProd.Resolve("  ", null, "\n", null, false, null, Now).Kept,
                "Whitespace-only is not copy.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §2  Wire parsing
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class NoticeWireParsingTests
    {
        private const string Payload =
            "{\"fetched_at\":\"2026-08-18T04:10:00+00:00\",\"notices\":[" +
            "{\"title_en\":\"MAINTENANCE NOTICE\",\"title_ja\":\"メンテナンス情報\"," +
            "\"body_en\":\"Scheduled server maintenance: 2026/08/28\",\"body_ja\":\"定期サーバーメンテナンス\"," +
            "\"expires_at\":\"2026-08-29T00:00:00+00:00\"}," +
            "{\"title_en\":\"SECOND\",\"title_ja\":null,\"body_en\":\"Body two\",\"body_ja\":null," +
            "\"expires_at\":null}]}";

        private static string Enveloped => "{\"data\":" + Payload + "}";

        [Test]
        public void Enveloped_cache_body_and_bare_live_payload_both_parse_identically()
        {
            var fromCache = NoticeProd.Rows(Enveloped);
            var fromLive  = NoticeProd.Rows(Payload);

            Assert.AreEqual(2, fromCache.Count, "The disk cache holds the RAW enveloped body.");
            Assert.AreEqual(2, fromLive.Count, "ApiEnvelope already unwrapped a live fetch.");
            CollectionAssert.AreEqual(fromCache, fromLive);
        }

        [Test]
        public void Order_is_preserved_because_sort_order_decides_page_one()
        {
            var rows = NoticeProd.Rows(Enveloped);
            Assert.AreEqual("MAINTENANCE NOTICE", rows[0].TitleEn);
            Assert.AreEqual("SECOND", rows[1].TitleEn);
        }

        [Test]
        public void Both_locales_arrive_and_null_ja_stays_null()
        {
            var rows = NoticeProd.Rows(Enveloped);
            Assert.AreEqual("メンテナンス情報", rows[0].TitleJa);
            Assert.AreEqual("定期サーバーメンテナンス", rows[0].BodyJa);
            Assert.IsNull(rows[1].TitleJa, "null ja means 'fall back to English', not empty string.");
            Assert.IsNull(rows[1].BodyJa);
        }

        [Test]
        public void Expires_at_survives_as_the_exact_characters_the_server_sent()
        {
            // DateParseHandling.None is what keeps ParseUtc the only place a timestamp is
            // interpreted; without it Newtonsoft converts to a LOCAL DateTime en route.
            var rows = NoticeProd.Rows(Enveloped);
            Assert.AreEqual("2026-08-29T00:00:00+00:00", rows[0].ExpiresAt);
            Assert.IsNull(rows[1].ExpiresAt);

            DateTime? parsed = NoticeProd.ParseUtc(rows[0].ExpiresAt);
            Assert.IsTrue(parsed.HasValue);
            Assert.AreEqual(DateTimeKind.Utc, parsed!.Value.Kind);
            Assert.AreEqual(new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc), parsed.Value);
        }

        [Test]
        public void Offset_timestamps_land_on_the_same_absolute_instant()
        {
            Assert.AreEqual(
                NoticeProd.ParseUtc("2026-08-29T09:00:00+09:00"),
                NoticeProd.ParseUtc("2026-08-29T00:00:00+00:00"),
                "A player in UTC+9 and one in UTC must see the same notice expire at the same instant.");
        }

        [Test]
        public void An_empty_notices_array_is_a_healthy_response_not_a_failure()
        {
            const string empty = "{\"data\":{\"fetched_at\":\"2026-08-18T04:10:00+00:00\",\"notices\":[]}}";

            Assert.IsTrue(NoticeProd.HasNoticesArray(empty),
                "An empty array must APPLY (and hide the panel), not be discarded as unmappable.");
            Assert.AreEqual(0, NoticeProd.Rows(empty).Count);
        }

        [Test]
        public void Malformed_json_maps_to_nothing_so_the_current_set_survives()
        {
            // Each of these logs one warning by design; a warning does not fail a Unity test, so
            // there is deliberately no LogAssert scope here — the point is that Maps() is false.
            foreach (string bad in new[] { "{not json", "[", "{\"data\":", "�" })
                Assert.IsFalse(NoticeProd.Maps(bad), $"Malformed body must map to null: '{bad}'");
        }

        [Test]
        public void Absent_body_maps_to_nothing()
        {
            Assert.IsFalse(NoticeProd.Maps(null));
            Assert.IsFalse(NoticeProd.Maps(""));
            Assert.IsFalse(NoticeProd.Maps("   "));
            Assert.IsFalse(NoticeProd.Maps("{\"data\":null}"));
        }

        [Test]
        public void A_payload_without_a_notices_key_changes_nothing()
        {
            // Distinct from an empty array: no key at all means this build could not read the
            // payload, so Apply must decline and leave the previous set alone.
            Assert.IsFalse(NoticeProd.HasNoticesArray("{\"data\":{\"fetched_at\":\"2026-08-18T04:10:00+00:00\"}}"));
        }
    }
}
