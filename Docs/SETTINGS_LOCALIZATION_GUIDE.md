# Settings Screen Localization Guide

## Overview

This guide explains how to add localization to all text in the Settings Screen using the auto-generation tool.

**Time Required:** ~5 minutes (one-click setup!)  
**Tool:** `Tools → GOLFIN → Localize Settings Screen`  
**Integration:** Uses existing `LocalizedText` component system

### How It Works

The tool integrates with your existing localization system:

1. **Uses `LocalizedText` component** (from `Assets/Localization/LocalizedText.cs`)
   - Automatically adds it to TextMeshPro objects
   - Sets the `key` field for each text

2. **Auto-refreshes on language change**
   - Listens to `LocalizationManager.OnLanguageChanged`
   - Updates text via `LocalizationManager.Get(key)`

3. **Generates CSV for your review**
   - English + Japanese translations
   - Merge into `LocalizationText.csv`

**Result:** One-click localization setup! No manual component adding needed.

---

## Step 1: Generate Localization Keys + Add Components

### In Unity Editor:

1. **Open the tool:**
   - Menu: `Tools → GOLFIN → Localize Settings Screen`

2. **Assign Settings Panel:**
   - Drag the `SettingsPanel` GameObject into the "Settings Panel" field
   - This is usually found under: `Canvas/SettingsCanvas/SettingsPanel`

3. **Enable auto-wiring:**
   - Make sure **"Add LocalizedText Component"** is checked ✅
   - This will automatically add `LocalizedText` to all texts and assign keys

4. **Generate:**
   - Click **"Generate Keys + Localize"** button
   - Tool will:
     - Scan all TextMeshProUGUI components
     - Generate localization keys
     - Add `LocalizedText` component to each text
     - Assign the correct key to each component
     - Create CSV file: `Assets/Localization/SettingsKeys_Generated.csv`

5. **Review output:**
   - Check Console for summary report (shows how many components added)
   - Click **"Open Generated CSV"** to view the file

---

## Step 2: Review Generated CSV

### What It Contains:

```csv
key,English,Japanese
SETTINGS_MENU_USER_PROFILE,User Profile,ユーザープロフィール
SETTINGS_MENU_SOUND_SETTINGS,Sound Settings,サウンド設定
SETTINGS_MENU_LANGUAGE,Language,言語
SETTINGS_SOUND_MUSIC_VOLUME,Music Volume,音楽の音量
SETTINGS_SOUND_SFX_VOLUME,SFX Volume,効果音の音量
SETTINGS_LANG_ENGLISH,English,英語
SETTINGS_LANG_JAPANESE,Japanese,日本語
SETTINGS_USER_USERNAME,Username,ユーザー名
SETTINGS_CLOSE,CLOSE,閉じる
```

### Key Naming Pattern:

- **Menu Items:** `SETTINGS_MENU_*`
- **User Profile:** `SETTINGS_USER_*`
- **Sound Settings:** `SETTINGS_SOUND_*`
- **Language:** `SETTINGS_LANG_*`
- **Generic:** `SETTINGS_*`

### Common Translations Pre-Loaded:

The tool knows translations for:
- All menu items (User Profile, Sound Settings, Language, etc.)
- Common buttons (CLOSE, SAVE, CANCEL, DONE, EDIT)
- Sound settings (Music Volume, SFX Volume)
- Language names (English, Japanese)
- User profile items (Username, Account Linking, etc.)

### Unknown Strings:

For text not in the pre-loaded list, you'll see:
```csv
SETTINGS_CUSTOM_TEXT,My Custom Text,[My Custom Text]
```

The Japanese column shows `[brackets]` → **manual translation needed!**

---

## Step 3: Adjust Translations (If Needed)

### Edit the CSV:

1. Open `SettingsKeys_Generated.csv` in Excel or text editor
2. Find entries with `[brackets]` in Japanese column
3. Replace with proper Japanese translation
4. Save the file

### Example:

**Before:**
```csv
SETTINGS_CUSTOM_BUTTON,Tap Here,[Tap Here]
```

**After:**
```csv
SETTINGS_CUSTOM_BUTTON,Tap Here,ここをタップ
```

---

## Step 4: Merge into Main CSV

### Option A: Manual Copy-Paste

1. Open `LocalizationText.csv` in text editor
2. Copy all rows from `SettingsKeys_Generated.csv` (except header)
3. Paste at the end of `LocalizationText.csv`
4. Save

### Option B: Excel Merge

1. Open both CSVs in Excel
2. Copy data from generated file
3. Paste into main file
4. Save as CSV

### Verify:

- No duplicate keys
- All keys start with `SETTINGS_`
- Japanese translations look correct
- No `[brackets]` remain

---

## Step 5: Test Language Switching (That's It!)

### In Play Mode:

1. Open Settings
2. Click **Language** → Toggle between English/Japanese
3. All text should update to the selected language
4. Check that all translations display correctly

### Common Issues:

**Text doesn't change:**
- Localization component not added
- Wrong key assigned
- Key not in `LocalizationText.csv`

**Japanese shows [brackets]:**
- Translation not done in Step 3
- Merge incomplete in Step 4

**Text is blank:**
- Key typo (case-sensitive!)
- CSV not loaded by game

---

## Generated Key Reference

### Menu Items (Main Rows)

| GameObject | Key | English | Japanese |
|------------|-----|---------|----------|
| UserProfileRow | `SETTINGS_MENU_USER_PROFILE` | User Profile | ユーザープロフィール |
| SoundSettingsRow | `SETTINGS_MENU_SOUND_SETTINGS` | Sound Settings | サウンド設定 |
| LanguageRow | `SETTINGS_MENU_LANGUAGE` | Language | 言語 |
| TermsOfUseRow | `SETTINGS_MENU_TERMS_OF_USE` | Terms of Use | 利用規約 |
| PrivacyPolicyRow | `SETTINGS_MENU_PRIVACY_POLICY` | Privacy Policy | プライバシーポリシー |
| FAQRow | `SETTINGS_MENU_FAQ` | FAQ | よくある質問 |
| AboutRow | `SETTINGS_MENU_ABOUT` | About | アバウト |
| ContactRow | `SETTINGS_MENU_CONTACT` | Contact | お問い合わせ |
| LogOutRow | `SETTINGS_MENU_LOG_OUT` | Log Out | ログアウト |

### Sound Settings Submenu

| Component | Key | English | Japanese |
|-----------|-----|---------|----------|
| Music Volume Label | `SETTINGS_SOUND_MUSIC_VOLUME` | Music Volume | 音楽の音量 |
| SFX Volume Label | `SETTINGS_SOUND_SFX_VOLUME` | SFX Volume | 効果音の音量 |

### Language Submenu

| Component | Key | English | Japanese |
|-----------|-----|---------|----------|
| English Label | `SETTINGS_LANG_ENGLISH` | English | 英語 |
| Japanese Label | `SETTINGS_LANG_JAPANESE` | Japanese | 日本語 |

### User Profile Submenu

| Component | Key | English | Japanese |
|-----------|-----|---------|----------|
| Username Label | `SETTINGS_USER_USERNAME` | Username | ユーザー名 |
| Account Linking | `SETTINGS_USER_ACCOUNT_LINKING` | Account Linking | アカウント連携 |
| Link Google | `SETTINGS_USER_LINK_GOOGLE` | Link Google | Googleと連携 |
| Link Apple | `SETTINGS_USER_LINK_APPLE` | Link Apple | Appleと連携 |
| Link Twitter | `SETTINGS_USER_LINK_TWITTER` | Link Twitter | Twitterと連携 |

### Buttons

| Component | Key | English | Japanese |
|-----------|-----|---------|----------|
| Close Button | `SETTINGS_CLOSE` | CLOSE | 閉じる |
| Save Button | `SETTINGS_SAVE` | SAVE | 保存 |
| Done Button | `SETTINGS_DONE` | DONE | 完了 |
| Edit Button | `SETTINGS_EDIT` | EDIT | 編集 |

---

## Customizing the Tool

### Adding More Pre-Loaded Translations

Edit `LocalizeSettingsScreen.cs` → `commonTranslations` dictionary:

```csharp
{"My New Text", ("My New Text", "私の新しいテキスト")},
```

### Changing Key Format

Edit the `GenerateKey()` method to use different naming patterns.

### Output Path

Change `csvOutputPath` in the tool window to save CSV elsewhere.

---

## Next Steps

After localization is complete:

1. **Phase 3:** Integrate `LanguageSubmenu` with `LocalizationManager`
   - Toggle updates all UI text in real-time
   - Saves language preference to PlayerPrefs
   - Applies on app restart

2. **Test all screens:** Make sure localization works everywhere:
   - Home Screen
   - Loading Screen
   - Settings Screen
   - (Future screens)

3. **Add more languages:** Extend CSV with columns for other languages

---

## Troubleshooting

### "No TextMeshProUGUI components found"

**Cause:** Settings Panel not assigned or hierarchy incorrect  
**Fix:** Make sure you dragged the parent panel containing all text objects

### "Failed to write CSV"

**Cause:** File is open in another program or permissions issue  
**Fix:** Close Excel/text editor and try again

### Generated keys look wrong

**Cause:** Unexpected GameObject names or hierarchy  
**Fix:** Edit the `GenerateKey()` method to match your naming conventions

### Japanese translations are wrong

**Cause:** Auto-translation is basic, not context-aware  
**Fix:** Manually review and correct in Step 3

---

## Summary Checklist

- [ ] Open tool: `Tools → GOLFIN → Localize Settings Screen`
- [ ] Drag Settings Panel, enable "Add LocalizedText Component"
- [ ] Click "Generate Keys + Localize" ✅ **One-click setup!**
- [ ] Review CSV output
- [ ] Adjust translations for unknown strings (if any)
- [ ] Merge into `LocalizationText.csv`
- [ ] Test language switching in Play Mode
- [ ] Commit changes to repository

**Time:** ~5 minutes total!

---

**Tool Location:** `Assets/Scripts/UI/Editor/LocalizeSettingsScreen.cs`  
**Generated CSV:** `Assets/Localization/SettingsKeys_Generated.csv`  
**Main CSV:** `Assets/Localization/LocalizationText.csv`

**Created by:** Kai (Aikenken Bot)  
**Last Updated:** 2026-03-05
