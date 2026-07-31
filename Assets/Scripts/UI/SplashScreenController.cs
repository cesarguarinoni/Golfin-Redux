using System.Collections;
using UnityEngine;
using TMPro;

namespace GolfinRedux.UI
{
    public class SplashScreenController : MonoBehaviour
    {
        public void OnStartClicked()
        {
            Debug.Log("START clicked - attempting to show Loading");

            var manager = FindFirstObjectByType<ScreenManager>();
            if (manager != null)
                manager.ShowScreen(ScreenId.Loading);
            else
                Debug.LogError("ScreenManager not found");
        }

        // demo_build_slice §3.4: the demo boots to this Splash gate. Label the guest button "Play"
        // and remove Create Account (offline demo — guests tap Play to go straight into the game;
        // Login is kept). No-op in the full game.
        private void OnEnable()
        {
            if (GolfinRedux.Demo.DemoGate.IsDemo)
                StartCoroutine(ApplyDemoGate());
        }

        private IEnumerator ApplyDemoGate()
        {
            // Wait one frame so the LocalizedText on the Start label applies first — then override it.
            yield return null;

            var start = transform.Find("StartButton");
            if (start != null)
            {
                var label = start.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    // Disable the auto-localizer so it can't re-set "PLAY" over our "Play".
                    var loc = label.GetComponent("LocalizedText") as Behaviour;
                    if (loc != null) loc.enabled = false;
                    label.text = "Play";
                }
            }

            var create = transform.Find("CreateAccountButton");
            if (create != null) create.gameObject.SetActive(false);
        }

        // You can add public void OnCreateAccountClicked() / OnLoginClicked() later
    }
}
