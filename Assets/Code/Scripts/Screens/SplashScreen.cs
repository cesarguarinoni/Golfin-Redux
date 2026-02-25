using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Splash screen — game art background with START and CREATE ACCOUNT buttons.
/// Buttons trigger UnityEvents for flexibility.
/// </summary>
public class SplashScreen : ScreenBase
{
    [Header("Button References")]
    [SerializeField] private PressableButton startButton;
    [SerializeField] private PressableButton createAccountButton;
    
    [Header("Events")]
    public UnityEvent OnStartPressed;
    public UnityEvent OnCreateAccountPressed;
    
    public override void OnScreenEnter()
    {
        if (startButton != null)
            startButton.onClick.AddListener(HandleStart);
        if (createAccountButton != null)
            createAccountButton.onClick.AddListener(HandleCreateAccount);
    }
    
    public override void OnScreenExit()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(HandleStart);
        if (createAccountButton != null)
            createAccountButton.onClick.RemoveListener(HandleCreateAccount);
    }
    
    private void HandleStart()
    {
        Debug.Log("[SplashScreen] START pressed");
        OnStartPressed?.Invoke();
    }
    
    private void HandleCreateAccount()
    {
        Debug.Log("[SplashScreen] CREATE ACCOUNT pressed");
        OnCreateAccountPressed?.Invoke();
    }
}
