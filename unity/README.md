# NFWL Unity版（Phase 1 プロトタイプ）

Unreal版（リポジトリ直下の Sprint01 コード）と同じ内容を Unity 6 で動くようにしたものです。
Unreal版のファイルには手を加えていません。

- Unityプロジェクトの場所：`unity/NFWL/`
- 対応バージョン：Unity 6（6.3 LTS を推奨。6000.x ならどれでも開けます）
- はじめての方は **[Mac で開いて遊ぶまでの手順書](GUIDE_Mac_ja.md)** を読んでください。

## できること（Phase 1）

| 操作 | キー |
| --- | --- |
| 移動・ドリブル | W A S D（または矢印キー） |
| ダッシュ | Shift |
| ショートパス | J（長押しで強く） |
| シュート | K（長押しでチャージ、強すぎると浮く） |
| ロングパス | L（長押しで強く・高く） |
| キックオフに戻す | R |

ボールに近づくと自動でトラップして、画面左下に **BALL** と出ます（ドリブル中）。
画面右側（+X）のゴールに入れると HOME の得点です。外に出るとセンターから再開します。

## 中身

| ファイル | 役割（Unreal版の対応ファイル） |
| --- | --- |
| `Assets/Scripts/NFWLRules.cs` | ピッチ寸法・能力値換算・長押し判定・ゴール判定（NFWLTypes.h） |
| `Assets/Scripts/Pitch.cs` | 42m×25mピッチ、ライン、ゴールとネットを実行時に生成（NFWLPitch） |
| `Assets/Scripts/Ball.cs` | 4号球（半径11cm・0.40kg）、反発・摩擦・減衰、高速でもすり抜けない設定（NFWLBall） |
| `Assets/Scripts/PlayerController.cs` | 移動・ダッシュ・ドリブル・パス・シュート（NFWLPlayerCharacter） |
| `Assets/Scripts/MatchCamera.cs` | 斜め俯瞰のサイドビューカメラ |
| `Assets/Scripts/MatchManager.cs` | 試合進行・得点・アウト判定・スコア表示（GameMode / GameState / HUD） |
| `Assets/Scripts/Editor/NFWLProjectSetup.cs` | 初回に `Assets/Scenes/Main.unity` を自動作成して開く |

座標は Unity の流儀（Y が上、メートル単位）。ピッチの長さ方向が X、幅方向が Z です。

## 未確認・今後

- この環境には Unity を入れられなかったため、**Unity上での動作確認はまだ**です（C#の構文チェックのみ実施）。初回に開いたとき赤いエラーが出たら、その文面を送ってください。
- 選手はカプセルの仮モデル、アニメーションなし。
- ゲームパッド、味方・相手AI、GK、試合時間は Phase 2 で追加します。
