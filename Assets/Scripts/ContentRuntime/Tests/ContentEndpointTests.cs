using Golfin.Net;
using NUnit.Framework;

namespace Golfin.Content.Tests
{
    /// <summary>
    /// The request this build actually sends. Pinned because all three parameters are load-bearing
    /// and two of them are silent when wrong: a bare-int <c>since</c> would apply to every catalog
    /// (the lossy scalar <c>content_cursor_per_catalog</c> exists to remove), and a missing
    /// <c>catalogs</c> would pull the 275 KB clubs catalog onto the boot path for a build that
    /// reads none of it.
    /// </summary>
    public class ContentEndpointTests
    {
        [TearDown]
        public void TearDown() => Endpoints.ResetToDefault();

        [Test]
        public void Content_UsesThePerCatalogCursorForm_AndNarrowsToTexts()
        {
            string url = Endpoints.Content("texts:11", 2297, "texts");

            StringAssert.Contains("/api/v1/content?", url);
            // UnityWebRequest.EscapeURL emits LOWER-case hex ("%3a"), which the server's
            // parse_since unescapes identically — so the assertion is case-insensitive rather
            // than pinned to a casing the escaper does not produce.
            StringAssert.Contains("since=texts%3a11", url.ToLowerInvariant(),
                "The colon must be escaped, not dropped — an unescaped ':' would be a malformed query.");
            StringAssert.Contains("build=2297", url);
            StringAssert.Contains("catalogs=texts", url);
            Assert.IsFalse(url.Contains("/content/"), "No trailing slash — the bare path is the 200.");
        }

        [Test]
        public void Content_WithoutCatalogs_KeepsTheOldTwoArgShape()
        {
            string url = Endpoints.Content("texts:11", 2297);

            Assert.IsFalse(url.Contains("catalogs="),
                "The parameter is optional so the pre-existing two-arg call site shape is unchanged; " +
                "omitting it asks for every catalog, which is the server's documented default.");
        }

        [Test]
        public void Content_WithBuildZero_StillWellFormed()
        {
            // Parse failure sends 0 — the safe end. It must produce a valid request, not a broken one.
            StringAssert.Contains("build=0", Endpoints.Content("texts:11", 0, "texts"));
        }
    }
}
