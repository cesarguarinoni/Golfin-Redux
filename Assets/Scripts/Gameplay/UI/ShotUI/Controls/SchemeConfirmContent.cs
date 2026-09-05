using UnityEngine;

namespace Golfin.Gameplay.UI.Controls
{
    /// <summary>
    /// What the scheme confirm pop-up says about each control scheme
    /// (<c>scheme_confirm_popup</c> § 3.1): a title key, three step tiles, three caption keys and
    /// three "how it works" line keys.
    ///
    /// <para>EVERYTHING PLAYER-FACING IS A KEY. The only literals in this file are localisation
    /// keys and Resources paths — no English, no Japanese. The step numbers <c>1 2 3</c> are drawn
    /// by the prefab as typography, not stored here as strings, which is why an
    /// <see cref="Entry"/> has no "1"/"2"/"3" anywhere.</para>
    ///
    /// <para>The tiles are CAPTURED FROM THE RUNNING GAME, not exported from Figma — see
    /// <c>Assets/Editor/ShotUI/SchemeConfirmTilesCapture.cs</c> and the
    /// <c>GOLFIN ▸ Capture ▸ Scheme Confirm Tiles</c> menu. RE-RUN THAT MENU WHENEVER A SCHEME'S
    /// UI CHANGES, or the pop-up will keep explaining the old controls.</para>
    ///
    /// <para>A static table rather than a ScriptableObject on purpose: it is pure content keyed by
    /// an enum the code already switches on, it must be readable from an EditMode test with no
    /// asset loaded, and a missing row has to be a compile-time impossibility rather than an
    /// unassigned Inspector slot (Hard rule 7 — no white-box placeholders).</para>
    ///
    /// <para>It lives in <c>Golfin.Gameplay.UI</c>, next to <see cref="ControlSchemeService"/>
    /// whose <c>LabelKey</c> it mirrors, rather than beside the modal in Assembly-CSharp: an
    /// asmdef cannot reference Assembly-CSharp, so a table left there would be unreachable from
    /// <c>Golfin.Gameplay.Tests</c> and SPEC § 5.2's completeness gate could not exist.</para>
    /// </summary>
    public static class SchemeConfirmContent
    {
        /// <summary>Resources folder holding the twelve captured tiles.</summary>
        public const string TileResourceFolder = "UI/Controls/Tiles";

        /// <summary>Localisation key for the shared gold "HOW IT WORKS" header.</summary>
        public const string HowItWorksKey = "SCHEME_POPUP_HOW";

        /// <summary>Localisation key for the shared muted footer line.</summary>
        public const string FooterKey = "SCHEME_POPUP_FOOTER";

        /// <summary>One scheme's worth of pop-up content. Arrays are always length 3.</summary>
        public struct Entry
        {
            /// <summary>The scheme's Settings label key — the pop-up title reuses it so the two
            /// surfaces can never disagree on what a scheme is called.</summary>
            public string TitleKey;

            /// <summary><c>Resources.Load</c> paths for the three step tiles.</summary>
            public string[] TilePaths;

            /// <summary>Keys for the three tile captions ("PULL", "TIME IT", …).</summary>
            public string[] CaptionKeys;

            /// <summary>Keys for the three numbered "how it works" lines.</summary>
            public string[] LineKeys;
        }

        /// <summary>Enum name used in the string keys and the tile file names. Deliberately the
        /// C# name (<c>Needle</c>, not "TapTiming"): the key set and the twelve PNG names then
        /// derive mechanically from <see cref="ControlScheme"/> and cannot drift from it.</summary>
        static string Token(ControlScheme scheme)
        {
            switch (scheme)
            {
                case ControlScheme.Pendulum:  return "Pendulum";
                case ControlScheme.Needle:    return "Needle";
                case ControlScheme.FreeSwing: return "FreeSwing";
                default:                      return "Flick";
            }
        }

        /// <summary>The content row for a scheme. Total 26 keys across the four schemes plus the
        /// two shared ones; every one of them ships in <c>LocalizationText.csv</c> EN + JA and is
        /// covered by <c>SchemeConfirmContentTests</c>.</summary>
        public static Entry For(ControlScheme scheme)
        {
            string t    = Token(scheme);
            string stem = "SCHEME_POPUP_" + t.ToUpperInvariant();

            return new Entry
            {
                TitleKey = ControlSchemeService.LabelKey(scheme),
                TilePaths = new[]
                {
                    TilePath(scheme, 1), TilePath(scheme, 2), TilePath(scheme, 3)
                },
                CaptionKeys = new[]
                {
                    stem + "_STEP1", stem + "_STEP2", stem + "_STEP3"
                },
                LineKeys = new[]
                {
                    stem + "_LINE1", stem + "_LINE2", stem + "_LINE3"
                },
            };
        }

        /// <summary>Resources path of one tile. <paramref name="step"/> is 1-based, matching both
        /// the caption numeral the player reads and the capture menu's file names.</summary>
        public static string TilePath(ControlScheme scheme, int step)
            => TileResourceFolder + "/T_" + Token(scheme) + "_" + step;

        /// <summary>Load one tile sprite. Returns null when the capture has never been run —
        /// the caller hides the Image rather than drawing a white box (Hard rule 7).</summary>
        public static Sprite LoadTile(ControlScheme scheme, int step)
            => Resources.Load<Sprite>(TilePath(scheme, step));

        /// <summary>Every scheme, for the completeness test and the capture menu.</summary>
        public static readonly ControlScheme[] AllSchemes =
        {
            ControlScheme.Flick, ControlScheme.Pendulum, ControlScheme.Needle, ControlScheme.FreeSwing
        };
    }
}
