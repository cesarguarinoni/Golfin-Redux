using UnityEngine;
using Golfin.Roster;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.UI.HUD
{
    public class PlayerContextPopulator : MonoBehaviour
    {
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
            var mgr = CharacterManager.Instance;
            var db  = CharacterDatabaseCSV.Instance;

            if (mgr == null)
            {
                PlayerContext.DisplayName = "PLAYER";
                PlayerContext.Level       = 1;
                PlayerContext.Portrait    = null;
                PlayerContext.Raise();
                return;
            }

            string id = mgr.GetSelectedCharacterId();
            if (string.IsNullOrEmpty(id))
            {
                PlayerContext.DisplayName = "PLAYER";
                PlayerContext.Level       = 1;
                PlayerContext.Portrait    = null;
                PlayerContext.Raise();
                return;
            }

            var rt = db != null ? db.GetCharacter(id) : null;
            var pc = mgr.GetPlayerCharacter(id);

            if (rt != null)
            {
                PlayerContext.DisplayName = (rt.characterName ?? "PLAYER").ToUpperInvariant();
                PlayerContext.Portrait    = rt.portraitSprite;
            }
            else
            {
                PlayerContext.DisplayName = "PLAYER";
                PlayerContext.Portrait    = null;
            }

            if (pc != null)
                PlayerContext.Level = pc.currentLevel;

            PlayerContext.Raise();
        }
    }
}
