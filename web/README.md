# NFWL World League — 公式Webサイト（`web/`）

Unity 5v5 試合（`unity/NFWL`）を組み込んだ、クラブ管理・リーグ・ランキング・成長・予想のWebプラットフォームです。

- Next.js 16（App Router）/ TypeScript / Tailwind CSS 4 / Radix（shadcn 風コンポーネント）/ Motion
- PostgreSQL + Prisma 7 / Cookie セッション（HttpOnly）/ SSE によるリアルタイム順位表
- Unity WebGL 埋め込み + 双方向ブリッジ（`src/lib/bridge`、C# 側は `unity/NFWL/Assets/Scripts/Web/WebBridge.cs`）

## いまの状態（正直な一覧）

| 機能 | 状態 |
| --- | --- |
| メール登録・ログイン・ログアウト | 動作確認済み（E2E） |
| Google / Discord ログイン | 実装済み・**未確認**（クライアントIDが未設定のためボタン非表示） |
| ウォレットログイン（SIWE, EVM） | 実装済み・**実ウォレットで未確認** |
| クラブ作成・スターター8人配布・500 NFWL Points | 動作確認済み（結合テスト・E2E） |
| 編成（2-1-1、ドラッグ／タップ／キーボード、ポジション補正） | 動作確認済み |
| AI試合 → 結果保存 → 試合履歴 → 再ログイン後も保持 | **DEV SIMULATOR で**動作確認済み。実際の Unity WebGL ビルドでは未確認 |
| 試合結果のサーバー検証（速すぎる試合・スコア不整合などを拒否） | 動作確認済み（単体・結合テスト） |
| リーグ（サーバーシミュレーション）・昇降格・順位表 | 実装済み。Cron 実行は本番未確認 |
| レベル上限解放（ポイント消費・冪等） | 動作確認済み（結合テスト） |
| 予想（Free Pick、換金不可ポイントのみ） | 実装済み |
| マーケット・オークション | **画面とデータのみ。売買APIは常に 503（無効）** |
| 実価値トークンの賭け・NFT・オンチェーン処理 | **無効**（チェーン・契約・法務確認前のため） |

デモデータは画面上で必ず「DEMO」と表示されます。

## ローカルで動かす

```bash
cd web
cp .env.example .env            # DATABASE_URL を設定
npm install                     # prisma generate も実行されます
npm run db:migrate:dev          # テーブル作成
npm run db:seed                 # ディビジョン・スキル・スターター候補30人・ベータシーズン
npm run dev                     # http://localhost:3000
```

Unity ビルドがなくても試合の連携を確認できます：`.env` に `NFWL_ENABLE_BRIDGE_SIMULATOR=1`（と短い試合用に `NFWL_ALLOW_SHORT_MATCHES=1`）を入れると、/play に「DEV SIMULATOR」が出ます。シミュレーターは Unity と同じメッセージを送るだけで、実際の試合ではありません。本番（`NODE_ENV=production`）では両方とも無効です。

## テスト

```bash
npm run typecheck && npm run lint
npm test          # 単体 + DB結合テスト（DATABASE_URL のDBを使用、テスト用ユーザーを作成）
npm run e2e       # Playwright：登録 → クラブ → 編成 → 試合（シミュレーター）→ 履歴 → 再ログイン、スマホ表示
```

## Unity WebGL ビルドを載せる

1. Unity 6（6000.6.9f1）で `unity/NFWL` を開き、メニュー **NFWL > Build WebGL** を実行（Brotli + decompression fallback、出力 `unity/NFWL/Builds/NFWL`）。
2. ローカル：`npm run unity:copy` で `public/unity/Build` にコピー。
3. 本番：`Build` フォルダを S3 + CloudFront などに置き、`NEXT_PUBLIC_UNITY_BUILD_URL` にその URL を設定（CSP にも自動で追加されます）。

Unity に渡すのは試合設定（セッションID・ユーザーID・チーム・選手・フォーメーション・能力値・スキル・見た目）だけです。Cookie・トークン・ウォレット鍵は渡しません。

## 試合結果の扱い

- ブラウザ（WebGL）からの結果は信用しません。サーバーは受信時刻で「実時間より速い試合」「スコアの食い違い」などを検出し、不可能な結果は拒否します。
- 検証を通っても「クライアント報告」扱いです。**公式シーズン（`Season.official = true`）ではブラウザの結果は順位・RPに一切入りません。** 公式順位はサーバーが行うリーグシミュレーション（`/api/cron/league`）だけで決まります。
- ベータシーズン（現在のシード）では CPU 戦を「暫定」として順位表に表示します（RPは動きません）。

## 本番デプロイ（未実施。アカウントと秘密情報が必要です）

- DB：Neon / Supabase などの PostgreSQL → `DATABASE_URL`。初回に `npm run db:migrate && npm run db:seed`。
- ホスティング：Vercel（`web/` をルートに設定）。環境変数は `.env.example` 参照。
- Cron：`vercel.json` に15分ごとのリーグ処理を定義済み。`CRON_SECRET` を設定してください（Hobby プランは1日1回までなので、その場合は間隔を変更）。
- CDN：Unity ビルドを S3/CloudFront 等へ。`.unityweb` は fallback 付きなので特別なヘッダーなしでも動きます。
- レート制限はメモリ内です。複数インスタンスで厳密にするなら Redis（Upstash 等）に置き換えてください。
