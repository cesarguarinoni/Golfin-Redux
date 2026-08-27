/**
 * Dashboard localisation — EN + JA.
 *
 * Deliberately dependency-free. This is a five-panel internal tool for a team of
 * one or two, and next-intl would add a routing model, a middleware hop and a
 * build step to solve a problem a flat dictionary solves. If the dashboard ever
 * grows a third language or real pluralisation rules, swap this out — the
 * `useT()` call sites will not change.
 *
 * The chosen language lives in a cookie rather than localStorage so the SERVER
 * renders in the right language on the first paint. localStorage would mean
 * rendering English, hydrating, then flipping — a visible flash on every load.
 */

export const LANGS = ["en", "ja"] as const;
export type Lang = (typeof LANGS)[number];

export const LANG_COOKIE = "golfin_admin_lang";
export const DEFAULT_LANG: Lang = "en";

export function isLang(v: unknown): v is Lang {
  return typeof v === "string" && (LANGS as readonly string[]).includes(v);
}

/** Label for each language, written in that language. */
export const LANG_LABEL: Record<Lang, string> = {
  en: "English",
  ja: "日本語",
};
/** Compact label for the switcher chip. */
export const LANG_SHORT: Record<Lang, string> = {
  en: "EN",
  ja: "日本語",
};

type Entry = { en: string; ja: string };

/**
 * Keys are dot-namespaced by surface. Japanese uses 常体 for descriptions and
 * です・ます for anything addressed to the operator as an instruction, which is
 * the register an internal tool wants — informative, not chatty.
 */
export const DICT = {
  // ---- shell -------------------------------------------------------------
  "app.name": { en: "GOLFIN", ja: "GOLFIN" },
  "app.subtitle": { en: "Admin v1", ja: "管理画面 v1" },
  "app.signOut": { en: "Sign out", ja: "サインアウト" },
  "app.language": { en: "Language", ja: "言語" },

  "nav.users": { en: "Users", ja: "ユーザー" },
  "nav.points": { en: "Points", ja: "ポイント" },
  "nav.tournaments": { en: "Tournaments", ja: "トーナメント" },
  "nav.banners": { en: "Banners", ja: "バナー" },
  "nav.telemetry": { en: "Telemetry", ja: "テレメトリ" },
  "nav.audit": { en: "Audit Log", ja: "監査ログ" },
  "nav.clubs": { en: "Clubs", ja: "クラブ" },
  "nav.characters": { en: "Characters", ja: "キャラクター" },
  "nav.items": { en: "Items", ja: "アイテム" },
  "nav.texts": { en: "Texts", ja: "テキスト" },
  "nav.shop": { en: "Shop", ja: "ショップ" },

  "mode.mock": {
    en: "MOCK DATA — running on local fixtures, no Supabase connection",
    ja: "モックデータ — ローカルの固定データで動作中。Supabase には接続していません",
  },
  "mode.production": { en: "PRODUCTION — live PLAYLIFE database", ja: "本番環境 — PLAYLIFE 本番データベース" },

  // ---- generic -----------------------------------------------------------
  "common.cancel": { en: "Cancel", ja: "キャンセル" },
  "common.save": { en: "Save", ja: "保存" },
  "common.close": { en: "Close", ja: "閉じる" },
  "common.delete": { en: "Delete", ja: "削除" },
  "common.loading": { en: "Loading…", ja: "読み込み中…" },
  "common.none": { en: "(none)", ja: "（なし）" },
  "common.free": { en: "free", ja: "無料" },
  "common.of": { en: "of", ja: "/" },
  "common.rows": { en: "rows", ja: "件" },
  "common.prev": { en: "← Prev", ja: "← 前へ" },
  "common.next": { en: "Next →", ja: "次へ →" },
  "common.page": { en: "Page", ja: "ページ" },
  "common.all": { en: "All", ja: "すべて" },
  "common.yes": { en: "Yes", ja: "はい" },
  "common.no": { en: "No", ja: "いいえ" },
  "common.search": { en: "Search…", ja: "検索…" },
  "common.retry": { en: "Retry", ja: "再試行" },
  "common.mock": { en: "MOCK", ja: "モック" },

  // ---- login -------------------------------------------------------------
  "login.title": { en: "GOLFIN Admin", ja: "GOLFIN 管理画面" },
  "login.email": { en: "Email", ja: "メールアドレス" },
  "login.password": { en: "Password", ja: "パスワード" },
  "login.submit": { en: "Sign in", ja: "サインイン" },
  "login.submitting": { en: "Signing in…", ja: "サインイン中…" },
  "login.mockHint": {
    en: "Mock mode — any password works, but the email must be on the allowlist.",
    ja: "モックモード — パスワードは任意ですが、メールアドレスは許可リストに登録されている必要があります。",
  },

  "notAdmin.title": { en: "Not an admin", ja: "管理者ではありません" },
  "notAdmin.body": {
    en: "This account is not on the admin allowlist.",
    ja: "このアカウントは管理者の許可リストに登録されていません。",
  },
  "notAdmin.signOut": { en: "Sign out and try another account", ja: "サインアウトして別のアカウントを試す" },

  // ---- audit -------------------------------------------------------------
  "audit.title": { en: "Audit Log", ja: "監査ログ" },
  "audit.subtitle": { en: "admin_audit_log · read-only viewer", ja: "admin_audit_log · 閲覧専用" },
  "audit.loading": { en: "Loading audit log…", ja: "監査ログを読み込み中…" },
  "audit.loadFailed": { en: "Failed to load audit log", ja: "監査ログの読み込みに失敗しました" },
  "audit.empty": { en: "No audit entries yet.", ja: "監査ログはまだありません。" },
  "audit.emptyBody": {
    en: "Every admin mutation (username edits, RP adjustments, bans, email confirmations, deletions…) writes one row to public.admin_audit_log. This panel fills up as soon as mutations run against the live database.",
    ja: "管理操作（表示名の変更、RP の増減、BAN、メール確認、削除など）はすべて public.admin_audit_log に 1 行ずつ記録される。本番データベースに対して操作を行うと、ここに表示される。",
  },
  "audit.col.when": { en: "When", ja: "日時" },
  "audit.col.admin": { en: "Admin", ja: "管理者" },
  "audit.col.action": { en: "Action", ja: "操作" },
  "audit.col.target": { en: "Target user", ja: "対象ユーザー" },
  "audit.col.table": { en: "Table", ja: "テーブル" },
  "audit.col.before": { en: "Before", ja: "変更前" },
  "audit.col.after": { en: "After", ja: "変更後" },

  // ---- points ------------------------------------------------------------
  "points.title": { en: "Points", ja: "ポイント" },
  "points.subtitle": { en: "global points_transactions · read-only", ja: "points_transactions 全件 · 閲覧専用" },
  "points.loading": { en: "Loading ledger…", ja: "取引履歴を読み込み中…" },
  "points.loadFailed": { en: "Failed to load ledger", ja: "取引履歴の読み込みに失敗しました" },
  "points.currency.all": { en: "all", ja: "すべて" },
  "points.currency.activity": { en: "activity", ja: "アクティビティ" },
  "points.currency.gift": { en: "gift", ja: "ギフト" },
  "points.allTypes": { en: "All types", ja: "すべての種別" },
  "points.filterEmail": { en: "Filter by user email…", ja: "メールアドレスで絞り込み…" },
  "points.from": { en: "From", ja: "開始" },
  "points.to": { en: "To", ja: "終了" },
  "points.col.when": { en: "When", ja: "日時" },
  "points.col.user": { en: "User", ja: "ユーザー" },
  "points.col.type": { en: "Type", ja: "種別" },
  "points.col.amount": { en: "Amount", ja: "増減" },
  "points.col.description": { en: "Description", ja: "説明" },
  "points.col.key": { en: "Idempotency key", ja: "冪等キー" },
  "points.none": { en: "No transactions match the current filters.", ja: "条件に一致する取引はありません。" },

  // ---- users -------------------------------------------------------------
  "users.title": { en: "Users", ja: "ユーザー" },
  "users.subtitle": { en: "read-only · RP = total_points", ja: "閲覧専用 · RP = total_points" },
  "users.loading": { en: "Loading users…", ja: "ユーザーを読み込み中…" },
  "users.loadFailed": { en: "Failed to load users", ja: "ユーザーの読み込みに失敗しました" },
  "users.stat.total": { en: "Total users", ja: "ユーザー総数" },
  "users.stat.new7": { en: "New (last 7 days)", ja: "新規（過去 7 日）" },
  "users.stat.confirmed": { en: "Confirmed", ja: "確認済み" },
  "users.stat.providers": { en: "Providers", ja: "認証方法" },
  "users.search": { en: "Search email or name…", ja: "メールアドレス・名前で検索…" },
  "users.unconfirmedOnly": { en: "Unconfirmed only", ja: "未確認のみ" },
  "users.bannedOnly": { en: "Banned only", ja: "BAN のみ" },
  "users.countSuffix": { en: "users", ja: "人" },
  "users.col.email": { en: "Email", ja: "メールアドレス" },
  "users.col.username": { en: "Username", ja: "表示名" },
  "users.col.provider": { en: "Provider", ja: "認証方法" },
  "users.col.confirmed": { en: "Confirmed", ja: "確認" },
  "users.col.created": { en: "Created", ja: "登録日" },
  "users.col.lastSignIn": { en: "Last sign-in", ja: "最終サインイン" },
  "users.col.rp": { en: "RP", ja: "RP" },
  "users.banned": { en: "BANNED", ja: "BAN 中" },
  "users.none": { en: "No users match the current filters.", ja: "条件に一致するユーザーはいません。" },
  "users.catalog.title": { en: "Earn catalog", ja: "獲得カタログ" },
  "users.catalog.action": { en: "action", ja: "アクション" },
  "users.catalog.pts": { en: "pts", ja: "ポイント" },
  "users.catalog.maxEvent": { en: "max / event", ja: "1 回上限" },
  "users.catalog.dailyCap": { en: "daily cap", ja: "1 日上限" },
  "users.catalog.oncePerUser": { en: "once per user", ja: "1 人 1 回" },

  // ---- tournaments: list -------------------------------------------------
  "tourn.title": { en: "Tournaments", ja: "トーナメント" },
  "tourn.loading": { en: "Loading tournaments…", ja: "トーナメントを読み込み中…" },
  "tourn.loadFailed": { en: "Failed to load tournaments", ja: "トーナメントの読み込みに失敗しました" },
  "tourn.count.inactive": { en: "inactive", ja: "無効" },
  "tourn.count.open": { en: "open", ja: "開催中" },
  "tourn.count.upcoming": { en: "upcoming", ja: "開催予定" },
  "tourn.count.ended": { en: "ended", ja: "終了" },
  "tourn.live.headline": { en: "Edits here reach players on their next launch.", ja: "ここでの変更は、プレイヤーの次回起動時に反映される。" },
  "tourn.live.body": {
    en: "The game fetches this schedule at boot and falls back to the shipped tournaments.csv only when it cannot reach the server. Re-export at each release so that offline fallback is not a schedule from three builds ago.",
    ja: "ゲームは起動時にこのスケジュールを取得し、サーバーに接続できない場合のみ同梱の tournaments.csv を使用する。オフライン時のフォールバックが古いままにならないよう、リリースごとに再エクスポートすること。",
  },
  "tourn.export.tournaments": { en: "Export tournaments.csv", ja: "tournaments.csv を書き出す" },
  "tourn.export.prizes": { en: "Export tournament_prizes.csv", ja: "tournament_prizes.csv を書き出す" },
  "tourn.filter.activeAll": { en: "Active + inactive", ja: "有効 + 無効" },
  "tourn.filter.activeOnly": { en: "Active only", ja: "有効のみ" },
  "tourn.filter.inactiveOnly": { en: "Inactive only", ja: "無効のみ" },
  "tourn.filter.allStates": { en: "All states", ja: "すべての状態" },
  "tourn.filter.search": { en: "Filter by slug, title, course…", ja: "スラッグ・タイトル・コースで絞り込み…" },
  "tourn.new": { en: "+ New tournament", ja: "＋ 新規トーナメント" },
  "tourn.col.tournament": { en: "Tournament", ja: "トーナメント" },
  "tourn.col.state": { en: "State", ja: "状態" },
  "tourn.col.course": { en: "Course", ja: "コース" },
  "tourn.col.holes": { en: "Holes", ja: "ホール" },
  "tourn.col.fee": { en: "Fee", ja: "参加費" },
  "tourn.col.prizes": { en: "Prizes", ja: "賞金" },
  "tourn.col.window": { en: "Window (UTC)", ja: "期間（UTC）" },
  "tourn.col.entries": { en: "Entries", ja: "エントリー" },
  "tourn.col.art": { en: "Art", ja: "画像" },
  "tourn.inactiveBadge": { en: "inactive", ja: "無効" },
  "tourn.noBands": { en: "no bands", ja: "賞金設定なし" },
  "tourn.topPlaces": { en: "top ·", ja: "最高 ·" },
  "tourn.places": { en: "places", ja: "位まで" },
  "tourn.human": { en: "human", ja: "人間" },
  "tourn.none": { en: "No tournaments match the current filters.", ja: "条件に一致するトーナメントはありません。" },

  // ---- tournaments: badges ----------------------------------------------
  "tstate.Upcoming": { en: "Upcoming", ja: "開催予定" },
  "tstate.Open": { en: "Open", ja: "開催中" },
  "tstate.Ending": { en: "Ending", ja: "まもなく終了" },
  "tstate.Ended": { en: "Ended", ja: "終了" },
  "tstate.Unknown": { en: "Unknown", ja: "不明" },
  "tstate.hint": {
    en: "Derived from start_at/end_at — the same rule as LocalTournamentBackend.DeriveState",
    ja: "start_at / end_at から算出（LocalTournamentBackend.DeriveState と同じ規則）",
  },
  "tkind.golfin": { en: "golfin", ja: "ゲーム内" },
  "tkind.gps": { en: "gps", ja: "GPS" },
  "tkind.golfin.hint": { en: "In-game tournament", ja: "ゲーム内トーナメント" },
  "tkind.gps.hint": { en: "Real-world PLAYLIFE event", ja: "実世界の PLAYLIFE イベント" },
  "tart.remote": { en: "remote", ja: "リモート" },
  "tart.bundled": { en: "bundled", ja: "同梱" },
  "tart.placeholder": { en: "placeholder", ja: "代替画像" },
  "tart.hint": { en: "Which art layer the client will resolve to", ja: "クライアントが使用する画像の階層" },
  "tourn.inactiveHint": { en: "Hidden from the game — the schedule endpoint does not return it", ja: "ゲームには表示されない — スケジュール API が返さない" },

  // ---- users: drawer + modals -------------------------------------------
  "udrawer.close": { en: "Close", ja: "閉じる" },
  "udrawer.editName": { en: "Edit display name", ja: "表示名を編集" },
  "udrawer.adminActions": { en: "Admin actions", ja: "管理操作" },
  "udrawer.working": { en: "Working…", ja: "処理中…" },

  "uact.resend_confirmation.title": { en: "Resend confirmation email", ja: "確認メールを再送する" },
  "uact.resend_confirmation.body": { en: "Resend the signup confirmation email to {email}?", ja: "{email} に登録確認メールを再送しますか？" },
  "uact.resend_confirmation.confirm": { en: "Resend email", ja: "再送する" },
  "uact.send_password_reset.title": { en: "Send password reset", ja: "パスワード再設定メールを送る" },
  "uact.send_password_reset.body": { en: "Send a password-reset email to {email}?", ja: "{email} にパスワード再設定メールを送信しますか？" },
  "uact.send_password_reset.confirm": { en: "Send reset email", ja: "送信する" },
  "uact.confirm_email.title": { en: "Manually confirm email", ja: "メールを手動で確認済みにする" },
  "uact.confirm_email.body": { en: "Mark {email} as confirmed without the user clicking the confirmation link?", ja: "{email} を、本人が確認リンクを開かないまま確認済みにしますか？" },
  "uact.confirm_email.confirm": { en: "Confirm email", ja: "確認済みにする" },
  "uact.ban.title": { en: "Ban user", ja: "ユーザーを BAN する" },
  "uact.ban.body": { en: "Ban {email}? The user will be unable to sign in until unbanned.", ja: "{email} を BAN しますか？ 解除するまでサインインできなくなります。" },
  "uact.ban.confirm": { en: "Ban user", ja: "BAN する" },
  "uact.unban.title": { en: "Unban user", ja: "BAN を解除する" },
  "uact.unban.body": { en: "Lift the ban on {email}? They will be able to sign in again.", ja: "{email} の BAN を解除しますか？ 再びサインインできるようになります。" },
  "uact.unban.confirm": { en: "Unban user", ja: "解除する" },

  "udel.title": { en: "Delete user", ja: "ユーザーを削除" },
  "udel.permanent": { en: "Permanent — cannot be undone", ja: "完全削除 — 取り消せません" },
  "udel.body": { en: "Deleting {email} removes the auth user and, via FK cascade, everything hanging off it:", ja: "{email} を削除すると認証ユーザーが消え、外部キーのカスケードで紐づくデータもすべて削除される:" },
  "udel.item.profile": { en: "row — RP balance ({rp} RP), avatar, social counters", ja: "行 — RP 残高（{rp} RP）、アバター、ソーシャル情報" },
  "udel.item.points": { en: "— the entire points ledger history", ja: "— ポイント取引履歴のすべて" },
  "udel.item.activities": { en: "— GPS check-ins", ja: "— GPS チェックイン" },
  "udel.typeEmail": { en: "Type the user's email to confirm", ja: "確認のためメールアドレスを入力してください" },
  "udel.confirm": { en: "Delete user permanently", ja: "完全に削除する" },

  "urp.title": { en: "Adjust RP", ja: "RP を調整" },
  "urp.amount": { en: "Amount (positive grants, negative deducts)", ja: "増減量（正の値で付与、負の値で減算）" },
  "urp.amountPlaceholder": { en: "e.g. 100 or -50", ja: "例: 100 または -50" },
  "urp.reason": { en: "Reason (required, max 200 chars)", ja: "理由（必須・200 文字以内）" },
  "urp.reasonPlaceholder": { en: "e.g. welcome grant for closed beta tester", ja: "例: クローズドベータ参加者への付与" },
  "urp.grant": { en: "Grant RP", ja: "RP を付与" },
  "urp.deduct": { en: "Deduct RP", ja: "RP を減算" },

  "udrawer.rp": { en: "Reward Points (total_points)", ja: "リワードポイント（total_points）" },
  "udrawer.noTx": { en: "No points transactions.", ja: "ポイント取引はありません。" },
  "udrawer.noActivities": { en: "No recorded activities.", ja: "記録されたアクティビティはありません。" },
  "udrawer.audited": { en: "All mutations are audited — admin_audit_log", ja: "すべての操作は admin_audit_log に記録される" },
  "udrawer.alreadyConfirmed": { en: "Email already confirmed", ja: "メールは確認済みです" },
  "udrawer.loadFailed": { en: "Failed to load detail", ja: "詳細の読み込みに失敗しました" },
  "udrawer.editNameHint": { en: "Edit display name (writes profiles.display_name + auth user_metadata)", ja: "表示名を編集（profiles.display_name と auth user_metadata を更新）" },
  "udrawer.banned": { en: "BANNED", ja: "BAN 中" },
  "common.done": { en: "Done.", ja: "完了しました。" },
  "common.requestFailed": { en: "Request failed", ja: "リクエストに失敗しました" },
  "te.slugFirst": {
    en: "Give the tournament a slug first — the file is named after it.",
    ja: "先にトーナメントの slug を設定してください。ファイル名に使われます。",
  },
  "urp.ledgerHint": {
    en: "Ledger description will read",
    ja: "取引履歴には次のように記録される:",
  },
  "urp.ledgerHint2": {
    en: "Deductions debit activity points first, then gift points.",
    ja: "減算はアクティビティポイントから先に引き、次にギフトポイントから引く。",
  },
  "udrawer.action.rp": { en: "Adjust RP", ja: "RP を調整" },
  "udrawer.action.resendConfirmation": { en: "Resend confirmation", ja: "確認メールを再送" },
  "udrawer.action.sendPasswordReset": { en: "Send password reset", ja: "パスワード再設定を送信" },
  "udrawer.action.confirmEmail": { en: "Confirm email", ja: "メールを確認済みにする" },
  "udrawer.action.unban": { en: "Unban user", ja: "BAN を解除" },
  "udrawer.action.ban": { en: "Ban user", ja: "ユーザーを BAN" },
  "udrawer.action.delete": { en: "Delete user", ja: "ユーザーを削除" },
  "udrawer.action.grant": { en: "Grant items", ja: "アイテムを付与" },
  "udrawer.field.avatarLevel": { en: "Avatar level", ja: "アバターレベル" },
  "udrawer.field.avatarXp": { en: "Avatar XP", ja: "アバター XP" },
  "udrawer.field.trustLevel": { en: "Trust level", ja: "信頼レベル" },
  "udrawer.field.followers": { en: "Followers", ja: "フォロワー" },
  "udrawer.field.following": { en: "Following", ja: "フォロー中" },
  "udrawer.field.badges": { en: "Badges", ja: "バッジ" },
  "udrawer.field.providers": { en: "Providers", ja: "認証方法" },
  "udrawer.field.emailConfirmed": { en: "Email confirmed", ja: "メール確認" },
  "udrawer.field.created": { en: "Created", ja: "登録日" },
  "udrawer.field.lastSignIn": { en: "Last sign-in", ja: "最終サインイン" },
  "udrawer.field.bannedUntil": { en: "Banned until", ja: "BAN 期限" },
  "udrawer.unconfirmed": { en: "unconfirmed", ja: "未確認" },
  "udrawer.tab.transactions": { en: "Points ledger", ja: "ポイント履歴" },
  "udrawer.tab.activities": { en: "Activities", ja: "アクティビティ" },
  "udrawer.tab.inventory": { en: "Inventory", ja: "インベントリ" },

  "loginPage.title": { en: "GOLFIN Admin", ja: "GOLFIN 管理画面" },
  "loginPage.subtitle": { en: "Internal dashboard — admins only", ja: "社内管理画面 — 管理者専用" },

  "provider.email": { en: "Email / password", ja: "メール / パスワード" },
  "provider.google": { en: "Google", ja: "Google" },
  "provider.apple": { en: "Apple", ja: "Apple" },

  // ---- banners -----------------------------------------------------------
  "ban.title": { en: "Banners", ja: "バナー" },
  "ban.loading": { en: "Loading banners…", ja: "バナーを読み込み中…" },
  "ban.loadFailed": { en: "Failed to load banners", ja: "バナーの読み込みに失敗しました" },
  "ban.switchFailed": { en: "Failed to switch the banner", ja: "バナーの切り替えに失敗しました" },
  "ban.liveNote": {
    en: "LIVE is the only state a player can see — every other state means the slot shows its bundled sprite.",
    ja: "プレイヤーに表示されるのは LIVE のみ。それ以外の状態では、同梱のスプライトが表示される。",
  },
  "ban.onePerPlacement": {
    en: "At most one banner per placement is served, and the bundled sprite is always the fallback.",
    ja: "1 つの掲載枠につき配信されるバナーは最大 1 件。フォールバックは常に同梱スプライト。",
  },
  "ban.activate": { en: "Activate", ja: "有効にする" },
  "ban.deactivate": { en: "Deactivate", ja: "無効にする" },
  "ban.preview": { en: "Preview", ja: "プレビュー" },
  "ban.label": { en: "Label (admin-only)", ja: "ラベル（管理用）" },
  "ban.col.state": { en: "State", ja: "状態" },
  "ban.col.window": { en: "Window (UTC)", ja: "期間（UTC）" },
  "ban.noTournament": { en: "Not assigned to any tournament", ja: "トーナメント未割り当て" },
  "ban.new": { en: "New banner", ja: "新規バナー" },
  "ban.placement": { en: "Placement", ja: "掲載枠" },
  "ban.type": { en: "Type", ja: "種別" },
  "ban.linkUrl": { en: "Link URL (optional)", ja: "リンク URL（任意）" },
  "ban.start": { en: "Start (UTC, optional)", ja: "開始（UTC・任意）" },
  "ban.end": { en: "End (UTC, optional)", ja: "終了（UTC・任意）" },
  "ban.sortOrder": { en: "Sort order", ja: "表示順" },
  "ban.sortHint": { en: "Highest wins within the placement, then newest. −999…999.", ja: "同じ掲載枠では値が大きいものが優先、次に新しいもの。−999〜999。" },
  "ban.artEn": { en: "English artwork", ja: "英語版の画像" },
  "ban.artJa": { en: "Japanese artwork", ja: "日本語版の画像" },
  "ban.exclusiveHint": { en: "Exclusive. Sent to the client so a banner cached on-device expires even offline.", ja: "排他的。端末にキャッシュされたバナーがオフラインでも失効するよう、クライアントに送られる。" },
  "ban.isLive": { en: "This banner is LIVE.", ja: "このバナーは配信中です。" },
  "ban.assignedNotScheduled": { en: "This banner is assigned, not scheduled.", ja: "このバナーは割り当て済みで、日時指定はありません。" },
  "ban.activeOn": { en: "Active — the game receives this", ja: "有効 — ゲームに配信される" },
  "ban.draft": { en: "Draft — hidden from the game", ja: "下書き — ゲームには表示されない" },
  "ban.deleteBanner": { en: "Delete banner…", ja: "バナーを削除…" },
  "ban.create": { en: "Create", ja: "作成" },
  "ban.remove": { en: "Remove", ja: "削除" },
  "ban.saving": { en: "Saving…", ja: "保存中…" },
  "ban.saved": { en: "Saved.", ja: "保存しました。" },
  "ban.deleted": { en: "Deleted.", ja: "削除しました。" },
  "ban.saveFailed": { en: "Save failed", ja: "保存に失敗しました" },
  "ban.deleteFailed": { en: "Delete failed", ja: "削除に失敗しました" },

  // ---- notices -----------------------------------------------------------
  "nav.notices": { en: "Notices", ja: "お知らせ" },
  "notice.title": { en: "Home notices", ja: "ホームのお知らせ" },
  "notice.loading": { en: "Loading notices…", ja: "お知らせを読み込み中…" },
  "notice.loadFailed": { en: "Failed to load notices", ja: "お知らせの読み込みに失敗しました" },
  "notice.switchFailed": { en: "Failed to switch the notice", ja: "お知らせの切り替えに失敗しました" },
  "notice.count": { en: "{live} live · {total} total", ja: "掲載中 {live} 件 · 全 {total} 件" },
  "notice.howItWorksLead": {
    en: "This is the panel under the top bar on Home.",
    ja: "ホーム画面の上部バー下にあるパネルです。",
  },
  "notice.howItWorks": {
    en: "Every live notice becomes one page, ordered by sort order then newest, up to {max}; the dots under the panel page through them. With nothing live the panel is hidden — it is an announcement surface, not a permanent fixture. Players pick this up on their next launch, or when they next open Home (the client refetches on screen entry, at most once a minute).",
    ja: "掲載中のお知らせがそれぞれ 1 ページになり、表示順、次に新しい順で最大 {max} 件まで並ぶ。パネル下のドットでページを切り替えられる。掲載中のものが 1 件もない場合、パネル自体が非表示になる（常設ではなく告知用の枠）。プレイヤーには次回の起動時、またはホーム画面を次に開いたときに反映される（クライアントは画面表示時に、最大 1 分に 1 回再取得する）。",
  },
  "notice.noneLive": {
    en: "Nothing is live — the notice panel is hidden in game right now.",
    ja: "掲載中のお知らせはありません。現在ゲーム側ではパネルが非表示になっています。",
  },
  "notice.newNotice": { en: "+ New notice", ja: "＋ 新規お知らせ" },
  "notice.empty": { en: "No notices yet.", ja: "お知らせはまだありません。" },
  "notice.col.page": { en: "Page", ja: "ページ" },
  "notice.col.label": { en: "Label (admin-only)", ja: "ラベル（管理用）" },
  "notice.col.state": { en: "State", ja: "状態" },
  "notice.col.text": { en: "Text (EN)", ja: "本文（英語）" },
  "notice.col.langs": { en: "Languages", ja: "言語" },
  "notice.col.window": { en: "Window (UTC)", ja: "期間（UTC）" },
  "notice.col.sort": { en: "Sort", ja: "表示順" },
  "notice.always": { en: "always", ja: "制限なし" },
  "notice.noExpiry": { en: "no expiry", ja: "無期限" },
  "notice.noWindow": { en: "no window", ja: "期間指定なし" },
  "notice.activate": { en: "Activate", ja: "掲載する" },
  "notice.deactivate": { en: "Deactivate", ja: "取り下げる" },
  "notice.confirmDeactivate": {
    en: "\"{label}\" is LIVE — players are reading it right now.\nRe-type the label to take it down:",
    ja: "「{label}」は掲載中です — 現在プレイヤーに表示されています。\n取り下げるにはラベルを再入力してください:",
  },
  "notice.new": { en: "New notice", ja: "新規お知らせ" },
  "notice.label": { en: "Label (admin-only)", ja: "ラベル（管理用）" },
  "notice.labelHint": {
    en: "So you can find the row. Never sent to the client and never shown to a player.",
    ja: "行を探すための管理用の名前。クライアントには送られず、プレイヤーにも表示されない。",
  },
  "notice.titleEn": { en: "Title (English)", ja: "タイトル（英語）" },
  "notice.titleJa": { en: "Title (Japanese)", ja: "タイトル（日本語）" },
  "notice.bodyEn": { en: "Body (English)", ja: "本文（英語）" },
  "notice.bodyJa": { en: "Body (Japanese)", ja: "本文（日本語）" },
  "notice.textHint": {
    en: "Line breaks are kept exactly as typed — the panel is narrow, so break the lines yourself rather than trusting it to wrap. Japanese is optional: leave it empty and Japanese players read the English.",
    ja: "改行は入力したとおりに反映される。パネルの幅は狭いため、折り返しに任せず自分で改行を入れること。日本語は任意で、空のままなら日本語のプレイヤーには英語が表示される。",
  },
  "notice.start": { en: "Start (UTC, optional)", ja: "開始（UTC・任意）" },
  "notice.end": { en: "End (UTC, optional)", ja: "終了（UTC・任意）" },
  "notice.endHint": {
    en: "Sent to the client, so a notice cached on-device disappears on time even offline. Worth setting on a maintenance notice: it must not outlive the maintenance.",
    ja: "クライアントに送られるため、端末にキャッシュされたお知らせもオフラインで期限どおりに消える。メンテナンス告知には設定しておくこと（メンテナンス終了後も残ってはいけない）。",
  },
  "notice.sortOrder": { en: "Sort order", ja: "表示順" },
  "notice.sortHint": {
    en: "Page order: highest first, then newest. −999…999.",
    ja: "ページ順。値が大きいものが先、次に新しいもの。−999〜999。",
  },
  "notice.preview": { en: "What a player sees", ja: "プレイヤーに表示される内容" },
  "notice.fallbackHint": {
    en: "Japanese is blank, so Japanese players see the English. That is the fallback, not a hidden notice.",
    ja: "日本語が空のため、日本語のプレイヤーには英語が表示される（非表示になるのではなくフォールバックする）。",
  },
  "notice.activeOn": { en: "Live — the game shows this", ja: "掲載中 — ゲームに表示される" },
  "notice.draft": { en: "Draft — hidden from the game", ja: "下書き — ゲームには表示されない" },
  "notice.activeHint": {
    en: "Separate from the schedule window below. Live plus inside the window is the only combination a player sees.",
    ja: "下の掲載期間とは別の設定。掲載中かつ期間内のときだけプレイヤーに表示される。",
  },
  "notice.isLive": { en: "This notice is LIVE.", ja: "このお知らせは掲載中です。" },
  "notice.liveConfirmHint": {
    en: "Taking it down is instant and player-facing — it disappears on the next fetch. Re-type the label to confirm.",
    ja: "取り下げると即座にプレイヤー側へ反映され、次回の取得で消える。確認のためラベルを再入力してください。",
  },
  "notice.deleteNotice": { en: "Delete notice…", ja: "お知らせを削除…" },
  "notice.deleteConfirmType": { en: "Type", ja: "入力:" },
  "notice.deleteConfirmHint": {
    en: "to delete this notice.",
    ja: "と入力するとこのお知らせを削除する。",
  },
  "notice.create": { en: "Create", ja: "作成" },
  "notice.saving": { en: "Saving…", ja: "保存中…" },
  "notice.saved": { en: "Saved.", ja: "保存しました。" },
  "notice.deleted": { en: "Deleted.", ja: "削除しました。" },
  "notice.saveFailed": { en: "Save failed", ja: "保存に失敗しました" },
  "notice.deleteFailed": { en: "Delete failed", ja: "削除に失敗しました" },
  "te.bundledArt": { en: "bundled: {file}", ja: "同梱: {file}" },
  "te.placeholderArt": { en: "placeholder", ja: "プレースホルダー" },
  "te.addBand": { en: "+ Add band", ja: "＋ 順位帯を追加" },
  "ban.emptyPlacementNoSprite": {
    en: "Nothing scheduled — no bundled fallback, so the strip is simply absent.",
    ja: "予定なし — 同梱のフォールバック画像はないため、この帯は表示されない。",
  },
  "art.unsupportedType": {
    en: "Unsupported type \"{type}\". Use JPG, PNG or WebP.",
    ja: "対応していない形式です（{type}）。JPG・PNG・WebP を使ってください。",
  },
  "art.tooBig": {
    en: "{kb} KB exceeds the {cap} KB cap. Every mobile player downloads this once.",
    ja: "{kb} KB は上限の {cap} KB を超えています。すべてのプレイヤーが 1 回ダウンロードする画像です。",
  },
  "art.aspectWarn": {
    en: "{w}×{h} (ratio {ratio}) — the slot is {sw}×{sh} ({saspect}). It will be cropped or letterboxed.",
    ja: "{w}×{h}（比率 {ratio}）— 枠は {sw}×{sh}（{saspect}）。切り抜きまたは余白付きで表示される。",
  },
  "art.uploadFailed": { en: "Upload failed", ja: "アップロードに失敗しました" },
  "art.uploadedSaveHint": { en: "Save to publish it.", ja: "公開するには保存してください。" },
  "art.uploading": { en: "Uploading…", ja: "アップロード中…" },
  "art.remove": { en: "Remove", ja: "画像を外す" },
  "art.removeBand": { en: "Remove band {n}", ja: "{n} 番目の順位帯を削除" },
  "ban.artNone": {
    en: "none — falls back to the other locale, then the bundled sprite",
    ja: "なし — 他言語の画像、次に同梱スプライトにフォールバックする",
  },
  "ban.placement.home_promo": { en: "Home — promo strip", ja: "ホーム — プロモ帯" },
  "ban.placement.rankings": { en: "Rankings — banner", ja: "ランキング — バナー" },
  "ban.placement.tournament_modal": { en: "Tournament — sign-up modal strip", ja: "トーナメント — 参加モーダルの帯" },
  "te.nameKeyHint": {
    en: "Optional, and it overrides the title whenever it resolves in the shipped build. Keys ship inside the app, so a key invented here resolves nowhere and the title is used instead. Leave it empty for anything you name yourself.",
    ja: "任意。製品ビルドで解決できる場合はタイトルより優先される。キーはアプリに同梱されるため、ここで新しく作ったキーはどこにも解決されず、代わりにタイトルが使われる。自分で名前を付けるものは空のままにする。",
  },
  "te.descKeyHint": {
    en: "Optional, and it overrides both descriptions whenever it resolves in the shipped build — in both languages. Keys ship inside the app, so a key invented here resolves nowhere and the text above is used instead.",
    ja: "任意。製品ビルドで解決できる場合は、両言語とも上の説明文より優先される。キーはアプリに同梱されるため、ここで新しく作ったキーはどこにも解決されず、代わりに上のテキストが使われる。",
  },
  "te.deleteCascade": {
    en: "cascades {entries} entries ({human} human) and {bands} prize bands. Type the slug to confirm.",
    ja: "を削除すると、エントリー {entries} 件（うち人間 {human} 件）と賞金設定 {bands} 件も一緒に削除される。確認のため slug を入力してください。",
  },
  "te.duplicateHint": {
    en: "— same course, holes, fee and prize ladder, dates shifted forward one cycle, artwork not copied. New slug:",
    ja: "をコピー — コース、ホール、参加費、賞金設定は同じ。日付は 1 サイクル分だけ後ろにずれ、画像はコピーされない。新しい slug:",
  },
  "te.copy": { en: "Copy", ja: "コピー元:" },
  "te.poolSummary": {
    en: "top {top} RP · {places} paid places · {total} RP total if every place fills",
    ja: "1 位 {top} RP · 入賞 {places} 枠 · 全枠が埋まった場合の合計 {total} RP",
  },
  "te.band.from": { en: "From", ja: "開始順位" },
  "te.band.to": { en: "To", ja: "終了順位" },
  "te.artHint": {
    en: "JPG / PNG / WebP · max {maxKb} KB · {w}×{h} card. Uploaded to the project's tournament-art bucket under an immutable content-hashed name, so the URL is its own cache key. The client accepts only URLs on that host.",
    ja: "JPG / PNG / WebP · 最大 {maxKb} KB · カードサイズ {w}×{h}。アップロード先はプロジェクトの tournament-art バケットで、内容ハッシュに基づく不変の名前が付くため、URL 自体がキャッシュキーになる。クライアントはこのホストの URL のみ受け付ける。",
  },
  "te.bannerHint1": {
    en: "The 970×252 cross-promotion strip at the top of this tournament's sign-up modal. The artwork lives in the",
    ja: "このトーナメントの参加モーダル上部に表示される 970×252 のクロスプロモーション帯。画像の管理は",
  },
  "te.bannerHint2": {
    en: "— upload it once there and assign it to as many tournaments as you like. Switching a banner off in that panel removes it from every tournament at once.",
    ja: "で行う。一度アップロードすれば、いくつでもトーナメントに割り当てられる。そのパネルでバナーを無効にすると、すべてのトーナメントから一度に外れる。",
  },
  "te.noActiveTail": { en: "banners yet. Create one in the", ja: "のバナーはまだない。作成は", },
  "te.noActiveTail2": { en: "and it will appear here.", ja: "から。作成するとここに表示される。" },
  "te.noArtUploaded": { en: "no art uploaded", ja: "画像未アップロード" },
  "te.tapsOpen": { en: "taps open", ja: "タップで開く:" },
  "ban.liveConfirmHint": {
    en: "Switching it off is instant and player-facing — the slot snaps back to the bundled sprite on the next fetch. Re-type the label to confirm.",
    ja: "無効にすると即座にプレイヤー側へ反映され、次回の取得で枠は同梱スプライトに戻る。確認のためラベルを再入力してください。",
  },
  "ban.activeHint": {
    en: "Separate from the schedule window below. Active plus inside the window is the only combination a player sees; everything else leaves the bundled sprite on screen.",
    ja: "下の配信期間とは別の設定。有効かつ期間内のときだけプレイヤーに表示され、それ以外は同梱スプライトのままになる。",
  },
  "ban.labelHint": {
    en: "So you can find the row. Never sent to the client and never shown to a player — all player-visible copy is baked into the artwork.",
    ja: "行を探すための管理用の名前。クライアントには送られず、プレイヤーにも表示されない。プレイヤーが見る文言はすべて画像に焼き込む。",
  },
  "ban.linkHint": {
    en: "Opens in the device browser. Only {hosts} — the client ships its own copy of that list, so a new host needs a client release, not a dashboard change. Leave empty for an informational banner: the slot is then not tappable.",
    ja: "端末のブラウザで開く。許可されるのは {hosts} のみ。クライアント側も同じ一覧を持つため、ホストの追加にはダッシュボードではなくクライアントのリリースが必要。告知だけのバナーは空のままにすると、タップできなくなる。",
  },
  "ban.assignedHint": {
    en: "Schedule and sort order do not apply — each tournament's own window decides when its strip is on screen, and a tournament shows exactly the one banner it is assigned in the Tournaments panel. Active is still the kill switch: switching this off removes it from every tournament using it, at once.",
    ja: "配信期間と表示順は適用されない。表示のタイミングは各トーナメント自身の期間で決まり、トーナメント画面にはトーナメントパネルで割り当てた 1 枚だけが表示される。「有効」は引き続き緊急停止スイッチで、オフにすると使用中のすべてのトーナメントから一度に外れる。",
  },
  "ban.assignedNow": { en: "Right now that is {count} tournament(s):", ja: "現在の対象は {count} 件:" },
  "ban.artHint": {
    en: "One image per locale — there are no text fields, so all copy is baked into the artwork. A JP player gets the JA image and falls back to EN when it is absent (and vice versa); with neither, the slot keeps its bundled {sprite}. JPG / PNG / WebP · max {maxKb} KB · target {w}×{h}. Uploads go to the game-banners bucket under an immutable content-hashed name, so the URL is its own cache key.",
    ja: "言語ごとに 1 枚。テキスト項目はないため、文言はすべて画像に焼き込む。日本語のプレイヤーには JA 画像が表示され、なければ EN にフォールバックする（逆も同様）。どちらもない場合は同梱の {sprite} のまま。JPG / PNG / WebP · 最大 {maxKb} KB · 推奨 {w}×{h}。アップロード先は game-banners バケットで、内容ハッシュに基づく不変の名前が付くため、URL 自体がキャッシュキーになる。",
  },
  "ban.deleteAssignedWarn": {
    en: "Assigned to {count} tournament(s):",
    ja: "{count} 件のトーナメントに割り当て済み:",
  },
  "ban.deleteAssignedBody": {
    en: "Deleting clears the assignment on each of them — the tournaments stay live and their sign-up modals simply render without a strip.",
    ja: "削除するとそれぞれの割り当てが解除される。トーナメント自体は残り、参加モーダルはバナーなしで表示される。",
  },
  "ban.deleteConfirmHint": {
    en: "to delete this banner. The uploaded artwork stays in Storage.",
    ja: "と入力するとこのバナーを削除する。アップロード済みの画像は Storage に残る。",
  },
  "ban.deleteConfirmType": { en: "Type", ja: "入力:" },
  "ban.count": { en: "{live} live · {total} total", ja: "配信中 {live} 件 · 全 {total} 件" },
  "ban.howItWorks": {
    en: "The game picks the highest sort order that is active and inside its window, then the newest. A placement with nothing live shows exactly what it shows today — nothing here can make a slot go blank. Players pick this up on their next launch, or on their next visit to the screen (the client refetches on screen entry, at most once a minute).",
    ja: "ゲームは、有効かつ期間内のもののうち表示順が最大のもの、次に新しいものを選ぶ。配信中のものがない掲載枠は現状のまま表示され、ここでの操作で枠が空になることはない。プレイヤーには次回の起動時、または次に画面を開いたときに反映される（クライアントは画面表示時に、最大 1 分に 1 回再取得する）。",
  },
  "ban.newBanner": { en: "+ New banner", ja: "＋ 新規バナー" },
  "ban.col.art": { en: "Art", ja: "画像" },
  "ban.col.link": { en: "Link", ja: "リンク" },
  "ban.col.sort": { en: "Sort", ja: "表示順" },
  "ban.noArt": { en: "no art", ja: "画像なし" },
  "ban.notTappable": { en: "none — not tappable", ja: "なし — タップ不可" },
  "ban.always": { en: "always", ja: "制限なし" },
  "ban.noExpiry": { en: "no expiry", ja: "無期限" },
  "ban.noWindow": { en: "no window", ja: "期間指定なし" },
  "ban.assignedTo": { en: "Assigned to {count} tournament(s)", ja: "{count} 件のトーナメントに割り当て済み" },
  "ban.emptyPlacement": { en: "Nothing scheduled — this slot shows the bundled {sprite}.", ja: "予定なし — この枠は同梱の {sprite} を表示する。" },
  "ban.confirmDeactivate": {
    en: "\"{label}\" is LIVE — players are seeing it right now.\nRe-type the label to switch it off:",
    ja: "「{label}」は配信中です — 現在プレイヤーに表示されています。\n無効にするにはラベルを再入力してください:",
  },

  // ---- tournaments: editor ----------------------------------------------
  "te.new": { en: "New tournament", ja: "新規トーナメント" },
  "te.save": { en: "Save changes", ja: "変更を保存" },
  "te.create": { en: "Create tournament", ja: "トーナメントを作成" },
  "te.createCopy": { en: "Create copy", ja: "コピーを作成" },
  "te.deleteReal": { en: "Delete for real", ja: "本当に削除する" },
  "te.dupFailed": { en: "Duplicate failed", ja: "複製に失敗しました" },
  "te.delFailed": { en: "Delete failed", ja: "削除に失敗しました" },
  "te.saveFailed": { en: "Save failed", ja: "保存に失敗しました" },
  "te.duplicated": { en: "Duplicated.", ja: "複製しました。" },
  "te.deleted": { en: "Deleted.", ja: "削除しました。" },
  "te.saved": { en: "Saved.", ja: "保存しました。" },
  "te.duplicate": { en: "Duplicate", ja: "複製" },
  "te.delete": { en: "Delete", ja: "削除" },
  "te.tab.details": { en: "Details", ja: "基本情報" },
  "te.tab.artwork": { en: "Artwork", ja: "画像" },
  "te.slug": { en: "Slug (game id)", ja: "スラッグ（ゲーム内 ID）" },
  "te.titleJa": { en: "Title (Japanese)", ja: "タイトル（日本語）" },
  "te.venue": { en: "Venue (playable course)", ja: "会場（プレイ可能コース）" },
  "te.holeSet": { en: "Hole set", ja: "ホールセット" },
  "te.start": { en: "Start (UTC)", ja: "開始（UTC）" },
  "te.end": { en: "End (UTC)", ja: "終了（UTC）" },
  "te.fee": { en: "Entry fee (RP)", ja: "参加費（RP）" },
  "te.resolveDelay": { en: "Resolve delay (minutes)", ja: "確定までの遅延（分）" },
  "te.botField": { en: "Bot field", ja: "ボットフィールド" },
  "te.league": { en: "League", ja: "リーグ" },
  "te.sponsor": { en: "Sponsor", ja: "スポンサー" },
  "te.descKey": { en: "Description localization key", ja: "説明のローカライズキー" },
  "te.descEn": { en: "Description (English)", ja: "説明（英語）" },
  "te.descJa": { en: "Description (Japanese)", ja: "説明（日本語）" },
  "te.locKey": { en: "Localization key", ja: "ローカライズキー" },
  "te.rankBands": { en: "Rank bands", ja: "順位別賞金" },
  "te.itemReward": { en: "Item reward", ja: "アイテム報酬" },
  "te.cardArt": { en: "Card artwork", ja: "カード画像" },
  "te.activeOn": { en: "Active — the game receives this", ja: "有効 — ゲームに配信される" },
  "te.activeOff": { en: "Inactive — hidden from the game", ja: "無効 — ゲームには表示されない" },
  "te.titleShadowed": { en: "Players will not see this title.", ja: "このタイトルはプレイヤーには表示されません。" },
  "te.clearKey": { en: "Clear the key and use this title", ja: "キーを消してこのタイトルを使う" },
  "te.botFieldHint": { en: "Filler so a young leaderboard is never empty. Bots are never paid.", ja: "参加者が少ない間もリーダーボードを埋めるための存在。ボットに賞金は支払われない。" },
  "te.bandsOk": { en: "Ladder is continuous from rank 1 with no gaps or overlaps.", ja: "1 位から連続していて、抜けも重複もありません。" },
  "te.removeArt": { en: "Remove — fall back to the course photo", ja: "削除して、コース写真に戻す" },
  "te.art.remote": { en: "Remote — the uploaded image, fetched and disk-cached by the client.", ja: "リモート — アップロードした画像。クライアントが取得してディスクにキャッシュする。" },
  "te.art.jpFallback": { en: "JP players see the English art (no JA upload).", ja: "日本語版の画像が未登録のため、日本語のプレイヤーにも英語版の画像が表示される。" },
  "te.notTappable": { en: "Not tappable — no link set.", ja: "タップできません — リンクが未設定です。" },
  "te.noBanner": { en: "None — the modal renders without a strip", ja: "なし — 帯なしでモーダルを表示" },
  "te.currentAssignment": { en: "(current assignment — inactive or removed)", ja: "（現在の割り当て — 無効または削除済み）" },
  "te.bannersFailed": { en: "Could not load banners.", ja: "バナーを読み込めませんでした。" },
  "te.bannersLoading": { en: "Loading banners…", ja: "バナーを読み込み中…" },
  "te.bannersPanel": { en: "Banners panel", ja: "バナー画面" },
  "te.entriesLoading": { en: "Loading entries…", ja: "エントリーを読み込み中…" },
  "te.entriesFailed": { en: "Failed to load entries", ja: "エントリーの読み込みに失敗しました" },
  "te.noEntries": { en: "No entries yet. Bot filler is generated at resolve time, not stored up front.", ja: "エントリーはまだありません。ボットは確定時に生成され、事前には保存されない。" },
  "te.col.character": { en: "Character", ja: "キャラクター" },
  "te.col.player": { en: "Player", ja: "プレイヤー" },
  "te.col.holes": { en: "Holes", ja: "ホール" },

  "te.title": { en: "Title", ja: "タイトル" },
  "te.active": { en: "Active", ja: "有効" },
  "te.col.status": { en: "Status", ja: "状態" },
  "te.col.score": { en: "Score", ja: "スコア" },
  "te.col.submitted": { en: "Submitted", ja: "提出日時" },
  "te.signupBanner": { en: "Sign-up modal banner", ja: "参加モーダルのバナー" },
  "te.restr": { en: "Entry restrictions", ja: "参加制限" },
  "te.restr.hint": {
    en: "Blank = unrestricted. The game shows these in the sign-up modal's RULES block and refuses an ineligible CONFIRM; the server independently enforces max players and the character bands at entry, before the fee is charged.",
    ja: "空欄＝制限なし。ゲームは参加モーダルのルール欄にこれらを表示し、条件を満たさないCONFIRMを拒否する。最大参加人数とキャラクター条件は、参加費の徴収前にサーバー側でも強制される。",
  },
  "te.restr.category": { en: "Category", ja: "カテゴリ" },
  "te.restr.divType": { en: "Division type", ja: "ディビジョン方式" },
  "te.restr.maxPlayers": { en: "Max players", ja: "最大参加人数" },
  "te.restr.maxPlayersHint": {
    en: "Human entries only — bots never count toward the cap.",
    ja: "人間のエントリーのみ。ボットは定員に含まれない。",
  },
  "te.restr.perDivision": { en: "Players per division", ja: "1ディビジョンの人数" },
  "te.restr.charRarity": { en: "Character rarity (min – max)", ja: "キャラクターレアリティ（下限 – 上限）" },
  "te.restr.charLevel": { en: "Character level (min – max)", ja: "キャラクターレベル（下限 – 上限）" },
  "te.restr.gear": { en: "Gear rule", ja: "ギア規定" },
  "te.restr.gearHint": {
    en: "own = players bring their bag. Client-enforced.",
    ja: "own＝プレイヤー自身のバッグを使用。クライアント側で強制。",
  },
  "te.restr.suppliedBlocked": {
    en: "blocked until standard-spec ships",
    ja: "標準スペック実装まで設定不可",
  },
  "te.restr.clubCap": { en: "Club rarity cap", ja: "クラブレアリティ上限" },
  "te.restr.clubCapHint": {
    en: "Highest club rarity allowed in the equipped bag. Client-enforced.",
    ja: "装備バッグ内で許可されるクラブレアリティの上限。クライアント側で強制。",
  },
  "te.restr.unset": { en: "(none)", ja: "（なし）" },
  "te.restr.unlimited": { en: "unlimited", ja: "無制限" },
  "te.restr.min": { en: "min", ja: "下限" },
  "te.restr.max": { en: "max", ja: "上限" },
  "te.signupDesc": { en: "Sign-up modal description", ja: "参加モーダルの説明" },
  "te.uploading": { en: "Uploading…", ja: "アップロード中…" },
  "te.uploaded": { en: "Uploaded.", ja: "アップロードしました。" },
  "te.uploadFailed": { en: "Upload failed", ja: "アップロードに失敗しました" },
  "te.saving": { en: "Saving…", ja: "保存中…" },
  "te.deleting": { en: "Deleting", ja: "削除中" },
  "te.noActive": { en: "No active", ja: "有効な" },

  "te.art.placeholder": { en: "Placeholder — no remote art and no bundled photo for this course. The card will render the fallback sprite.", ja: "代替画像 — リモート画像も、このコースの同梱写真もない。カードにはフォールバック用のスプライトが表示される。" },
  "te.art.bundled": { en: "Bundled — the shipped venue photo ({art}). Fine for a venue-named event, but a brand tournament needs its own art: every tournament on this course looks identical without one.", ja: "同梱 — 同梱されている会場写真（{art}）。会場名を冠したイベントなら十分だが、ブランド名のトーナメントには専用の画像が要る。用意しないと、このコースのトーナメントはすべて同じ見た目になる。" },
  "login.failed": { en: "Login failed", ja: "サインインに失敗しました" },

  "te.hint.active": { en: "Separate from Upcoming/Open/Ended, which is derived from the dates. This is whether the game is told the tournament exists at all. Switching it off does not eject a player who has already entered — they finish, nobody new sees it.", ja: "Upcoming / Open / Ended（日付から算出される状態）とは別物。ゲームにこのトーナメントの存在を伝えるかどうかを決める。オフにしても、すでに参加中のプレイヤーは追い出されない — 最後までプレイでき、新規には表示されなくなる。" },
  "te.hint.slug": { en: "Stable key the client keys off. Changing it on a live tournament orphans entries in any client that cached the old id.", ja: "クライアントが参照する固定キー。開催中に変更すると、古い ID をキャッシュしているクライアントのエントリーが孤立する。" },
  "te.hint.title": { en: "Free text, and independent of the venue — a tournament can be brand-led (“PUMA Summer Slam” at Lomond). This is what players see, since no localization key is set.", ja: "自由入力で、会場とは無関係。ブランド名を冠したトーナメント（例: Lomond 開催の「PUMA Summer Slam」）も設定できる。ローカライズキーが未設定のため、これがプレイヤーに表示される。" },
  "te.hint.titleJa": { en: "Shown to players on Japanese. Leave empty and they see the title above.", ja: "日本語のプレイヤーに表示される。空欄の場合は上のタイトルが表示される。" },
  "te.hint.titleJaUnused": { en: "Currently unused — the localization key overrides both titles.", ja: "現在は使用されていない — ローカライズキーが両方のタイトルより優先される。" },
  "te.hint.dates": { en: "Absolute UTC on purpose. State is derived from these two — there is no status switch to flip.", ja: "意図的に絶対 UTC で扱う。状態はこの 2 つから算出され、切り替えるスイッチは存在しない。" },
  "te.hint.sponsor": { en: "Text only — renders as “{sponsor} PRESENTS”.", ja: "テキストのみ — 「{sponsor} PRESENTS」と表示される。" },
  "te.hint.desc": { en: "The blurb beside the course photo in the sign-up modal. Leave both empty and the modal hides that whole row — photo included — rather than showing a lone image.", ja: "参加モーダルでコース写真の横に出る説明文。両方とも空にすると、写真を含めてその行ごと非表示になる（画像だけが残ることはない）。" },
  "te.hint.orphanBanner": { en: "This tournament points at a banner that is no longer active. Players see no strip. Pick another, or None.", ja: "このトーナメントは、無効になったバナーを参照している。プレイヤーには帯が表示されない。別のバナーを選ぶか「なし」にすること。" },
  "te.liveWarn": { en: "This tournament is {state}. Players may be mid-entry — changing the fee, dates or prize ladder now changes the deal underneath them. Saving requires re-typing the slug below.", ja: "このトーナメントは現在「{state}」です。参加中のプレイヤーがいる可能性があり、参加費・日程・賞金を変えると条件が途中で変わってしまいます。保存するには下にスラッグを入力してください。" },
  "te.tab.prizes": { en: "Prizes", ja: "賞金" },
  "te.tab.entries": { en: "Entries", ja: "エントリー" },

  "te.hint.titleShadowedBody": { en: "The localization key {key} is set, and a key that resolves in the shipped build always wins — the title is only the fallback. Clear the key to make this title the name players see, in every language.", ja: "ローカライズキー {key} が設定されている。ビルドに含まれるキーが解決できる場合は常にそちらが優先され、タイトルはフォールバックにすぎない。キーを消すと、このタイトルがすべての言語でプレイヤーに表示される名前になる。" },
  "te.hint.venue": { en: "Where it is played, and the venue subtitle. Default art only — {art}", ja: "プレイされる場所と、会場のサブタイトル。既定の画像のみ — {art}" },
  "te.hint.holeSet": { en: "{n} holes · ranges and lists, expanded client-side", ja: "{n} ホール · 範囲指定とリストに対応。展開はクライアント側で行う" },
  "te.hint.bands": { en: "Bands are per-tournament, not a shared template: raising this tournament's first prize cannot silently change another's. Payouts run through earn_pts_v2 under the tournament_prize action, capped at 2000 RP per event.", ja: "賞金設定はトーナメントごとに独立しており、共有テンプレートではない。あるトーナメントの 1 位賞金を上げても、他に影響しない。支払いは tournament_prize アクションで earn_pts_v2 を通して行われ、1 回あたり 2000 RP が上限。" },
  "te.holeSetBad": { en: "malformed", ja: "書式が不正" },

  // ---- telemetry panel (SPEC telemetry_admin_panel) ----------------------
  "tel.title": { en: "Telemetry", ja: "テレメトリ" },
  "tel.subtitle": { en: "telemetry_events · read-only beta analytics", ja: "telemetry_events · 閲覧専用のベータ分析" },
  "tel.loading": { en: "Loading telemetry…", ja: "テレメトリを読み込み中…" },
  "tel.loadFailed": { en: "Failed to load telemetry", ja: "テレメトリの読み込みに失敗しました" },
  "tel.range.from": { en: "From", ja: "開始" },
  "tel.range.to": { en: "To", ja: "終了" },
  "tel.range.apply": { en: "Apply", ja: "適用" },
  "tel.range.reset": { en: "Last 7 days", ja: "直近 7 日間" },
  "tel.rowsScanned": { en: "{n} rows scanned", ja: "{n} 件を集計" },
  "tel.truncated": { en: "TRUNCATED", ja: "打ち切り" },
  "tel.truncatedHint": {
    en: "The 10,000-row read cap was hit. Every number on this page is computed from a PARTIAL window — narrow the date range before trusting them.",
    ja: "1 万件の読み取り上限に達しました。このページの数値はすべて範囲の一部だけから計算されています。信用する前に期間を狭めてください。",
  },
  "tel.noTable": { en: "telemetry_events does not exist yet", ja: "telemetry_events はまだ存在しません" },
  "tel.noTableBody": {
    en: "The beta_telemetry migration has not been applied to this Supabase project. Every figure below is a true zero, not a failed query. Apply migrations/2026_08_18_telemetry_events.sql and reload.",
    ja: "この Supabase プロジェクトには beta_telemetry のマイグレーションがまだ適用されていません。以下の数値はクエリ失敗ではなく、すべて正真正銘のゼロです。migrations/2026_08_18_telemetry_events.sql を適用してから再読み込みしてください。",
  },
  "tel.empty": { en: "No telemetry events in this range.", ja: "この期間のテレメトリイベントはありません。" },
  "tel.emptyBody": {
    en: "Either no tester has played yet, or the client is not sending. The table exists and the query succeeded — this is the zero state, not an error.",
    ja: "テスターがまだプレイしていないか、クライアントが送信していません。テーブルは存在し、クエリも成功しています。エラーではなく、データがない状態です。",
  },

  "tel.tab.kpis": { en: "Overview", ja: "概要" },
  "tel.tab.funnel": { en: "Funnel", ja: "ファネル" },
  "tel.tab.holes": { en: "Holes", ja: "ホール" },
  "tel.tab.shots": { en: "Shot quality", ja: "ショット品質" },
  "tel.tab.testers": { en: "Testers", ja: "テスター" },
  "tel.tab.events": { en: "Events", ja: "イベント" },

  "tel.kpi.testers": { en: "Active testers", ja: "アクティブテスター" },
  "tel.kpi.sessions": { en: "Sessions", ja: "セッション" },
  "tel.kpi.rounds": { en: "Rounds started", ja: "開始ラウンド" },
  "tel.kpi.holes": { en: "Holes completed", ja: "完了ホール" },
  "tel.kpi.abandons": { en: "Abandons", ja: "中断" },
  "tel.kpi.crashes": { en: "Crashes", ja: "クラッシュ" },
  "tel.kpi.today": { en: "{n} today", ja: "本日 {n}" },
  "tel.kpi.ofRounds": { en: "{pct} of rounds", ja: "ラウンドの {pct}" },
  "tel.kpi.clean": { en: "none", ja: "なし" },

  "tel.funnel.title": { en: "Session funnel", ja: "セッションファネル" },
  "tel.funnel.hint": {
    en: "Share of sessions that reached each stage. A session counts at a stage when it reached that stage or any later one, so a dropped screen_view cannot make the funnel read as increasing.",
    ja: "各段階に到達したセッションの割合。より後の段階に到達したセッションは前の段階にも計上されるため、screen_view が欠落してもファネルが増加して見えることはない。",
  },
  "tel.funnel.session_start": { en: "App opened", ja: "アプリ起動" },
  "tel.funnel.home": { en: "Reached Home", ja: "ホーム到達" },
  "tel.funnel.hole_select": { en: "Reached hole selection", ja: "ホール選択到達" },
  "tel.funnel.round_start": { en: "Started a round", ja: "ラウンド開始" },
  "tel.funnel.hole_complete": { en: "Completed a hole", ja: "ホール完了" },

  "tel.holes.title": { en: "Per-hole difficulty", ja: "ホール別の難易度" },
  "tel.holes.col.hole": { en: "Hole", ja: "ホール" },
  "tel.holes.col.plays": { en: "Plays", ja: "プレイ数" },
  "tel.holes.col.completions": { en: "Completed", ja: "完了" },
  "tel.holes.col.abandons": { en: "Abandoned", ja: "中断" },
  "tel.holes.col.strokes": { en: "Avg strokes", ja: "平均打数" },
  "tel.holes.col.penalty": { en: "Avg penalty", ja: "平均ペナルティ" },
  "tel.holes.col.ob": { en: "OB rate", ja: "OB 率" },
  "tel.holes.col.duration": { en: "Avg duration", ja: "平均所要時間" },
  "tel.holes.col.fps": { en: "fps_low (median)", ja: "fps_low（中央値）" },
  "tel.holes.none": { en: "No rounds played in this range.", ja: "この期間にプレイされたラウンドはありません。" },

  "tel.shots.title": { en: "Shot quality — do the controls work", ja: "ショット品質 — 操作は機能しているか" },
  "tel.shots.flickReject": { en: "Flick reject rate", ja: "フリック却下率" },
  "tel.shots.flickRejectHint": {
    en: "flick_rejected ÷ (flick_rejected + shot_taken). The headline number of the beta: every rejected flick is a tester who meant to hit the ball and did not.",
    ja: "flick_rejected ÷（flick_rejected + shot_taken）。ベータで最も重要な数値。却下されたフリックはすべて、打とうとして打てなかったテスターを意味する。",
  },
  "tel.shots.cancel": { en: "Cancel rate", ja: "キャンセル率" },
  "tel.shots.ob": { en: "OB rate", ja: "OB 率" },
  "tel.shots.taken": { en: "Shots taken", ja: "ショット数" },
  "tel.shots.col.club": { en: "Club", ja: "クラブ" },
  "tel.shots.col.shots": { en: "Shots", ja: "ショット数" },
  "tel.shots.col.distance": { en: "Avg distance", ja: "平均飛距離" },
  "tel.shots.none": { en: "No shots recorded in this range.", ja: "この期間に記録されたショットはありません。" },

  "tel.testers.title": { en: "Testers", ja: "テスター" },
  "tel.testers.col.tester": { en: "Tester", ja: "テスター" },
  "tel.testers.col.device": { en: "Device", ja: "端末" },
  "tel.testers.col.build": { en: "Build", ja: "ビルド" },
  "tel.testers.col.sessions": { en: "Sessions", ja: "セッション" },
  "tel.testers.col.playTime": { en: "Play time", ja: "プレイ時間" },
  "tel.testers.col.rounds": { en: "Rounds", ja: "ラウンド" },
  "tel.testers.col.holes": { en: "Holes", ja: "ホール" },
  "tel.testers.col.points": { en: "Points Δ", ja: "ポイント差" },
  "tel.testers.col.crashes": { en: "Crashes", ja: "クラッシュ" },
  "tel.testers.col.lastSeen": { en: "Last seen", ja: "最終観測" },
  "tel.testers.unclean": { en: "{n} unclean", ja: "異常終了 {n}" },
  "tel.testers.uncleanHint": {
    en: "Sessions with no session_end event: the app was killed, or the last batch never flushed. Their play time is missing from the total.",
    ja: "session_end イベントのないセッション。アプリが強制終了されたか、最後のバッチが送信されなかったことを意味する。プレイ時間は合計に含まれない。",
  },
  "tel.testers.none": { en: "No testers seen in this range.", ja: "この期間に観測されたテスターはいません。" },
  "tel.testers.filter": { en: "Show only this tester's events below", ja: "下のイベントをこのテスターだけに絞り込む" },

  "tel.events.title": { en: "Event explorer", ja: "イベントエクスプローラ" },
  "tel.events.allNames": { en: "All events", ja: "すべてのイベント" },
  "tel.events.allTesters": { en: "All testers", ja: "すべてのテスター" },
  "tel.events.col.received": { en: "Received", ja: "受信" },
  "tel.events.col.ts": { en: "Client ts", ja: "端末時刻" },
  "tel.events.col.tester": { en: "Tester", ja: "テスター" },
  "tel.events.col.name": { en: "Event", ja: "イベント" },
  "tel.events.col.session": { en: "Session", ja: "セッション" },
  "tel.events.col.payload": { en: "Payload", ja: "ペイロード" },
  "tel.events.expand": { en: "Click a payload to expand it.", ja: "ペイロードをクリックすると展開されます。" },
  "tel.events.none": { en: "No events match these filters.", ja: "条件に一致するイベントはありません。" },
  "tel.events.count": { en: "{n} matching events", ja: "一致 {n} 件" },

  // ---- content catalogs (content_admin_panels) ---------------------------
  // Untranslated by design (ADMIN_DASHBOARD_OPS.md §3.4): catalog names, DB
  // column names, row ids, sprite names, and the LIVE/SCHEDULED/ENDED/OFF
  // state badges.

  "c.loading": { en: "Loading catalog…", ja: "カタログを読み込み中…" },
  "c.loadFailed": { en: "Could not load the catalog", ja: "カタログを読み込めませんでした" },
  "c.search": { en: "Search id or name…", ja: "ID・名前で検索…" },
  "c.searchTexts": { en: "Search key or English…", ja: "キー・英文で検索…" },
  "c.rows.none": { en: "No rows match these filters.", ja: "条件に一致する行はありません。" },
  "c.rows.count": { en: "{shown} of {total} rows", ja: "{total} 件中 {shown} 件" },
  "c.page": { en: "Page {page} of {pages}", ja: "{pages} ページ中 {page} ページ目" },
  "c.serverPaged": {
    en: "Paged on the server — only this page is fetched.",
    ja: "サーバー側でページ分割。表示中のページのみ取得します。",
  },

  "c.col.rowId": { en: "Row id", ja: "行 ID" },
  "c.col.state": { en: "State", ja: "状態" },
  "c.col.minBuild": { en: "Min build", ja: "最小ビルド" },

  "c.badge.dirty": { en: "{n} unpublished", ja: "未公開 {n}" },
  "c.badge.dirtyHint": {
    en: "Draft rows that differ from what the game is being served. A publish would apply exactly these.",
    ja: "配信中の内容と異なる下書き行の数。公開するとこの行がそのまま反映されます。",
  },
  "c.badge.clean": { en: "No unpublished changes", ja: "未公開の変更なし" },
  "c.badge.disabled": { en: "DISABLED", ja: "配信停止中" },
  "c.badge.disabledHint": {
    en: "The kill switch is off, so the game does not receive this catalog at all and falls back to its bundled CSV.",
    ja: "キルスイッチが OFF のため、ゲームはこのカタログを受信せず、同梱 CSV にフォールバックします。",
  },
  "c.globalKill.headline": {
    en: "Remote content is OFF for every player",
    ja: "リモートコンテンツはすべてのプレイヤーで OFF です",
  },
  "c.globalKill.body": {
    en: "content_settings.content_enabled is false, so every client ignores the content response and runs on its bundled CSVs. Editing and publishing still work here, but nothing reaches a player until it is switched back on — Review & publish ▸ Kill switch.",
    ja: "content_settings.content_enabled が false のため、すべてのクライアントがコンテンツ応答を無視し同梱 CSV で動作しています。この画面での編集と公開は引き続き可能ですが、再度 ON にするまでプレイヤーには届きません（「差分を確認して公開」▸「キルスイッチ」）。",
  },
  "c.version": { en: "Published v{n}", ja: "公開中 v{n}" },
  "c.publishOpen": { en: "Review & publish", ja: "差分を確認して公開" },
  "c.newRow": { en: "+ New row", ja: "+ 新規行" },

  "c.facet.brand": { en: "Brand", ja: "ブランド" },
  "c.facet.type": { en: "Type", ja: "種別" },
  "c.facet.rarity": { en: "Rarity", ja: "レアリティ" },
  "c.facet.category": { en: "Category", ja: "カテゴリ" },
  "c.facet.any": { en: "Any {label}", ja: "{label}: すべて" },
  "c.facet.serverNote": {
    en: "Runs as a server query over the whole catalog, combined with the other filters — not a narrowing of the loaded page.",
    ja: "カタログ全体に対するサーバー側クエリとして、他のフィルタと組み合わせて実行されます。表示中のページ内での絞り込みではありません。",
  },

  "c.edit.title": { en: "Edit draft row", ja: "下書き行を編集" },
  "c.edit.newTitle": { en: "New draft row", ja: "下書き行を新規作成" },
  // The id is the one field that cannot be changed later: `upsertDraftRow` keys
  // the upsert on it, and publish keys `on conflict (catalog, row_id)`.
  "c.edit.rowIdHint": {
    en: "Lower-case letters, digits and underscores, up to {max}. Written into data.{column} automatically, and fixed once saved — it is what every other catalog resolves against.",
    ja: "英小文字・数字・アンダースコアのみ、最大 {max} 文字。data.{column} には自動で書き込まれます。保存後は変更できません（他のカタログが参照する識別子です）。",
  },
  "c.edit.rowIdInvalid": {
    en: "Row id must be lower-case letters, digits and underscores only (texts keys may be upper-case), and at most {max} characters.",
    ja: "行 ID は英小文字・数字・アンダースコアのみ（texts のキーは大文字も可）、最大 {max} 文字です。",
  },
  "c.edit.rowIdTaken": {
    en: "Row id \"{rowId}\" is already taken in this catalog",
    ja: "行 ID「{rowId}」はこのカタログで既に使用されています",
  },
  "c.edit.subtitle": {
    en: "Drafts are never served to the game. Publish is the gate.",
    ja: "下書きはゲームに配信されません。公開が唯一のゲートです。",
  },
  "c.edit.active": { en: "Active", ja: "有効" },
  "c.edit.activeHint": {
    en: "Turning this off deactivates the row: it leaves shops and pools, and every player who already owns one keeps it. Nothing is ever deleted.",
    ja: "OFF にすると行が無効化されます。ショップや排出から外れますが、既に所持しているプレイヤーは保持したままです。削除は行われません。",
  },
  "c.edit.minBuildHint": {
    en: "Withheld from any build below this number. Immutable once published.",
    ja: "この番号未満のビルドには配信されません。公開後は変更できません。",
  },
  // content_two_way §6 — a sprite column names a FILE the BUILD must already
  // ship; it is not an upload and not a URL. Rows whose art is missing are
  // withheld on that build (§4), which is safe but invisible, so the constraint
  // is spelled out at the field. Clubs get their own line: they fall back to the
  // shared Placeholder sprite instead of being withheld (§4, decision of record).
  "c.edit.spriteHint": {
    en: "Must match a file under Resources/{folder}/ in the build. Rows whose art is missing are withheld on that build (clubs show Placeholder).",
    ja: "ビルド内の Resources/{folder}/ にあるファイル名と一致する必要があります。アートが見つからない行は、そのビルドでは非表示になります（クラブは Placeholder が表示されます）。",
  },
  "c.edit.spriteHintClubs": {
    en: "Must match a file under Resources/{folder}/ in the build. A club whose art is missing still shows, using the shared Placeholder sprite.",
    ja: "ビルド内の Resources/{folder}/ にあるファイル名と一致する必要があります。アートが見つからないクラブも、共通の Placeholder 画像で表示されます。",
  },

  "c.edit.save": { en: "Save draft", ja: "下書きを保存" },
  "c.edit.saving": { en: "Saving…", ja: "保存中…" },
  "c.edit.saved": { en: "Draft saved. Publish to send it to the game.", ja: "下書きを保存しました。ゲームに反映するには公開してください。" },
  "c.edit.saveFailed": { en: "Save failed", ja: "保存に失敗しました" },

  // ---- publish drawer ----------------------------------------------------
  "cp.title": { en: "Publish {catalog}", ja: "{catalog} を公開" },
  "cp.tab.diff": { en: "Changes", ja: "変更点" },
  "cp.tab.history": { en: "Version history", ja: "バージョン履歴" },
  "cp.tab.switch": { en: "Kill switch", ja: "キルスイッチ" },

  "cp.diff.loading": { en: "Computing the diff…", ja: "差分を計算中…" },
  "cp.diff.failed": { en: "Could not compute the diff", ja: "差分を計算できませんでした" },
  "cp.diff.none": {
    en: "Drafts match what is published. There is nothing to publish.",
    ja: "下書きは公開中の内容と一致しています。公開する変更はありません。",
  },
  "cp.diff.added": { en: "added", ja: "追加" },
  "cp.diff.changed": { en: "changed", ja: "変更" },
  "cp.diff.deactivated": { en: "deactivated", ja: "無効化" },
  "cp.diff.reactivated": { en: "reactivated", ja: "再有効化" },
  "cp.diff.col.field": { en: "Field", ja: "項目" },
  "cp.diff.col.before": { en: "Published", ja: "公開中" },
  "cp.diff.col.after": { en: "Draft", ja: "下書き" },
  "cp.diff.deactivatedNote": {
    en: "A deactivated row leaves shops and gacha pools; players who own it keep it. It is never deleted.",
    ja: "無効化された行はショップや排出から外れますが、所持しているプレイヤーは保持したままです。削除はされません。",
  },
  "cp.diff.truncated": {
    en: "Showing the first {shown} of {total} changed rows.",
    ja: "変更 {total} 件のうち先頭 {shown} 件を表示しています。",
  },

  "cp.confirm.headline": { en: "This goes live to every player.", ja: "この操作は全プレイヤーに即時反映されます。" },
  "cp.confirm.body": {
    en: "Publishing replaces what {catalog} serves from v{from} onward. Read the changes above first — this is the only place they are shown.",
    ja: "公開すると {catalog} の配信内容が v{from} から置き換わります。上の変更点を必ず確認してください。ここが唯一の確認画面です。",
  },
  "cp.confirm.check": { en: "I have read the changes above", ja: "上の変更点を確認しました" },
  "cp.note.label": { en: "Note (optional)", ja: "メモ（任意）" },
  "cp.note.placeholder": { en: "Why this publish — stored on the version snapshot", ja: "公開理由。バージョンのスナップショットに保存されます" },

  "cp.publish": { en: "Publish now", ja: "今すぐ公開" },
  "cp.publishing": { en: "Publishing…", ja: "公開中…" },
  "cp.published": { en: "Published {catalog} v{version}.", ja: "{catalog} v{version} を公開しました。" },
  "cp.publishFailed": { en: "Publish failed — nothing was published", ja: "公開に失敗しました。何も公開されていません" },
  "cp.problems.title": { en: "Blocking problems ({n})", ja: "公開できない問題 ({n} 件)" },
  "cp.problems.body": {
    en: "Every problem below has to be fixed. Nothing was published — not the valid rows, not a subset.",
    ja: "以下の問題をすべて解消する必要があります。正常な行を含め、一切公開されていません。",
  },
  "cp.warnings.title": { en: "Warnings ({n})", ja: "警告 ({n} 件)" },
  "cp.warnings.body": {
    en: "Published anyway — these are advisory, not rules.",
    ja: "公開は完了しています。これらは参考情報であり、規則ではありません。",
  },

  "cp.history.title": { en: "Version history", ja: "バージョン履歴" },
  "cp.history.forward": {
    en: "Rollback moves FORWARD. Restoring v{example} republishes that snapshot as a NEW, HIGHER version — the counter never decreases. A client that already fetched the bad version only learns about the fix because the number went UP.",
    ja: "ロールバックは「前に進む」操作です。v{example} を復元すると、そのスナップショットが新しい、より大きなバージョンとして再公開されます。番号が戻ることはありません。不具合のあるバージョンを既に取得したクライアントは、番号が上がることでのみ修正を認識できます。",
  },
  "cp.history.current": { en: "Current", ja: "現在" },
  // Short by necessity: measured 2026-08-25, "Restore this version" rendered
  // 122px into a 104px column and pushed the button outside the drawer. The
  // full phrase lives in the title attribute instead.
  "cp.history.restore": { en: "Restore", ja: "復元" },
  "cp.history.restoreHint": {
    en: "Republish this snapshot as a new, higher version.",
    ja: "このスナップショットを、新しくより大きなバージョンとして再公開します。",
  },
  "cp.history.confirm": {
    en: "Restore v{version}? It will be republished as v{next}.",
    ja: "v{version} を復元しますか？ v{next} として再公開されます。",
  },
  "cp.history.done": { en: "Restored v{from} — published forward as v{version}.", ja: "v{from} を復元し、v{version} として再公開しました。" },
  "cp.history.failed": { en: "Rollback failed", ja: "ロールバックに失敗しました" },
  "cp.history.col.version": { en: "Version", ja: "バージョン" },
  "cp.history.col.when": { en: "When", ja: "日時" },
  "cp.history.col.what": { en: "What changed", ja: "変更内容" },
  "cp.history.source": {
    en: "Every published snapshot, read from content_versions — including v1, the seeded baseline. Paged, not capped: the oldest version is always reachable, because it is the one you want in an emergency.",
    ja: "content_versions から読み取った、公開済みスナップショットのすべてです（シード投入された v1 を含む）。件数の打ち切りはなくページ送りで表示するため、最も古いバージョンにも必ず到達できます。緊急時に必要になるのはそのバージョンだからです。",
  },
  "cp.history.col.rows": { en: "Rows", ja: "行数" },
  "cp.history.seed": { en: "SEED", ja: "初期" },
  "cp.history.seedHint": {
    en: "The baseline every catalog started from, applied by SQL before this dashboard existed. The most likely rollback target in an emergency.",
    ja: "各カタログの出発点となるバージョン。この管理画面ができる前に SQL で投入されました。緊急時に最も選ばれる可能性が高いロールバック先です。",
  },
  "cp.history.bySeed": { en: "(seeded)", ja: "（シード投入）" },
  "cp.history.none": { en: "No versions recorded yet.", ja: "記録されているバージョンはまだありません。" },
  "cp.history.total": { en: "{n} versions", ja: "全 {n} バージョン" },

  "cp.enabled.title": { en: "Serve this catalog to the game", ja: "このカタログをゲームに配信する" },
  "cp.enabled.on": {
    en: "ON — the game receives {catalog} from /api/v1/content.",
    ja: "ON — ゲームは /api/v1/content から {catalog} を受信します。",
  },
  "cp.enabled.off": {
    en: "OFF — {catalog} vanishes from /api/v1/content entirely and the game runs on its bundled CSV. It is never sent as an empty catalog, which a client could apply as 'everything was deleted'.",
    ja: "OFF — {catalog} は /api/v1/content から完全に消え、ゲームは同梱 CSV で動作します。「空のカタログ」としては送信されません（クライアントが「全削除」と解釈しうるため）。",
  },
  "cp.enabled.disable": { en: "Stop serving", ja: "配信を停止" },
  "cp.enabled.enable": { en: "Resume serving", ja: "配信を再開" },
  "cp.enabled.failed": { en: "Could not change the kill switch", ja: "キルスイッチを変更できませんでした" },

  // The GLOBAL kill switch — a DIFFERENT switch from the per-catalog one above
  // (content_settings.content_enabled). Every string here names its blast radius explicitly,
  // because "kill switch" on its own is the phrase that let the two be confused for one.
  "cp.global.title": { en: "Serve remote content at all", ja: "リモートコンテンツ配信そのもの" },
  "cp.global.tag": { en: "ALL CATALOGS", ja: "全カタログ" },
  "cp.global.on": {
    en: "ON — the game reads remote content. Killing this reverts EVERY catalog to its bundled CSV, for EVERY player.",
    ja: "ON — ゲームはリモートコンテンツを読み込みます。これを停止すると、すべてのプレイヤーで全カタログが同梱 CSV に戻ります。",
  },
  "cp.global.off": {
    en: "OFF — every client is ignoring the content response entirely and running on its bundled CSVs. Nothing you publish reaches a player until this is back on.",
    ja: "OFF — すべてのクライアントがコンテンツ応答を完全に無視し、同梱 CSV で動作しています。これを ON に戻すまで、公開した内容はプレイヤーに届きません。",
  },
  "cp.global.timing": {
    en: "Not instant: up to ~60s of response cache to reach a client, then applied at that client's NEXT LAUNCH. Turning it back on costs another launch, because a killed client has already dropped its caches.",
    ja: "即時ではありません。クライアントに届くまで応答キャッシュで最大約 60 秒、その後そのクライアントの次回起動時に適用されます。再開時はキャッシュを破棄済みのため、さらに 1 回の起動が必要です。",
  },
  "cp.global.disable": { en: "Kill remote content (all catalogs)", ja: "リモートコンテンツを停止（全カタログ）" },
  "cp.global.enable": { en: "Resume remote content", ja: "リモートコンテンツを再開" },
  "cp.global.confirm": {
    en: "Kill remote content for EVERY catalog and EVERY player?\n\nEvery client reverts to its bundled CSVs at its next launch, and turning it back on costs them another launch. The per-catalog switch above is the one that affects a single catalog.",
    ja: "すべてのカタログ・すべてのプレイヤーに対してリモートコンテンツを停止しますか？\n\n各クライアントは次回起動時に同梱 CSV に戻り、再開にはさらに 1 回の起動が必要です。1 つのカタログだけを止めたい場合は、上のカタログ別スイッチを使用してください。",
  },
  "cp.global.failed": { en: "Could not change the global kill switch", ja: "グローバルキルスイッチを変更できませんでした" },
  "cp.global.row": {
    en: "Stored as content_settings.content_enabled. It is a database row, not a deploy — but it used to have no control here at all, so flipping it meant hand-writing SQL.",
    ja: "content_settings.content_enabled として保存されます。デプロイではなくデータベースの 1 行です。以前はこの画面に操作がなく、切り替えには SQL を手書きする必要がありました。",
  },

  // ---- clubs / characters / items / texts --------------------------------
  "cl.title": { en: "Clubs", ja: "クラブ" },
  "ch.title": { en: "Characters", ja: "キャラクター" },
  // content_two_way §6 — the admin can create a character's DATA today; its ART
  // ships with the next build that bundles the sprites. Until then §4 withholds
  // it everywhere rather than drawing a blank card. Art by URL, which would make
  // it render on an installed build, is the next spec (content_art_urls).
  "ch.notice.headline": {
    en: "Creating a character here creates its data.",
    ja: "ここでキャラクターを作成すると、そのデータが作成されます。",
  },
  "ch.notice.body": {
    en: "Its art ships with the next build that bundles the sprites; until then it is withheld on every build — it appears in no roster, no shop and no pool, rather than showing as a blank card. Sprite names must match files under Resources/Portraits/.",
    ja: "アートは、スプライトを同梱する次のビルドで配信されます。それまでは、どのビルドでも非表示となり、ロスター・ショップ・排出のいずれにも表示されません（空のカードとして表示されることはありません）。スプライト名は Resources/Portraits/ 内のファイル名と一致する必要があります。",
  },
  "it.title": { en: "Items, Bags & Balls", ja: "アイテム・バッグ・ボール" },
  "it.tab.items": { en: "Items", ja: "アイテム" },
  "it.tab.bags": { en: "Bags", ja: "バッグ" },
  "it.tab.balls": { en: "Balls", ja: "ボール" },
  "it.oneCatalogNote": {
    en: "Three catalogs, one panel — each publishes independently.",
    ja: "3 つのカタログを 1 つの画面に集約しています。公開はカタログごとに独立しています。",
  },
  "tx.title": { en: "Texts", ja: "テキスト" },
  "tx.col.key": { en: "Key", ja: "キー" },
  "tx.col.en": { en: "English", ja: "英語" },
  "tx.col.ja": { en: "Japanese", ja: "日本語" },
  "tx.prefix": { en: "Key prefix", ja: "キーの接頭辞" },
  "tx.prefix.any": { en: "All prefixes", ja: "すべての接頭辞" },
  "tx.missingJa": { en: "No Japanese", ja: "日本語なし" },
  "tx.missingJaHint": {
    en: "This key falls back to English in the game.",
    ja: "このキーはゲーム内で英語にフォールバックします。",
  },

  // ---- shop --------------------------------------------------------------
  "sh.title": { en: "Shop", ja: "ショップ" },
  // ---- users ▸ inventory tab (content_player_inventory §5, §6) -----------
  //
  // ⚠️ uinv.notice.* is the SPEC §6 disclosure and is the counterpart of
  // sh.notice.* below: moving something server-side makes it very easy to assume
  // it is now enforced. It is not. Do not soften or drop these two strings.
  "uinv.notice.headline": {
    en: "This inventory is NOT server-enforced.",
    ja: "このインベントリはサーバーで検証されていません。",
  },
  "uinv.notice.body": {
    en: "Everything below was asserted by the player's client and backed up as-is. Inventory sync is backup and cross-device restore, not anti-cheat — a modified client can still grant itself anything. Server-authoritative purchases are a separate, later decision. Read this tab as a RECORD of what a device reported, not as proof of what was earned.",
    ja: "以下の内容はプレイヤーのクライアントが申告したものをそのまま保存したものです。インベントリ同期はバックアップと機種変更時の復元のための仕組みであり、チート対策ではありません。改造クライアントは依然として何でも自己付与できます。サーバー側で購入を検証する仕組みは別途、後日の判断となります。この画面は「端末が報告した記録」であり、「正当に入手した証明」ではありません。",
  },
  "uinv.rev": { en: "rev", ja: "rev" },
  "uinv.lastSync": { en: "Last sync", ja: "最終同期" },
  "uinv.size": { en: "Blob size", ja: "データサイズ" },
  "uinv.neverSynced": {
    en: "This player has never synced an inventory. Normal for an account that has not launched a build with sync in it — their local save is still the only copy.",
    ja: "このプレイヤーはまだインベントリを同期していません。同期対応ビルドを起動していないアカウントでは正常な状態で、端末内のセーブが唯一のコピーです。",
  },
  "uinv.clubs": { en: "Clubs", ja: "クラブ" },
  "uinv.characters": { en: "Characters", ja: "キャラクター" },
  "uinv.items": { en: "Items", ja: "アイテム" },
  "uinv.balls": { en: "Balls", ja: "ボール" },
  "uinv.tickets": { en: "Gacha tickets", ja: "ガチャチケット" },
  "uinv.holes": { en: "Unlocked holes", ja: "解放済みホール" },
  "uinv.starter": { en: "Starter character", ja: "スターターキャラ" },
  "uinv.selected": { en: "Selected character", ja: "選択中キャラ" },
  "uinv.noClubs": { en: "No clubs in the blob.", ja: "クラブは記録されていません。" },
  "uinv.noCharacters": { en: "No characters in the blob.", ja: "キャラクターは記録されていません。" },
  "uinv.noItems": { en: "No items.", ja: "アイテムはありません。" },
  "uinv.noBalls": { en: "No balls.", ja: "ボールはありません。" },
  "uinv.noTickets": { en: "No tickets.", ja: "チケットはありません。" },
  "uinv.unlimited": { en: "unlimited", ja: "無制限" },
  "uinv.atDefault": { en: "default", ja: "初期値" },
  "uinv.showRaw": { en: "Show the stored blob", ja: "保存されている生データを表示" },
  "uinv.hideRaw": { en: "Hide the stored blob", ja: "生データを隠す" },
  "uinv.grants": { en: "Grants", ja: "付与キュー" },
  "uinv.noGrants": { en: "No grants issued.", ja: "付与はありません。" },
  "uinv.grantPending": { en: "PENDING", ja: "未受取" },
  "uinv.grantApplied": { en: "APPLIED", ja: "受取済" },
  "uinv.appliedAt": { en: "applied", ja: "受取" },
  "uinv.grantsHint": {
    en: "A grant is queued, not written into the inventory: the player's client owns that blob and writes it back every 30 seconds, so an admin edit would race it. The player picks a grant up on their NEXT LAUNCH, applies it once, and acknowledges it. Grants are additive-only and idempotent — re-issuing the same one is a new grant, but the same grant can never apply twice.",
    ja: "付与はインベントリに直接書き込まれず、キューに積まれます。インベントリはプレイヤーのクライアントが所有し 30 秒ごとに書き戻すため、管理画面から直接編集すると競合します。プレイヤーは次回起動時に受け取り、一度だけ適用して確認応答します。付与は加算のみで冪等です。同じ内容をもう一度発行すれば別の付与になりますが、同一の付与が二重に適用されることはありません。",
  },

  "uinv.revoke": { en: "Revoke", ja: "取り消し" },
  "uinv.revokeHint": {
    en: "Delete this grant from the queue before the player picks it up. Only possible while it is PENDING — once applied, the player has it, and nothing in this system can subtract it.",
    ja: "プレイヤーが受け取る前に、この付与をキューから削除します。「未受取」の間だけ可能です。受取済みになるとプレイヤーの所持品となり、このシステムには減算する手段がありません。",
  },

  // ---- users ▸ revoke-grant modal ----------------------------------------
  "urevoke.title": { en: "Revoke this grant?", ja: "この付与を取り消しますか？" },
  "urevoke.body": {
    en: "{amount}× {refId} ({kind}) is still queued and has NOT been applied. Revoking removes it from the queue, so the player never receives it. This is the only chance: grants are additive-only, so once it is applied nothing here can take it back.",
    ja: "{amount}×{refId}（{kind}）はキューに残っており、まだ適用されていません。取り消すとキューから削除され、プレイヤーには届きません。取り消せるのは今だけです。付与は加算のみのため、適用後はこの画面から取り戻す手段はありません。",
  },
  "urevoke.confirm": { en: "Revoke grant", ja: "付与を取り消す" },

  // ---- users ▸ grant modal ----------------------------------------------
  "ugrant.title": { en: "Grant inventory", ja: "インベントリを付与" },
  "ugrant.kind": { en: "Kind", ja: "種別" },
  "ugrant.refId": { en: "Catalog id", ja: "カタログ ID" },
  "ugrant.refIdPlaceholder": { en: "club_iron9_klyro", ja: "club_iron9_klyro" },
  "ugrant.refIdNumeric": { en: "Number", ja: "番号" },
  "ugrant.refIdNumericPlaceholder": { en: "a ticket type or hole number, e.g. 0", ja: "チケット種別またはホール番号（例: 0）" },
  "ugrant.amount": { en: "Amount", ja: "数量" },
  "ugrant.amountHint": {
    en: "1–9999. Grants add; they can never subtract.",
    ja: "1〜9999。付与は加算のみで、減算はできません。",
  },
  "ugrant.amountUnique": {
    en: "A club or character is owned or not owned — there is no stacking, so the amount is always 1.",
    ja: "クラブとキャラクターは所有しているかどうかのみで、重複所持はありません。数量は常に 1 です。",
  },
  "ugrant.note": { en: "Note (optional)", ja: "メモ（任意）" },
  "ugrant.notePlaceholder": { en: "support ticket #12", ja: "サポート問い合わせ #12" },
  "ugrant.deliveryHint": {
    en: "The player receives this on their next launch, not immediately — grants drain at boot. Recorded in the audit log with your email.",
    ja: "付与は起動時にまとめて受け取られるため、プレイヤーに届くのは次回起動時です。あなたのメールアドレスとともに監査ログに記録されます。",
  },
  "ugrant.confirm": { en: "Queue grant", ja: "付与を登録" },

  // sh.notice.* — INFORMATION now, not a warning (shop_server_purchase §4). The
  // price became authoritative on {build}: a purchase is one POST /shop/purchase
  // that reads the PUBLISHED row, prices it off the SERVER clock and debits +
  // queues the grant in one transaction. It is still worth saying out loud that
  // enforcement is only as good as the OLDEST build in the wild — an installed
  // older client keeps debiting locally at its bundled price until the legacy
  // /points/spend shop reason is closed (§2.6). Do not drop that second sentence.
  "sh.notice.headline": {
    en: "Prices are enforced by the server for builds {build} and later.",
    ja: "ビルド {build} 以降では価格をサーバーが強制します。",
  },
  // The PENDING half of the banner: the constant in lib/buildGates.ts is still
  // 0, so no build carrying the strict client half has been uploaded, validator
  // rule G1 refuses every character/item row, and promising an enforcement
  // build number here would be promising a build that does not exist.
  "sh.notice.pendingHeadline": {
    en: "Server pricing is live; the client build is pending upload — character and item rows cannot be published yet.",
    ja: "サーバー価格は有効です。ただしクライアントビルドが未アップロードのため、キャラクター行とアイテム行はまだ公開できません。",
  },
  "sh.notice.pendingBody": {
    en: "Purchases go through /shop/purchase, which charges the PUBLISHED price at purchase time (server clock, listing + sale windows). Club and ball rows publish normally. Character and item rows need the build that parses those categories strictly: upload it, then set SHOP_CATEGORY_STRICT_BUILD in lib/buildGates.ts from Docs/Versioning/last_uploaded_build.txt and redeploy this dashboard.",
    ja: "購入は /shop/purchase を経由し、購入時点で公開中の価格（サーバー時刻・出品期間とセール期間を適用）を請求します。クラブ行とボール行は通常どおり公開できます。キャラクター行とアイテム行は、それらのカテゴリを厳密に解釈するビルドが必要です。アップロード後、Docs/Versioning/last_uploaded_build.txt の値を lib/buildGates.ts の SHOP_CATEGORY_STRICT_BUILD に設定し、この管理画面を再デプロイしてください。",
  },
  "sh.notice.body": {
    en: "Purchases on build {build} and later go through /shop/purchase, which charges the PUBLISHED price at purchase time (server clock, listing + sale windows) and queues the item as a grant in the same transaction. Older builds still debit locally at their bundled price until the legacy spend path is closed.",
    ja: "ビルド {build} 以降の購入は /shop/purchase を経由し、購入時点で公開中の価格（サーバー時刻・出品期間とセール期間を適用）を請求したうえで、同一トランザクションでアイテムを付与キューに登録します。それ以前のビルドは、旧来の消費経路が閉じられるまでバンドル価格でローカルに消費し続けます。",
  },
  "sh.col.entry": { en: "Entry", ja: "出品" },
  "sh.col.refers": { en: "Refers to", ja: "参照先" },
  "sh.col.price": { en: "Price", ja: "価格" },
  "sh.col.order": { en: "Order", ja: "並び順" },
  "sh.col.flags": { en: "Flags", ja: "フラグ" },
  "sh.category": { en: "Category", ja: "カテゴリ" },
  "sh.refId": { en: "Referenced row", ja: "参照する行" },
  "sh.refId.search": { en: "Type to search the {catalog} catalog…", ja: "{catalog} カタログを検索…" },
  "sh.refId.searching": { en: "Searching…", ja: "検索中…" },
  "sh.refId.none": { en: "No active rows match.", ja: "一致する有効な行はありません。" },
  "sh.refId.activeOnly": {
    en: "Only ACTIVE rows are offered — listing a deactivated row is the most likely way a shop edit produces a broken card.",
    ja: "有効な行のみを候補として表示します。無効な行を出品することが、ショップの表示崩れを招く最も多い原因です。",
  },
  "sh.preview.title": { en: "Resolved reference", ja: "参照先の解決結果" },
  "sh.preview.unresolved": {
    en: "\"{refId}\" does not exist in the {catalog} catalog. Publish will block on it.",
    ja: "「{refId}」は {catalog} カタログに存在しません。公開時にブロックされます。",
  },
  "sh.preview.inactive": {
    en: "\"{refId}\" exists but is DEACTIVATED. Publish will block on it.",
    ja: "「{refId}」は存在しますが無効化されています。公開時にブロックされます。",
  },
  "sh.preview.artRef": { en: "Sprite", ja: "スプライト" },
  "sh.preview.noArtUrl": {
    en: "The catalogs store a Unity sprite NAME, not an image URL — the game resolves it with Resources.Load. There is no image to fetch here; art-URL columns are out of scope for this task.",
    ja: "カタログが保持しているのは画像 URL ではなく Unity のスプライト「名」で、ゲーム側が Resources.Load で解決します。ここで取得できる画像は存在しません（画像 URL 列は本タスクの対象外）。",
  },
  "sh.sale": { en: "SALE", ja: "セール" },
  "sh.state.hint": {
    en: "Derived from startAt / endAt (endAt is EXCLUSIVE). A row with no window falls back to its active switch.",
    ja: "startAt / endAt から判定します（endAt は「その時刻を含まない」）。期間が未設定の行は有効フラグに従います。",
  },
  "sh.state.brokenHint": {
    en: "This row's schedule window could not be read, so it is treated as NOT live. Fix startAt / endAt — an unreadable bound must never mean 'show it forever'.",
    ja: "この行のスケジュール期間を読み取れなかったため、配信対象外として扱っています。startAt / endAt を修正してください。読み取れない値が「無期限に表示」を意味してはならないためです。",
  },
  "sh.windows.title": { en: "Scheduling", ja: "スケジュール" },
  "sh.windows.help": {
    en: "startAt / endAt set the listing window and saleStartAt / saleEndAt the sale window, as ISO-8601 UTC (2026-09-01T00:00:00Z). endAt is EXCLUSIVE. Empty means no bound. The sale window must sit inside the listing window, and publish blocks on an unreadable or inverted one.",
    ja: "startAt / endAt で出品期間を、saleStartAt / saleEndAt でセール期間を指定します（ISO-8601 UTC 形式、例: 2026-09-01T00:00:00Z）。endAt はその時刻を含みません。空欄は「期限なし」です。セール期間は出品期間の内側に収める必要があり、読み取れない値や前後が逆の場合は公開時にブロックされます。",
  },
} as const satisfies Record<string, Entry>;

export type DictKey = keyof typeof DICT;

export function translate(
  key: DictKey,
  lang: Lang,
  vars?: Record<string, string | number>
): string {
  const entry = DICT[key] as Entry | undefined;
  if (!entry) return key;
  // Fall back to English rather than the raw key: a missing Japanese string
  // should read as untranslated, not as a broken UI.
  let out = (lang === "ja" ? entry.ja : entry.en) || entry.en;
  if (vars) {
    for (const [k, v] of Object.entries(vars)) {
      out = out.split(`{${k}}`).join(String(v));
    }
  }
  return out;
}
