#!/usr/bin/env python3
"""Bake GPS Profile/Avatar/Badges panel sprites from Figma tokens.

    python3 Docs/Scripts/make_gps_profile_panels.py

Edit THIS, not the PNGs. All three screens share the hub's backdrop
(Home Background.png) and the same blue-card token family as gps_hub_entry.
New panels specific to these three screens are baked here.

Node geometry (measured from Figma via get_metadata, canvas 1170x2532):
  ContentContainer = (96, 361, 978, 1860) — same for all 3 screens.
  All node y values are Figma coords within ContentContainer.
"""
import importlib.util, os, sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
_spec = importlib.util.spec_from_file_location(
    "su_panels", os.path.join(REPO, "Docs", "Scripts", "make_score_upload_panels.py"))
su = importlib.util.module_from_spec(_spec)
try:
    _spec.loader.exec_module(su)
except SystemExit:
    pass

HUB_BG = "Assets/Art/HomeScreen/Home Background.png"

# Background for fitting: step 0 = hub background
BLUE_TOP    = su.BLUE_TOP
BLUE_BOTTOM = su.BLUE_BOTTOM

# Green gradient (same token as Score Upload hero) — used for Avatar Stage
GREEN_TOP    = su.GREEN_TOP
GREEN_BOTTOM = su.GREEN_BOTTOM

# Gift tiles. Node 14025:33385 / :33397 (re-pulled 2026-09-02) are FLAT fills, not
# gradients: `bg-[rgba(58,26,38,0.85)]` and `bg-[rgba(59,47,15,0.85)]`, r32, 3px white
# border. The previous values were invented (#5B1A3A / #4A3810) and read far too
# saturated; #3B2F0F is the same token ScoreUploadScreenBuilder calls PointsBg.
PINK_TOP    = (0x3A, 0x1A, 0x26, 0.85)
PINK_BOTTOM = (0x3A, 0x1A, 0x26, 0.85)

GOLD_TOP    = (0x3B, 0x2F, 0x0F, 0.85)
GOLD_BOTTOM = (0x3B, 0x2F, 0x0F, 0.85)

# Canvas (x,y) = ContentContainer origin (96,361) + panel's Figma x/y within ContentContainer
# (+65 BackRow offset is NOT added here — these are raw Figma positions within ContentContainer)
# All panels measured from Figma node metadata.

# name, w, h, radius, top, bottom, canvas_x, canvas_y
PANELS = [
    # ── Profile screen (14025:33087) ──────────────────────────────────────────
    # canvas_y = ContentContainer origin 361 + node y. (The BackRow, and its +65 offset,
    # were removed 2026-09-02: "back to game" belongs to the hub only.)
    # Radii/sizes are the NODE's, re-pulled 2026-09-02 via get_design_context:
    # every card is `bg-gradient-to-b rgba(19,52,83,.6) -> rgba(9,27,51,.6)`,
    # `border-3 solid #FFF`, r50 on the big cards and r32 on the small tiles.
    #
    # Hero was previously NOT baked — it reused S_HUB_HeroPanel, whose 296px height
    # belongs to the HUB's hero node. This node's hero is 449 (14025:33344), and that
    # 153px shortfall is what pushed the avatar disc on top of the player name.
    ("S_PROF_HeroPanel.png",          958,  449, 50, BLUE_TOP, BLUE_BOTTOM, 106,  361),
    # Trust panel: node 14025:33363 is h140 r50 (was baked h100 r32).
    ("S_PROF_TrustPanel.png",         958,  140, 50, BLUE_TOP, BLUE_BOTTOM, 106,  834),
    # Quick-stat tile: node 14025:33375 — 307.33 x 119, small tile so r32.
    ("S_PROF_QuickStatTile.png",      307,  119, 32, BLUE_TOP, BLUE_BOTTOM, 106,  998),
    # Gift totals — pink (received) / gold (sent). Node 14025:33385 = 470 x 118.
    ("S_PROF_GiftTileReceived.png",   470,  118, 32, PINK_TOP, PINK_BOTTOM, 106, 1141),
    ("S_PROF_GiftTileSent.png",       470,  118, 32, GOLD_TOP, GOLD_BOTTOM, 594, 1141),
    # Shortcut tile: node 14025:33410 — 307.33 x 174, rounded-[32px] (was h190 r50).
    ("S_PROF_ShortcutTile.png",       307,  174, 32, BLUE_TOP, BLUE_BOTTOM, 106, 1283),
    # Recent rounds: node 14025:33440 — 958 x 343 (was 450).
    ("S_PROF_RecentRoundsPanel.png",  958,  343, 50, BLUE_TOP, BLUE_BOTTOM, 106, 1481),

    # ── My Avatar screen (14026:33187) ────────────────────────────────────────
    # Avatar Stage: green gradient, node 14026:33444 = 958 x 840, r50.
    ("S_PROF_AvatarStage.png",        958,  840, 50, GREEN_TOP, GREEN_BOTTOM, 106, 426),
    # XP panel: node 14026:33493 = 958 x 136, r50.
    ("S_PROF_XpPanel.png",            958,  136, 50, BLUE_TOP, BLUE_BOTTOM, 106, 1225),
    # Evolution panel: node 14026:33509 = 958 x 246, r50.
    ("S_PROF_EvolutionPanel.png",     958,  246, 50, BLUE_TOP, BLUE_BOTTOM, 106, 1385),
    # Unlock panel: node 14026:33556 = 958 x 230, r50. Cesar 2026-09-02 restored it (SPEC had
    # it hidden in v1).
    ("S_PROF_UnlockPanel.png",        958,  230, 50, BLUE_TOP, BLUE_BOTTOM, 106, 1655),
    # Status panel: node 14026:33586 = 958 x 272, r50 (one row taller here — four roster stats).
    # Unlock TILE — node 14026:33561: each unlock is its own card (r28, 3px white border),
    # 287.33 x 147 inside the Unlock Row. Missing entirely from the first build.
    ("S_PROF_UnlockTile.png",         287,  147, 28, BLUE_TOP, BLUE_BOTTOM, 138, 1716),
    ("S_PROF_StatusPanel.png",        958,  320, 50, BLUE_TOP, BLUE_BOTTOM, 106, 1909),

    # ── Badges screen (14027:33298) ───────────────────────────────────────────
    # Collection panel: node 14027:33555 = 958 x 139, r50.
    ("S_PROF_CollectionPanel.png",    958,  139, 50, BLUE_TOP, BLUE_BOTTOM, 106,  361),
    # Section panel: GOLF/SOCIAL are 398 tall, TRUST/SPECIAL 233. Bake the tall one
    # and 9-slice it; r50 to match the other big cards.
    ("S_PROF_SectionPanel.png",       958,  398, 50, BLUE_TOP, BLUE_BOTTOM, 106,  524),
]



def bake_frame(path, w, h, radius, stroke, scale=2):
    """A rounded-rect OUTLINE in solid white with a fully transparent interior, baked at 2x
    and 9-sliceable, so it can be TINTED per badge rarity at runtime.

    The badge cell needs a 2px border in the rarity colour (earned) or a 1px #4a5a6e border
    (locked) over a translucent fill. A 9-sliced S_PillStadium cannot do that — it is a SOLID
    capsule, so tinting it paints the whole cell and destroys the fill underneath, which is
    exactly the bug this replaces. Baking white and tinting is the same trick the score-upload
    pills use.
    """
    from PIL import Image, ImageDraw
    W, H, R, S = w * scale, h * scale, radius * scale, max(1, int(round(stroke * scale)))
    img = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    # Draw the outline by filling the outer rounded rect and cutting an inset one back out,
    # so the corners stay true arcs at any stroke width.
    d.rounded_rectangle([0, 0, W - 1, H - 1], radius=R, fill=(255, 255, 255, 255))
    d.rounded_rectangle([S, S, W - 1 - S, H - 1 - S], radius=max(0, R - S), fill=(255, 255, 255, 0))
    img.save(path)
    return W, H


def bake_pill(path, w, h, fill_alpha, stroke, scale=2):
    """A capsule: translucent white fill + a 1px solid white border, baked at 2x and tinted at
    runtime. Node 14026:33490 is `bg rgba(238,220,154,.18)` + `border 1px #eedc9a` + r100 — fill
    and border are the SAME hue, so baking white and tinting gold reproduces both exactly. Same
    trick the score-upload pills use.
    """
    from PIL import Image, ImageDraw
    W, H, S = w * scale, h * scale, max(1, int(round(stroke * scale)))
    R = H // 2
    img = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, W - 1, H - 1], radius=R, fill=(255, 255, 255, 255))
    d.rounded_rectangle([S, S, W - 1 - S, H - 1 - S], radius=max(0, R - S),
                        fill=(255, 255, 255, int(round(fill_alpha * 255))))
    img.save(path)
    return W, H


PILLS = [
    # name, w, h, fill alpha, stroke  — node 14026:33490
    ("S_PROF_LevelPill.png", 99, 45, 0.18, 1),
]

FRAMES = [
    # name, w, h, radius, stroke  — node 14027:33578 (earned, 2px) / :33611 (locked, 1px), r24
    ("S_PROF_BadgeFrame2.png", 220, 153, 24, 2),
    ("S_PROF_BadgeFrame1.png", 220, 153, 24, 1),
]


def main():
    su.BACKGROUNDS[0] = HUB_BG
    su._BG_CACHE.clear()

    for name, w, h, fa, stroke in PILLS:
        W, H = bake_pill(os.path.join(su.OUT_DIR, name), w, h, fa, stroke)
        print("%-34s %dx%d (node %dx%d fill a=%.2f stroke %d) -> white capsule, tint at runtime"
              % (name, W, H, w, h, fa, stroke))

    for name, w, h, r, stroke in FRAMES:
        W, H = bake_frame(os.path.join(su.OUT_DIR, name), w, h, r, stroke)
        print("%-34s %dx%d (node %dx%d r%d stroke %d) -> white outline, tint at runtime"
              % (name, W, H, w, h, r, stroke))

    for name, w, h, r, top, bottom, cx, cy in PANELS:
        fit = su.fit_over_background(0, (cx, cy, w, h), top, bottom)
        W, H = su.bake_card(os.path.join(su.OUT_DIR, name), w, h, r, top, bottom, fit=fit)
        if fit:
            rgb, a = fit
            print("%-34s %dx%d (node %dx%d r%d) -> rgb(%d,%d,%d) a=%.3f"
                  % (name, W, H, w, h, r, rgb[0], rgb[1], rgb[2], a))
        else:
            print("%-34s %dx%d (node %dx%d r%d) -> opaque" % (name, W, H, w, h, r))


if __name__ == "__main__":
    sys.exit(main())
