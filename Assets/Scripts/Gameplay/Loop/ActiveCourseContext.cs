// Multi-course architecture refactor — SPEC §1.4
// Static bus that broadcasts the currently active course slug.
// Default "lomond-country-club" is the cold-boot fallback so every caller
// works on a single-course project without special-casing.
//
// Runtime load sites and bake sites both read CurrentCourseSlug.
// Bake sites MUST fail loudly when the slug is null or malformed (see
// CourseSlugResolver). The runtime default here is intentional; the bake-side
// default in CourseSlugResolver.cs is NOT allowed — see SPEC §5.6.

using System;
using UnityEngine;

namespace Golfin.Gameplay.Loop
{
    /// <summary>
    /// Lightweight static bus for the currently active course.
    /// Set by <see cref="GameplaySceneLoader"/> before any sim data is loaded.
    /// </summary>
    public static class ActiveCourseContext
    {
        // ── State ──────────────────────────────────────────────────────────────

        private static string _currentCourseSlug        = "lomond-country-club";
        private static string _currentCourseDisplayName = "Lomond Country Club";

        // ── Public read ───────────────────────────────────────────────────────

        /// <summary>
        /// The kebab-case slug that matches the folder under
        /// <c>Assets/Golf/Courses/&lt;slug&gt;/</c> and
        /// <c>Assets/Resources/HoleData/&lt;slug&gt;/</c>.
        /// Default: <c>"lomond-country-club"</c> (cold-boot fallback).
        /// </summary>
        public static string CurrentCourseSlug        => _currentCourseSlug;

        /// <summary>
        /// Human-readable course name for UI display.
        /// </summary>
        public static string CurrentCourseDisplayName => _currentCourseDisplayName;

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Fired after <see cref="Set"/> changes the active course.</summary>
        public static event Action OnCourseChanged;

        // ── Mutators ──────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the active course slug and display name, then fires
        /// <see cref="OnCourseChanged"/>.
        /// </summary>
        /// <param name="slug">
        /// Kebab-case course identifier, e.g. <c>"lomond-country-club"</c>.
        /// Must not be null or empty.
        /// </param>
        /// <param name="displayName">
        /// Human-readable name, e.g. <c>"Lomond Country Club"</c>.
        /// May be empty; it is display-only and never used as a path key.
        /// </param>
        public static void Set(string slug, string displayName)
        {
            if (string.IsNullOrEmpty(slug))
            {
                Debug.LogError("[ActiveCourseContext] Set() called with a null or empty slug — ignored.");
                return;
            }

            _currentCourseSlug        = slug;
            _currentCourseDisplayName = displayName ?? string.Empty;

            OnCourseChanged?.Invoke();
        }

        /// <summary>
        /// Resets to the Lomond default. Intended for test teardown.
        /// </summary>
        public static void Reset()
        {
            _currentCourseSlug        = "lomond-country-club";
            _currentCourseDisplayName = "Lomond Country Club";
            OnCourseChanged?.Invoke();
        }
    }
}
