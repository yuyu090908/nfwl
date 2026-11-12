# NFT FOOTBALL WORLD LEAGUE（NFWL）

UE5で作る5対5のリアル3Dオンラインサッカー。設計の正本は `design/NFWL_Master_Document_v2.1.md`（設計書）と `design/NFWL_Full_Specification_v1.0.md`（全体仕様書）。

現在：**Phase 1（3D Football Prototype）**。進め方は [docs/ROADMAP.md](docs/ROADMAP.md)、スプリント記録は [docs/sprints/](docs/sprints/)。

## 必要なもの

| 項目 | 内容 |
| --- | --- |
| Unreal Engine | 5.6 系を想定（他の5.x でも可。開くときに版を選び直せばよい） |
| Mac | Xcode（App Store 版）と Command Line Tools |
| Windows | Visual Studio 2022（「C++によるゲーム開発」ワークロード） |
| Git | Git LFS（`git lfs install`） |

## はじめて開くとき

1. `NFWL.uproject` をダブルクリック（Mac は Epic Games Launcher から UE5 を一度起動しておく）。
2. 「モジュールが見つからない／ビルドしますか」と聞かれたら **Yes**。C++ がビルドされエディタが開く。
   - 失敗したら Mac は `NFWL.uproject` を右クリック → 「Generate Xcode Project」、Xcode で `NFWLEditor` をビルド。
3. エディタで **File → New Level → Basic** を作り、アウトライナーの **Floor を削除**して `Content/Maps/PitchTest` に保存。
4. **Edit → Project Settings → Maps & Modes** で Editor Startup Map / Game Default Map を `PitchTest` にする（GameMode は設定ファイルで `NFWLGameMode` 指定済み）。
5. **Play**。ピッチ・ゴール・ボールは実行時に自動生成される。

## 操作（Phase 1）

| 操作 | ゲームパッド | キーボード |
| --- | --- | --- |
| 移動・ドリブル | 左スティック | WASD |
| ダッシュ | R1 | Shift |
| ショートパス | 下ボタン（長押しで強さ） | J |
| シュート | 右ボタン（長押しでチャージ） | K |
| ロングパス | 左ボタン | L |

ボールに近づくと自動でトラップし、ドリブル状態（画面左下に BALL）になる。ゴールの右側（+X）に入れると HOME の得点。

## テスト

エディタの **Tools → Session Frontend → Automation** で `NFWL` を絞り込んで実行（能力値の換算・長押し判定・ゴールライン判定）。

## フォルダ構成

```
Source/NFWL/
  Public|Private/Core    NFWLTypes（寸法・ルール・能力値）, GameMode, GameState
  Public|Private/Ball    ボール（物理・サーバー権威）
  Public|Private/Player  選手（移動・ドリブル・パス・シュート・カメラ）
  Public|Private/Pitch   42m×25mピッチ・ライン・ゴールの自動生成
  Public|Private/UI      HUD（スコア・パワーゲージ）
  Private/Tests          自動テスト
Config/                  GameMode・Enhanced Input・物理サブステップ
docs/                    ロードマップ・スプリント記録
```
