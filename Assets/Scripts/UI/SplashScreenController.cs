using UnityEngine;

namespace GolfinRedux.UI
{
    public class SplashScreenController : MonoBehaviour
    {
        public void OnStartClicked()
        {
            Debug.Log("START clicked – attempting to show Loading");

            var manager = FindObjectOfType<ScreenManager>();
            if (manager != null)
                manager.ShowScreen(ScreenId.Loading);
            else
                Debug.LogError("ScreenManager not found");
        }

        // You can add public void OnCreateAccountClicked() / OnLoginClicked() later
    }
}
