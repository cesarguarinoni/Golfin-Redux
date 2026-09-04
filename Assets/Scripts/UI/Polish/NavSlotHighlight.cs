// ─────────────────────────────────────────────────────────────────────────────
// game_polish_a §D7 — the bottom-nav SELECTED state, for BOTH bars.
//
// Cesar, 2026-09-03: the cyan tint "looks ugly". What it does today is set
// Image.color = Color.cyan on the whole slot — and a slot is ONE baked sprite
// carrying navy disc, gold ring and white glyph in a single PNG, so tinting it
// turns all three cyan at once. It reads as a broken image, not as a selection.
//
// AFTER: the glyph stays white, the disc stays navy, and the selection is said
// with LIGHT — a gold halo blooming behind the slot and a brighter #FCF195 ring
// laid exactly over the baked one. Both cross-fade in over FadeDur, and the halo
// pulses once so the change draws the eye a single time instead of sitting there
// glowing. Figma has no selected variant to match (`New Nav Bar Buttons`
// 2098:8164 is Property 1=Default only), so the palette is the constraint and
// Docs/Scripts/make_nav_selected.py bakes both sprites from it.
//
// ONE COMPONENT DRIVES BOTH BARS, which is the whole reason Cesar asked for this
// in one task: PersistentUIManager.UpdateScreenHighlight calls SetSelected on the
// five game slots, GpsNavBarHighlight calls it on the five GPS slots, and there
// is no second copy of the behaviour to drift.
//
// ── DEVIATION D-1 · THE CHILDREN ARE MADE AT RUNTIME, NOT AUTHORED ───────────
// §D7.2/3 has a builder add a Glow and a Ring child to PersistentUI.prefab AND
// to the GPS bar. It cannot: the GPS bar is cloned inside all eight GPS screen
// prefabs, so authoring a child there means editing Assets/Prefabs/UI/Gps/**,
// which Cesar's scope rule puts off limits ("The ONLY Gps/ file you may edit is
// GpsNavBarHighlight.cs"). Attach() therefore CREATES the two children the first
// time a slot is highlighted. This is not a shortcut — it is strictly better
// here:
//   · every GPS prefab stays byte-identical (A14 greps for it);
//   · the two bars cannot drift, because there is exactly one place that decides
//     what a highlight is made of;
//   · nothing is added to a REST frame — both children start at alpha 0, and an
//     alpha-0 CanvasGroup cannot move a rest pixel, which is the same argument
//     GpsScreenTransition.EnsureGroup makes for adding CanvasGroups at runtime;
//   · re-running any builder cannot orphan a scene override, because there is no
//     serialized reference to orphan.
// The cost is two GameObjects and two CanvasGroups per slot, created once.
//
// ── WHERE THE SPRITES COME FROM ──────────────────────────────────────────────
// Off PersistentUIManager, exactly as GpsNavBarHighlight already reads its
// COLOURS off it ("One source of truth: retune the Game's highlight and GPS
// follows on the next frame it paints"). Not Resources.Load: a Resources folder
// ships in EVERY build variant including the PLAYLIFE shell, and these two
// sprites belong to whichever build carries the bar. If the shell manager is
// absent the component simply paints nothing, which is the same graceful
// degradation the colour fallback already has.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish
{
    /// <summary>
    /// The selected state of ONE bottom-nav slot: a gold halo behind it and a brighter ring over
    /// it, both cross-faded, the halo pulsing once on select.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavSlotHighlight : MonoBehaviour
    {
        /// <summary>How far past the slot's own edge the halo reaches, in canvas px. Must match
        /// FEATHER in <c>Docs/Scripts/make_nav_selected.py</c> — the sprite was baked with the
        /// falloff filling exactly this much of its own border.</summary>
        public const float HaloFeather = 24f;

        private CanvasGroup? _glowGroup;
        private CanvasGroup? _ringGroup;
        private RectTransform? _glowRect;
        private RectTransform? _ringRect;

        private Coroutine? _glowFade, _ringFade;
        private bool _selected;
        private bool _built;

        /// <summary>
        /// Get (or add) the highlight on a slot and make sure its two children exist.
        ///
        /// <para>THE ONE entry point, called by <c>PersistentUIManager</c> for the game bar and by
        /// <c>GpsNavBarHighlight</c> for the GPS bar, so the two bars are the same thing built the
        /// same way. Returns null when the slot is null or the sprites cannot be resolved — a bar
        /// with no highlight looks like it does today rather than throwing.</para>
        /// </summary>
        public static NavSlotHighlight? Attach(Image? slot)
        {
            if (slot == null) return null;
            var h = slot.GetComponent<NavSlotHighlight>();
            if (h == null) h = slot.gameObject.AddComponent<NavSlotHighlight>();
            h.Build(slot);
            return h;
        }

        private void Build(Image slot)
        {
            if (_built) return;

            Sprite? halo = null, ring = null;
            var shell = PersistentUIManager.Instance;
            if (shell != null)
            {
                // The TEE / CAMERA slot is the big 238 px disc; every other slot is 156 (Character
                // is 158, and a 0.5 px radius difference is below what a 10 px stroke can show —
                // see the baker's header). Choosing on the RECT rather than on the slot's name is
                // what lets the same code serve two bars whose slots are named differently.
                var srt = slot.transform as RectTransform;
                bool big = srt != null && srt.rect.width > 200f;
                halo = big ? shell.navSlotGlowLarge : shell.navSlotGlowSmall;
                ring = big ? shell.navSlotRingLarge : shell.navSlotRingSmall;
            }
            if (halo == null && ring == null) return;   // sprites not wired: leave the bar as it was

            var slotRect = slot.transform as RectTransform;
            float w = slotRect != null ? slotRect.rect.width  : 156f;
            float hgt = slotRect != null ? slotRect.rect.height : 156f;

            // The halo goes BEHIND the slot's own Image. A UGUI child always draws AFTER its
            // parent, so a child can never be behind it — the halo is therefore a SIBLING, pinned
            // to the slot's centre and placed one index earlier. (The alternative, re-parenting the
            // slot, would move a rest pixel.)
            _glowRect  = MakeSibling(slot, "NavSlotGlow", halo, w + 2f * HaloFeather, hgt + 2f * HaloFeather,
                                     behind: true, additive: true);
            // The ring goes OVER the slot, at the sprite's OWN native size rather than the slot's:
            // the ring band is a fixed ~10 px in both bakes and stretching it to a 158 px rect
            // would thicken it by 1.3 %.
            float rw = ring != null ? ring.rect.width  : w;
            float rh = ring != null ? ring.rect.height : hgt;
            _ringRect  = MakeSibling(slot, "NavSlotRing", ring, rw, rh, behind: false, additive: false);

            _glowGroup = _glowRect != null ? _glowRect.GetComponent<CanvasGroup>() : null;
            _ringGroup = _ringRect != null ? _ringRect.GetComponent<CanvasGroup>() : null;
            _built = true;
        }

        private static RectTransform? MakeSibling(Image slot, string name, Sprite? sprite,
                                                  float w, float h, bool behind, bool additive)
        {
            if (sprite == null) return null;
            Transform parent = slot.transform.parent != null ? slot.transform.parent : slot.transform;

            Transform? existing = parent.Find(name + "_" + slot.name);
            RectTransform rt;
            if (existing != null) rt = (RectTransform)existing;
            else
            {
                var go = new GameObject(name + "_" + slot.name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                rt = (RectTransform)go.transform;
                rt.SetParent(parent, worldPositionStays: false);
            }

            var srt = slot.transform as RectTransform;
            if (srt != null)
            {
                rt.anchorMin        = srt.anchorMin;
                rt.anchorMax        = srt.anchorMax;
                rt.pivot            = srt.pivot;
                rt.anchoredPosition = srt.anchoredPosition;
                rt.localScale       = Vector3.one;
            }
            rt.sizeDelta = new Vector2(w, h);
            rt.SetSiblingIndex(behind ? slot.transform.GetSiblingIndex()
                                      : slot.transform.GetSiblingIndex() + 1);

            var img = rt.GetComponent<Image>();
            img.sprite        = sprite;
            img.type          = Image.Type.Simple;   // both bakes are whole shapes, never 9-sliced
            img.raycastTarget = false;               // never steals the slot's own tap
            img.preserveAspect = true;
            if (additive)
            {
                // The same additive material the Home pill's glow uses, so the two glows in the
                // app bloom the same way. Resolved by name off the pill rather than by a
                // serialized reference, so no prefab has to carry a second material slot.
                Material? m = AdditiveMaterial();
                if (m != null) img.material = m;
            }

            var cg = rt.GetComponent<CanvasGroup>();
            cg.alpha          = 0f;                  // REST IS INVISIBLE — A2's 0 px parity
            cg.blocksRaycasts = false;
            cg.interactable   = false;
            return rt;
        }

        private static Material? _additive;
        private static bool _additiveSearched;

        /// <summary>
        /// <c>TapSparkle_Additive</c> — the material on <c>DailyMissionPill/Glow</c>. Found once,
        /// by walking the loaded materials rather than through Resources (which would put it in
        /// every build variant). Null is fine: the halo then draws on the default UI material,
        /// which is dimmer but not wrong.
        /// </summary>
        private static Material? AdditiveMaterial()
        {
            if (_additiveSearched) return _additive;
            _additiveSearched = true;
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m == null || m.name != "TapSparkle_Additive") continue;
                _additive = m;
                break;
            }
            return _additive;
        }

        /// <summary>
        /// Light or unlight this slot.
        /// </summary>
        /// <param name="selected">Whether this slot is the current pillar's.</param>
        /// <param name="animate">False on the first paint after boot — a cold screen should not
        /// animate its chrome into place before the player has done anything.</param>
        public void SetSelected(bool selected, bool animate)
        {
            bool changed = _selected != selected;
            _selected = selected;

            float target = selected ? 1f : 0f;

            if (!animate || !Application.isPlaying)
            {
                UiMotion.Stop(this, ref _glowFade);
                UiMotion.Stop(this, ref _ringFade);
                if (_glowGroup != null) _glowGroup.alpha = target;
                if (_ringGroup != null) _ringGroup.alpha = target;
                return;
            }

            if (_ringGroup != null)
                UiMotion.Run(this, ref _ringFade, UiMotion.Fade(_ringGroup, _ringGroup.alpha, target));

            if (_glowGroup == null) return;

            // ONE pulse, and only when the slot actually just BECAME selected. Re-lighting the
            // slot you are already on — which HighlightScreen does on every ApplyScreen, so
            // several times per navigation — must not re-pulse, or the bar twitches continuously.
            //
            // Fade and pulse are ONE routine rather than two Run()s on the same CanvasGroup: two
            // concurrent tweens writing the same alpha is a race, and the one that happens to
            // finish second wins.
            if (selected && changed)
                UiMotion.Run(this, ref _glowFade, SelectGlow());
            else
                UiMotion.Run(this, ref _glowFade, UiMotion.Fade(_glowGroup, _glowGroup.alpha, target));
        }

        /// <summary>
        /// Fade the halo up, then breathe it once.
        ///
        /// <para>The pulse is <c>min: 1, max: 0.7</c> on purpose — <see cref="UiMotion.Pulse"/>
        /// sweeps min → max → min and settles on MIN, so those arguments give 1 → 0.7 → 1 settling
        /// at full. Passing them the intuitive way round (0.7 → 1 → 0.7) would start with a visible
        /// drop from the alpha the fade just reached, and would leave the halo permanently at
        /// 0.7.</para>
        /// </summary>
        private System.Collections.IEnumerator SelectGlow()
        {
            if (_glowGroup == null) yield break;

            System.Collections.IEnumerator fade = UiMotion.Fade(_glowGroup, _glowGroup.alpha, 1f);
            while (fade.MoveNext()) yield return fade.Current;

            System.Collections.IEnumerator pulse = UiMotion.Pulse(_glowGroup, min: 1f, max: 0.7f, cycles: 1);
            while (pulse.MoveNext()) yield return pulse.Current;

            if (_glowGroup != null) _glowGroup.alpha = 1f;
        }

        private void OnDisable()
        {
            UiMotion.Stop(this, ref _glowFade);
            UiMotion.Stop(this, ref _ringFade);
            // Settle on the state, not on whatever frame the tween died on: the bar is shared
            // chrome and comes back on the next screen exactly as it was left.
            float target = _selected ? 1f : 0f;
            if (_glowGroup != null) _glowGroup.alpha = target;
            if (_ringGroup != null) _ringGroup.alpha = target;
        }

        // ── Read-back seams for the probe and the reviewers ──────────────────
        public bool  IsSelected  => _selected;
        public float GlowAlpha   => _glowGroup != null ? _glowGroup.alpha : -1f;
        public float RingAlpha   => _ringGroup != null ? _ringGroup.alpha : -1f;
        public bool  Built       => _built;
    }
}
