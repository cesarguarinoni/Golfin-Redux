// Tests for CourseSlugResolver (SPEC §1.5 / §5.6)
// Three EditMode tests:
//   1. Happy path  — valid hole-scene path returns the slug.
//   2. Null return — non-matching path returns null (READ-site contract).
//   3. Throw       — ResolveOrThrow throws on non-matching path (BAKE-site contract, SPEC §5.6).

using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Course.Runtime;

namespace Golfin.Course.Tests
{
    public class CourseSlugResolverTests
    {
        // ── 1. Happy path ────────────────────────────────────────────────────

        [TestCase("Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity",
                  "lomond-country-club")]
        [TestCase("Assets/Golf/Courses/lomond-country-club/Generated/Hole_18_Geo.unity",
                  "lomond-country-club")]
        [TestCase("Assets/Golf/Courses/pebble-beach/Generated/Hole_07_Geo.unity",
                  "pebble-beach")]
        // Backslash variant (Windows path separator)
        [TestCase(@"Assets\Golf\Courses\lomond-country-club\Generated\Hole_05_Geo.unity",
                  "lomond-country-club")]
        public void Resolve_ValidHolePath_ReturnsSlug(string scenePath, string expectedSlug)
        {
            string result = CourseSlugResolver.Resolve(scenePath);
            Assert.AreEqual(expectedSlug, result,
                $"Expected slug '{expectedSlug}' for path '{scenePath}'.");
        }

        // ── 2. Non-matching paths → null ─────────────────────────────────────

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Assets/Scenes/ShellScene.unity")]
        [TestCase("Assets/Golf/Courses/lomond-country-club/Hole_01_Geo.unity")] // missing /Generated/
        [TestCase("Assets/Golf/Courses/Generated/Hole_01_Geo.unity")]            // slug segment is "Generated"
        public void Resolve_NonMatchingPath_ReturnsNull(string scenePath)
        {
            string result = CourseSlugResolver.Resolve(scenePath);
            Assert.IsNull(result,
                $"Expected null for path '{scenePath}', but got '{result}'.");
        }

        // ── 3. ResolveOrThrow → throws InvalidOperationException ─────────────

        [Test]
        public void ResolveOrThrow_NonMatchingPath_ThrowsInvalidOperation()
        {
            // Suppress the expected Debug.LogError so NUnit doesn't treat it as an
            // "unhandled log message" failure (Unity NUnit protection).
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                @"\[CourseSlugResolver\].*Could not extract course slug"));

            Assert.Throws<InvalidOperationException>(() =>
            {
                CourseSlugResolver.ResolveOrThrow("Assets/Scenes/ShellScene.unity",
                    "CourseSlugResolverTests");
            },
            "ResolveOrThrow must throw InvalidOperationException on a non-matching path (SPEC §5.6).");
        }

        [Test]
        public void ResolveOrThrow_ValidPath_ReturnsSlug()
        {
            string slug = CourseSlugResolver.ResolveOrThrow(
                "Assets/Golf/Courses/lomond-country-club/Generated/Hole_03_Geo.unity",
                "CourseSlugResolverTests");
            Assert.AreEqual("lomond-country-club", slug);
        }
    }
}
