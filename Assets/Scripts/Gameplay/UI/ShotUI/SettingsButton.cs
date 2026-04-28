using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    [RequireComponent(typeof(Button))]
    public class SettingsButton : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() => Debug.Log("[Settings] tapped"));
        }
    }
}
