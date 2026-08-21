using System.Text.RegularExpressions;

namespace Golfin.UI.Account
{
    /// <summary>
    /// The single rule for what counts as a valid display name.
    ///
    /// Two screens let a player set their name — Create Username (first login) and Settings →
    /// User Profile — and they used to disagree: 3–20 without spaces on one, 3–16 with spaces on
    /// the other. A name accepted in Settings could therefore be one the sign-up flow would have
    /// rejected. Both now call in here.
    ///
    /// Note there is no uniqueness guarantee: the name is stored in Supabase Auth
    /// <c>user_metadata.display_name</c>, which carries no constraint, so two players can hold the
    /// same name. Don't let the UI promise otherwise.
    /// </summary>
    public static class UsernameRules
    {
        public const int MinLength = 3;
        public const int MaxLength = 20;

        private static readonly Regex Pattern = new Regex(@"^[A-Za-z0-9_]{3,20}$");

        /// <summary>Player-facing description of the rule; matches the on-screen hint.
        /// Localised via AUTH_USERNAME_REQUIREMENT (EN/JP in LocalizationText.csv). A property,
        /// not a const: a const would bake the English into every call site at compile time.</summary>
        public static string Requirement => LocalizationManager.Get("AUTH_USERNAME_REQUIREMENT");

        public static bool IsValid(string username)
        {
            return !string.IsNullOrEmpty(username) && Pattern.IsMatch(username);
        }
    }
}
