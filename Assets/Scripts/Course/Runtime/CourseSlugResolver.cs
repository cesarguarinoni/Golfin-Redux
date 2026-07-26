using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Golfin.Course.Runtime
{
    /// <summary>
    /// Extracts the course slug from a hole scene path.
    ///
    /// Expected path pattern:
    ///   Assets/Golf/Courses/&lt;slug&gt;/Generated/Hole_NN_Geo.unity
    ///
    /// READ sites  → call Resolve() and handle null gracefully.
    /// BAKE sites  → call ResolveOrThrow() — fails loudly if slug is absent (SPEC §5.6).
    /// </summary>
    public static class CourseSlugResolver
    {
        // Matches:  Assets/Golf/Courses/<slug>/Generated/Hole_NN_Geo.unity
        private static readonly Regex _pattern = new Regex(
            @"Assets[/\\]Golf[/\\]Courses[/\\](?<slug>[^/\\]+)[/\\]Generated[/\\]Hole_\d{2}_Geo\.unity",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Returns the course slug extracted from <paramref name="scenePath"/>,
        /// or <c>null</c> if the path does not match the expected pattern.
        /// Callers should fall back to a sensible default (e.g. "lomond-country-club") when null.
        /// </summary>
        public static string Resolve(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                return null;

            var m = _pattern.Match(scenePath);
            return m.Success ? m.Groups["slug"].Value : null;
        }

        /// <summary>
        /// Returns the course slug or throws <see cref="InvalidOperationException"/>.
        /// Use this at BAKE sites where a missing slug is a hard error (SPEC §5.6).
        /// </summary>
        /// <param name="scenePath">Active scene path (e.g. from EditorSceneManager.GetActiveScene().path).</param>
        /// <param name="callerTag">Human-readable label included in the error message for fast diagnosis.</param>
        public static string ResolveOrThrow(string scenePath, string callerTag = "")
        {
            string slug = Resolve(scenePath);
            if (slug == null)
            {
                string msg = $"[CourseSlugResolver] {callerTag}: Could not extract course slug from scene path \"{scenePath}\". " +
                             "Open a hole scene under Assets/Golf/Courses/<slug>/Generated/ before baking.";
                Debug.LogError(msg);
                throw new InvalidOperationException(msg);
            }
            return slug;
        }
    }
}
