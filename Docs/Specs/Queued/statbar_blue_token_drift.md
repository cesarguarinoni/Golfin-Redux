# QUEUED — stat-bar blue token drift (three different blues in play)

**Filed:** 2026-07-14 (found during `gacha_history` Stage 0; Cesar asked it be recorded).
**Type:** design-token cleanup. Low urgency, zero user-visible impact today.

## The drift

The "normal" stat-bar blue is defined in three places with three different values:

| Source | Value | Notes |
|---|---|---|
| Figma node `4079:18306` (sampled off the node render) | `#387FDF` | design truth |
| `BagClubCard.cs` → `StatBarColor = new Color(0.2f, 0.5f, 0.9f, 1f)` | `#3380E6` | what actually ships at runtime |
| `ItemUseClubCard.prefab` (Bar) | `#3380E6` | matches the code |
| `GeneralShopCard_Club.prefab` (Fill) | `#3B7DDB` | a third value |

Track colour is consistent: `#182430` (node token and render agree).

## Why it's not urgent
The three blues differ by only a few RGB units — imperceptible side by side. Nothing is visibly wrong.

## What to do (when someone cares)
Pick ONE token (recommend the shipped `#3380E6`, or reconcile design to it), define it once (e.g. a
`RarityHelper`-style static or a design-token asset), and have `BagClubCard`, `ItemUseClubCard` and
`GeneralShopCard_Club` all read from it instead of hardcoding.

## Related
- `Docs/Specs/Active/gacha_history/STAGE1_NOTES.md` — the Stage-1 requirement to remove the hardcoded
  bar colour from the static history rows so the runtime binder owns it (that hardcode would otherwise
  mask the red low-durability state).
