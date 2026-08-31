# Gacha reveal SFX — placeholder set (2026-08-31)

All clips are built from CC0 (public-domain) sources — no attribution required, commercial use
OK. Mixed/trimmed/loudness-matched (−16 LUFS, −1.5 dBTP) with ffmpeg. Replace freely; keep the
file names so `SfxLibrary.asset` mappings survive.

| File | SfxId | Built from | Source (CC0) |
|---|---|---|---|
| `Gacha_BagDrop.ogg` | `GachaBagDrop` | `dropLeather` + `impactSoft_heavy_001` | Kenney RPG Audio, Kenney Impact Sounds |
| `Gacha_BagShake.ogg` | `GachaBagShake` | `cloth2` + `chips-handle-3` + `dice-shake-2`, trimmed 1.1 s | Kenney RPG Audio, Kenney Casino Audio |
| `Gacha_CardPop.ogg` | `GachaCardPop` | `cards-pack-take-out-1` + `phaserUp2` | Kenney Casino Audio, Kenney Digital Audio |
| `Gacha_CardLand.ogg` | `GachaCardLand` | `card-place-2` + `glass_002` | Kenney Casino Audio, Kenney Interface Sounds |
| `Gacha_CardExit.ogg` | `GachaCardExit` | `card-shove-2` + `minimize_003` | Kenney Casino Audio, Kenney Interface Sounds |
| `Gacha_Skip.ogg` | `GachaSkip` | `back_002` | Kenney Interface Sounds |
| `Gacha_RevealUncommon.ogg` | `GachaRevealUncommon` | `confirmation_002` | Kenney Interface Sounds |
| `Gacha_RevealRare.ogg` | `GachaRevealRare` | `jingles_STEEL09` + `glass_004` | Kenney Music Jingles, Kenney Interface Sounds |
| `Gacha_RevealMythic.ogg` | `GachaRevealMythic` | `jingles_STEEL03` + `powerUp5` | Kenney Music Jingles, Kenney Digital Audio |
| `Gacha_RevealLegendary.ogg` | `GachaRevealLegendary` | `jingles_HIT11` + `impactBell_heavy_001` | Kenney Music Jingles, Kenney Impact Sounds |
| `Gacha_RevealSupreme.ogg` | `GachaRevealSupreme` | `jingles_HIT15` + `impactBell_heavy_003` + `jingles_HIT03` (+0.9 s) + `glass_006` + `powerUp8` | Kenney Music Jingles / Impact / Interface / Digital |
| `Gacha_RevealComplete.ogg` | `GachaRevealComplete` | `Victory.wav` trimmed to 4.3 s, 0.5 s fade | OpenGameArt "Victory" (CC0) https://opengameart.org/content/victory |

Kenney packs: https://kenney.nl/assets/ (casino-audio, ui-audio, interface-sounds, impact-sounds,
digital-audio, music-jingles, rpg-audio) — all "Creative Commons Zero, CC0".

Import settings (Unity): match the existing SFX metas in `Assets/Sounds/Hit/` (Load Type =
Decompress On Load, Compression = Vorbis, quality 1.0, Force To Mono off, Preload off).
