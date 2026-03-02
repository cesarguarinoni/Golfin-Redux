using UnityEngine;

namespace GolfinRedux.UI
{
    public class SplashScreenController : MonoBehaviour
    {
        public void OnStartClicked()
        {
            Debug.Log("START clicked – attempting to show Loading");

            var screenManager = FindObjectOfType<ScreenManager>();
            if (screenManager != null)
            {
                screenManager.ShowScreen(ScreenId.Loading, instant: true);
            }
            else
            {
                Debug.LogError("ScreenManager not found in scene");
            }
        }
    }
}
