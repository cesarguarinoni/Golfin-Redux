namespace Golfin.Gameplay.UI.HUD
{
    public static class GameSession
    {
        public static int TurnCount = 1;
        public static event System.Action OnTurnChanged;
        public static void SetTurn(int n) { TurnCount = n; OnTurnChanged?.Invoke(); }
    }
}
