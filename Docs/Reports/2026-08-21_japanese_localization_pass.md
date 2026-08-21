# Japanese localization pass — 2026-08-21

Five commits. Started as "the game always boots English" and ended as a sweep of every
screen a player reaches before gameplay.

| Commit | What |
|---|---|
| `a6831529b` | Boot in the device's language on first run |
| `594a4610d` | Practice copy + 35 account labels wired |
| `18bcdffa3` | Account top banner |
| `19fe63512` | Email confirmation screen |
| `cc44aa3da` | Account validation messages |
| `8e0495dfb` | Starter club blurbs + rankings screen |

## The one bug behind most of it

**An authored localization key is not a localized screen.** The `AUTH_LOGIN_*`,
`AUTH_SIGNUP_*`, `AUTH_CREATE_USERNAME_*` and `AUTH_EMAIL_CONF_*` rows all existed in
`LocalizationText.csv`, Japanese included — and were referenced by **nothing**. 40 labels
across four screens carried hardcoded English. SplashScreen *was* wired, which is exactly
why the gate localized and every screen behind it did not.

Presence in the CSV proves nothing. Reference count does:

```bash
for k in $(grep -o "^AUTH_[A-Z0-9_]*" Assets/Localization/LocalizationText.csv); do
  echo "$(grep -rl "$k" Assets/Scripts Assets/Scenes Assets/Prefabs | wc -l)  $k"
done | sort -n
```

The same shape appeared on the mode cards: `ModeCardController` localizes by convention with
a raw-CSV fallback, but only `tournaments` had rows — so every other card fell back to
plausible English and nothing looked broken.

## Startup language

`LocalizationBootstrap` always initialized with its serialized `defaultLanguage`. Now:
**saved player choice → device language → fallback**, in `LanguageSettings`. Two ordering
bugs closed alongside it, because the feature depends on them:

- `LocalizationManager.Initialize` never fired `OnLanguageChanged`, so a label whose
  `OnEnable` ran first was stuck forever — `SetLanguage` early-returns when the language
  already matches.
- `LocalizedText.Refresh` only worked after `Awake`, which silently broke the public `SetKey`.

A saved language choice now also survives a relaunch. It previously only applied when the
settings accordion was opened.

## English was never changed

The authored CSV English had drifted from the shipped copy ("Welcome Back" vs the screen's
"LOGIN WITH EMAIL"; "Continue with Google" vs "Login with Google"; all five password rules;
the entire email-confirmation screen). Since none of those keys were referenced, the **CSV
English was realigned to the screen**, not the reverse — and the wiring pass asserted
`key.english == label.text` on every element *before* adding a component. English A/B
screenshots confirm it after the fact.

## Verification

Play mode, 1170×2532, booting into Japanese unprompted. Errors triggered through real
widgets (LOGIN with empty fields; CREATE with a weak password) — both validate and return
before any network call. Scene edits proven purely additive: 70 new YAML objects, 0 removed,
every `m_text`/font/anchor/size/position value byte-identical to HEAD. Full EditMode suite
1550, 0 failures.

## Still English, deliberately

- **Server messages** (`result.Message`, straight from Supabase). Needs an error-code mapping
  and the real message set.
- **Five dead `AUTH_` rows** (`AUTH_LOGIN_OR`, `AUTH_LOGIN_CREATE`, `AUTH_SIGNUP_OR`,
  `AUTH_SIGNUP_LOGIN`, `AUTH_EMAIL_CONF_OPEN`) describing elements that no longer exist.
  Dead rows, not missing translations.
- **Rankings list rows render "LV 96"** where the top-3 cards show "Lv 190". The string is
  correct; that label carries an `UpperCase` font style. A design call.

## Not yet swept

Gacha, Tournaments, Shop, and the in-game HUD / results screens.

## Screens

`Docs/Reports/Media/localization_ja_2026-08-21/` — 01 Practice description · 02 Login ·
03 Sign Up · 04 Create Username · 05 Login EN control · 06 Email confirmation ·
07 Email confirmation EN control · 08 Login error · 09 Sign Up error · 10 Club info ·
11 Rankings.
