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
                mgr.OnCharacterSelected += OnSelChanged;
                PullAndPublish();
            }
        }

        void OnDisable()
        {
            var mgr = CharacterManager.Instance;
            if (mgr != null) mgr.OnCharacterSelected -= OnSelChanged;
        }

        void OnSelChanged(string _) => PullAndPublish();

        void PullAndPublish()
        {
            var mgr = CharacterManager.Instance;
            var db  = CharacterDatabaseCSV.Instance;
            if (mgr == null) return;

            string id = mgr.GetSelectedCharacterId();
            if (string.IsNullOrEmpty(id)) { PlayerContext.Reset(); return; }

            var rt = db != null ? db.GetCharacter(id) : null;
            var pc = mgr.GetPlayerCharacter(id);

            if (rt != null)
            {
                PlayerContext.DisplayName = rt.characterName.ToUpper();
                PlayerContext.Portrait    = rt.portraitSprite;
            }
            if (pc != null)
            {
                PlayerContext.Level = pc.currentLevel;
            }
            PlayerContext.Raise();
        }
    }
}
