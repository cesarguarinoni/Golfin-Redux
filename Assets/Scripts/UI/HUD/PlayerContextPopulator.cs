using UnityEngine;
using Golfin.Roster;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.Session;

namespace Golfin.UI.HUD
{
    public class PlayerContextPopulator : MonoBehaviour
    {
        private const string InGamePortraitPath = "Portraits/InGame";
        private const string RarityPath         = "Rarities";

        void OnEnable()
        {
            var mgr = CharacterManager.Instance;
            if (mgr != null)
            {
                mgr.OnCharacterSelected  += OnCharSelected;
                mgr.OnCharacterLeveledUp += OnCharLeveledUp;
            }
            Refresh();
        }

        void OnDisable()
        {
            var mgr = CharacterManager.Instance;
            if (mgr != null)
            {
                mgr.OnCharacterSelected  -= OnCharSelected;
                mgr.OnCharacterLeveledUp -= OnCharLeveledUp;
            }
        }

        void OnCharSelected(string id)   => Refresh();
        void OnCharLeveledUp(string id)  => Refresh();

        void Refresh()
        {
            if (FakeStateLock.IsLocked) return;

            var mgr = CharacterManager.Instance;
            var db  = CharacterDatabaseCSV.Instance;

            if (mgr == null)
            {
                ResetContext();
                return;
            }

            string id = mgr.GetSelectedCharacterId();
            if (string.IsNullOrEmpty(id))
            {
                ResetContext();
                return;
            }

            var rt = db != null ? db.GetCharacter(id) : null;
            var pc = mgr.GetPlayerCharacter(id);

            if (rt != null)
            {
                PlayerContext.DisplayName = (rt.characterName ?? "PLAYER").ToUpperInvariant();

                // In-game portrait (hi-res) — separate from CSV-loaded Thumbnails sprite.
                // Falls back to the thumbnail if the InGame variant is missing.
                Sprite ingame = null;
                if (!string.IsNullOrEmpty(rt.portraitSpriteName))
                    ingame = Resources.Load<Sprite>($"{InGamePortraitPath}/{rt.portraitSpriteName}");
                PlayerContext.Portrait = ingame != null ? ingame : rt.portraitSprite;

                // Rarity background sprite — loaded by enum name from Resources/Rarities/.
                string rarityName = rt.rarity.ToString();
                PlayerContext.RarityBackground = Resources.Load<Sprite>($"{RarityPath}/{rarityName}");
            }
            else
            {
                PlayerContext.DisplayName      = "PLAYER";
                PlayerContext.Portrait         = null;
                PlayerContext.RarityBackground = null;
            }

            if (pc != null)
                PlayerContext.Level = pc.currentLevel;

            PlayerContext.Raise();

            // ── Versus mirror ─────────────────────────────────────────────────
            // When a 1v1 session is active, mirror P1 data into MatchContext.Players[0].
            // This ensures PlayerCardWidget in versus mode reads the same data as solo.
            if (GameSession.IsVersus)
            {
                MatchContext.Players[0] = new MatchContext.Player
                {
                    DisplayName      = PlayerContext.DisplayName,
                    Level            = PlayerContext.Level,
                    Portrait         = PlayerContext.Portrait,
                    RarityBackground = PlayerContext.RarityBackground,
                    TurnCount        = Golfin.Gameplay.Session.GameSession.TurnCount
                };
                MatchContext.Raise();
            }
        }

        void ResetContext()
        {
            PlayerContext.DisplayName      = "PLAYER";
            PlayerContext.Level            = 1;
            PlayerContext.Portrait         = null;
            PlayerContext.RarityBackground = null;
            PlayerContext.Raise();
        }
    }
}
