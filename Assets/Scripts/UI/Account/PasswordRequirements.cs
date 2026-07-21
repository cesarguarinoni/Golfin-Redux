// Order: login_signup_screens — Phase 1 (UI only)
// Pure C# helper — no Unity dependencies.
namespace Golfin.UI.Account
{
    /// <summary>
    /// Client-side password requirement checker.
    /// Advisory only — the server is the source of truth (Phase 2).
    /// </summary>
    public static class PasswordRequirements
    {
        public readonly struct Result
        {
            public readonly bool Len8;
            public readonly bool HasLower;
            public readonly bool HasUpper;
            public readonly bool HasDigit;
            public readonly bool HasSpecial;

            public Result(bool len8, bool lower, bool upper, bool digit, bool special)
            {
                Len8       = len8;
                HasLower   = lower;
                HasUpper   = upper;
                HasDigit   = digit;
                HasSpecial = special;
            }

            public bool AllMet => Len8 && HasLower && HasUpper && HasDigit && HasSpecial;
        }

        public static Result Check(string password)
        {
            if (string.IsNullOrEmpty(password))
                return new Result(false, false, false, false, false);

            bool len8    = password.Length >= 8;
            bool lower   = false;
            bool upper   = false;
            bool digit   = false;
            bool special = false;

            foreach (char c in password)
            {
                if      (char.IsLower(c))         lower   = true;
                else if (char.IsUpper(c))         upper   = true;
                else if (char.IsDigit(c))         digit   = true;
                else if (!char.IsLetterOrDigit(c)) special = true;
            }

            return new Result(len8, lower, upper, digit, special);
        }
    }
}
