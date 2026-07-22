# Golfin アカウント — サーバー設定チェックリスト（Ken さんへ）

Ken さん、お世話になっております 👋
Golfin ゲームに「ログイン / 新規登録 / ユーザー名作成 / メール確認」の画面ができました。
現在はサーバーに接続しない **「練習（モック）モード」** で動いていて、本物のサーバーに触れずにゲーム
制作を進められる状態です。これらの画面を **本物の PLAYLIFE / Supabase アカウントシステム** に接続する
ために、サーバー側でいくつか設定が必要です。以下に、必要な作業を分かりやすい手順でまとめました。
**ゲームのコードを触る必要は一切ありません** — すべて **Supabase の管理画面**（後半では Google / Apple の
コンソール）での作業です。

各セクションが終わったら、そこで得られた値を **Cesar に送ってください**（各所に「➡️ Cesar に送るもの」と
記載しています）。

対象の Supabase プロジェクト：
- **プロジェクト:** PLAYLIFE（ref `wmszyghwwkaptgqdunel`）
- **ダッシュボード:** https://supabase.com/dashboard/project/wmszyghwwkaptgqdunel

---

## ステップ 0 — Cesar に管理者権限を付与（最初にお願いします）

万一のときに Cesar がバックエンドを手伝ったり、引き継いだりできるよう、プロジェクトに Cesar を
**管理者（admin / owner）** として追加してください。ゲームとは別の作業で、1 分ほどで終わります。

**Supabase:**
1. 上記のダッシュボードリンクを開く。
2. 左サイドバー → **Project Settings（歯車アイコン）** → **Team**（または **Members**）。
3. **Invite** / **Add member** をクリック。
4. **Cesar のメールアドレス: cesar.guarinoni@wonderwall-g.com** を入力。
5. 役割（role）を **Owner**（Owner が無ければ **Admin**）に設定。
6. 招待を送信。

**Fly.io（PLAYLIFE API サーバー。Ken さんが管理している場合のみ）:**
1. https://fly.io/dashboard → **`playlife-api`** アプリ → **Organization** → **Members**。
2. **cesar.guarinoni@wonderwall-g.com** を **Admin** として招待。

➡️ **Cesar に送るもの:** 「Supabase（と Fly.io）に admin として招待しました」の連絡。Cesar が招待メールを
承認します。

---

## フェーズ A — メールアドレス + パスワードでの本番稼働（まずはこれ）

四つの画面を練習モードから本物のサーバーに切り替えるのに必要なのは、これだけです。

### A1. 「anon」キーを送ってください（約 2 分）
これはアプリが Supabase と通信するための **公開（public）キー** です（アプリに入れても安全なキーで、
スマホ版 PLAYLIFE アプリでも既に使われています）。

1. 上記ダッシュボードを開く。
2. 左サイドバー → 一番下の **Project Settings（歯車アイコン）**。
3. **API** をクリック。
4. **Project API keys** の中の **`anon` `public`** の行を探す。
5. その行の **Copy** をクリック。

➡️ **Cesar に送るもの:** コピーした **`anon public`** キー。
（**`service_role` キーは送らないでください** — こちらは秘密のキーで、ゲームに入れてはいけません。）

### A2. メール登録＋確認を有効にする（約 3 分）
1. 左サイドバー → **Authentication**。
2. **Providers**（または **Sign In / Providers**）をクリック。
3. 一覧の **Email** が **Enabled（有効）** になっていることを確認（トグルをオン）。
4. Email プロバイダーの設定を開き、以下を確認：
   - **Confirm email** = **ON**（ユーザーはログイン前にメール内のリンクをクリックして確認する必要が
     あります。これがゲームの「メール確認」画面です）。
   - **Enable Sign-ups** = **ON**（オフだと新規プレイヤーが登録できません）。
5. **Save** をクリック。

➡️ **Cesar に送るもの:** 「Email と Confirm email をオンにしました」の一言。

### A3. ウェブサイト / リダイレクト先アドレスを設定（約 3 分）
ユーザーがメール内の確認リンクをクリックしたとき、Supabase はどこへ遷移させるかを知る必要があります。

1. **Authentication** → **URL Configuration**。
2. **Site URL:** `https://playlife-app.web.app/` に設定（スマホ版アプリと同じ、既存の PLAYLIFE ウェブ
   アドレスです）。
3. **Redirect URLs:** 許可リストに `https://playlife-app.web.app/*` が入っていることを確認。
   （Google / Apple ログイン用に、後でゲーム自身のアドレスもここに追加します — フェーズ B）。
4. **Save** をクリック。

➡️ **Cesar に送るもの:** 「URL 設定を保存しました」の連絡。

### A4.（任意・推奨）確認メールの文面をチェック（約 5 分）
1. **Authentication** → **Email Templates**。
2. **Confirm signup** を開く。
3. 件名・本文が Golfin 向けとして問題ないか確認（不安であればデフォルトのままで問題なく動きます）。
   本文中の `{{ .ConfirmationURL }}` は削除しないでください — これがユーザーがクリックするリンクです。

➡️ **Cesar に送るもの:** 「メールテンプレートを確認しました」（変更した場合はその文面）。

### ✅ フェーズ A の完了後
Cesar が **anon キー（A1）** を受け取り、A2〜A3 が完了すれば、あとはゲーム側でスイッチを一つ切り替える
だけで、本物の 登録 / ログイン / メール確認 が動き出します。**この切り替えにアプリのストア公開は不要
です** — 設定変更だけで済みます。

---

## フェーズ B — Google と Apple の「〜でサインイン」ボタン（後日、準備が整ってから）

ゲームには既に **Login with Google** と **Login with Apple** のボタンがありますが、今は「近日対応」と
表示されます。これらを動かすには、Google と Apple 側でアカウント設定が必要です。作業はやや複雑で、
Google / Apple 側は開発者の協力が必要になることが多いです。**Cesar の指示があるまでは着手しないで
ください。** 計画のために必要事項だけ記載します：

### B1. Google サインイン
1. **Google Cloud Console**（https://console.cloud.google.com）の PLAYLIFE プロジェクトで、
   **OAuth 2.0 クライアント ID**（種類：ウェブアプリケーション）を作成。
2. **承認済みのリダイレクト URI** に以下を追加：
   `https://wmszyghwwkaptgqdunel.supabase.co/auth/v1/callback`
3. **クライアント ID** と **クライアントシークレット** をコピー。
4. Supabase で：**Authentication → Providers → Google** → ID とシークレットを貼り付け → **Enable** → **Save**。

➡️ **Cesar に送るもの:** 「Google プロバイダーを有効化しました」（シークレットは Supabase 内に留めるので
Cesar には不要です）。

### B2. Apple サインイン
1. **Apple Developer**（https://developer.apple.com）の Golfin アプリ用に、**Services ID** と
   **Sign in with Apple 用のキー** を作成。
2. 同じリダイレクトを追加：`https://wmszyghwwkaptgqdunel.supabase.co/auth/v1/callback`
3. Supabase で：**Authentication → Providers → Apple** → Services ID / Team ID / Key を入力 →
   **Enable** → **Save**。

➡️ **Cesar に送るもの:** 「Apple プロバイダーを有効化しました」の連絡。

### B3. ゲームへの戻り先アドレス（Cesar と開発者から提供します）
Google / Apple サインイン後にスマホがゲームへ戻るために、追加の **リダイレクト URL**（`golfin://auth-callback`
のような「ディープリンク」）を一つお渡しします。それを **Authentication → URL Configuration →
Redirect URLs** に追加してください。ゲーム側の準備ができ次第、Cesar が正確な文字列を送ります。

---

## 送らないでほしいもの / やらないでほしいこと
- ❌ **`service_role`** キーやパスワードは絶対に送らないでください — これらは秘密情報です。ゲームに渡すのは
  **`anon public`** キーだけです。
- ❌ ゲームのコード、Unity、GitHub を編集する必要はありません — すべて Supabase / Google / Apple の
  管理画面での作業です。

## フェーズ A のために Cesar が待っているもの（まとめ）
1. **`anon public`** キー（手順 A1）。← これが一番のカギです
2. **Email と Confirm email がオン**（A2）、**URL が設定済み**（A3）であることの確認。

メール / パスワードでの本番稼働に必要なのは以上です。Ken さん、よろしくお願いします 🙏
