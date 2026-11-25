# NFWL Unity v0.7 Real Football Motion & Photoreal Stadium（変更内容と報告）

v0.6（PR #4、ブランチ `claude/project-thread-3iyzor`）の上に積み上げています。5対5・ピッチ 42×25 m・ボール 0.40 kg / 直径 0.22 m（半径 0.11 m）・操作キー（WASD, Shift, J, K, L, Q, Z, R）はそのままです。

**最初に大事なこと：この環境では Unity を動かせません。** C# のコンパイルと、Unity に依存しない計算部分のテスト（13件）はこちらで実行して通しましたが、シェーダー、実際の見た目、PlayMode テスト、WebGL ビルド、FPS は一つも確認できていません。Mac で開いて確認していただく必要があります（最後の「確認手順」）。

---

## 1. 方針（PHASE 0 の結論）

- **描画は Built-in Render Pipeline のまま**にしました。URP へ移すとシーン・マテリアル・カメラ設定の作り直しが必要で、v0.6 の動作を壊す危険が大きいためです。代わりに自作シェーダー（PBR・草・水晶・ネット・空・群衆・ポスト処理）で URP 相当の見た目を目指しています。URP 移行の手順は 11 章にまとめました。HDRP は WebGL で使えないので使っていません。
- **大きな変更はすべて機能フラグ付き**です（F3 画面でオン／オフ、Play し直すと反映）。シェーダーが使えない環境では自動で v0.6 の見た目に戻ります。
- 他のゲームの素材・コードは一切使っていません。モデル・テクスチャ・スタジアムはすべて実行時にコードで生成しています。存在しない素材を「入れた」とは書いていません。

## 2. 変更・追加したファイル

### 新規
| ファイル | 主なクラス | 役割 |
|---|---|---|
| `Resources/Shaders/NFWLLit.shader` | NFWL/Lit | PBR（金属度・滑らかさ・法線・頂点AO・発光・布の光沢） |
| `Resources/Shaders/NFWLCrystal.shader` | NFWL/Crystal | 水晶部分（縁の光・脈動・きらめき、レア度で変化） |
| `Resources/Shaders/NFWLGrass.shader` / `.cginc` | NFWL/Grass | 芝（刈り目の縞、刈り方向による明暗、すり減り、近くだけ芝の凹凸） |
| `Resources/Shaders/NFWLGrassShell.shader` | NFWL/GrassShell | 芝の葉（重ね描き、22 m で消える） |
| `Resources/Shaders/NFWLNet.shader` | NFWL/Net | ゴールネットの網目（影も落ちる） |
| `Resources/Shaders/NFWLSky.shader` | NFWL/NightSky | 夜空（星・地平線の明るみ） |
| `Resources/Shaders/NFWLCrowd.shader` | NFWL/Crowd | 観客の動き（GPU で腕・旗を振る、ウェーブ） |
| `Resources/Shaders/NFWLAdditive.shader` | NFWL/Additive | 光の筋・LED・ハロー |
| `Resources/Shaders/NFWLBlob.shader` | NFWL/ContactShadow | 足元の接地影 |
| `Resources/Shaders/NFWLPost.shader` | Hidden/NFWL/Post | ブルーム・SSAO・色調（ACES）・ビネット |
| `Scripts/Core/NFWLFeatures.cs` | NFWLFeatures | 機能フラグ7個 |
| `Scripts/Rendering/GraphicsPreset.cs` | GraphicsPreset, QualityLevel | Low/Medium/High/Ultra の設定表 |
| `Scripts/Rendering/GraphicsQuality.cs` | GraphicsQuality | 画質の適用・保存・自動で下げる処理 |
| `Scripts/Rendering/NFWLShaders.cs` | NFWLShaders | シェーダー読み込みとマテリアル作成（失敗時は null → 旧マテリアル） |
| `Scripts/Rendering/ProceduralTextures.cs` | ProceduralTextures | 芝・布（編み目・しわ・縫い目）・革・コンクリートの法線マップ生成 |
| `Scripts/Rendering/CrystalLook.cs` | CrystalLook | レア度ごとの水晶の見た目 |
| `Scripts/Rendering/FloodlightMath.cs` | FloodlightMath | 照明塔の位置・色温度・向きの計算 |
| `Scripts/Rendering/StadiumLighting.cs` | StadiumLighting | ナイター照明一式 |
| `Scripts/Rendering/NFWLPostProcess.cs` | NFWLPostProcess | カメラのポスト処理・描画解像度 |
| `Scripts/Character/BodyMeshes.cs` / `BodyProfile.cs` / `CrystaliaProfiles.cs` | BodyMeshes, CrystaliaProfiles | 人体の断面データから滑らかな体のメッシュを作る |
| `Scripts/Character/PlayerModel.Anatomy.cs` | PlayerModel（partial） | 新しい選手モデルの組み立てと接地影 |
| `Scripts/Stadium/StadiumBuilder.cs` | StadiumBuilder | スタンド・屋根・照明塔・LED・トンネル・ベンチ・街並み |
| `Scripts/Stadium/CrowdBuilder.cs` | CrowdBuilder | 観客（近い列は立体、遠い列は板） |
| `Scripts/Stadium/GoalNet.cs` / `NetSpring.cs` | GoalNet, NetSpring | ボールが当たると膨らんで戻るネット |
| `Scripts/Stadium/GrassShells.cs` | GrassShells | 芝の葉の層 |
| `Scripts/Presentation/MatchAtmosphere.cs` | MatchAtmosphere | 観客の盛り上がり・ゴール時の LED・ボールの影 |
| `Scripts/Presentation/GraphicsSettingsPanel.cs` | GraphicsSettingsPanel | F3 の画質設定画面 |
| `Scripts/Animation/MotionProfile.cs` | MotionProfile | 選手ごとの走り方の個性 |
| `Scripts/Animation/MotionTrajectory.cs` | MotionTrajectory, TrajectoryPoint | 少し先の位置・向きの予測 |
| `Scripts/Animation/MotionInterfaces.cs` | IPoseDriver, MotionQuery, MotionEvents | 将来のモーションマッチング用の入口 |
| `Scripts/Ball/ShotTrajectory.cs` | ShotTrajectories | シュートの弾道の分類（5種） |
| `Tests/EditMode/V07Tests.cs` | V07Tests | v0.7 のテスト13件 |

### 変更
`Ball.cs`, `MatchCamera.cs`, `MatchManager.cs`, `Pitch.cs`, `PlayerController.cs`, `PlayerModel.cs`, `Visuals.cs`, `Animation/GaitController.cs`, `Player/KickController.cs`, `Player/DribbleController.cs`, `README.md`, `GUIDE_Mac_ja.md`

## 3. 動き（優先度 A：人間らしい走りと足の接地）

v0.6 の慣性・減速・旋回・歩幅と足の固定（IK）を土台に、次を足しました。

- **選手ごとの個性**：歩数のテンポ、腕の振り、前傾、曲がるときの体の傾き、タッチのリズムが選手ごとに少しずつ違います（背番号と身長から決まるので毎回同じ）。テストで「全員が自然な範囲に収まる」ことを確認しています。
- **よろめき**：体勢が崩れると上体が前に倒れ、腕が外に出てバランスを取ります。
- **キックの段階を細かく**：助走 → 軸足を置く → 振り上げ → 腰の回転 → 膝の伸び → インパクト → フォロースルー → 戻り。腰が先に回り、あとから脚が振られる順番にしました。
- **予測と将来の入口**：少し先の位置・向きを予測する計算と、モーションマッチングに差し替えるためのインターフェース（`IPoseDriver`）を用意しました。指定の役割名（PlayerInputController…PlayerVisualController）と現在のクラスの対応は `MotionInterfaces.cs` の冒頭に書いてあります。

## 4. ボール（優先度 B：足で扱う）

- ボールの見た目を実寸（直径 22 cm）にしました。見えにくい場合は F3 で v0.6 の大きめ表示（1.6倍）に戻せます。物理の大きさ・重さは変えていません。
- 革の質感の PBR ボール、ボールの真下の影（高さで大きさ・濃さが変わる）。
- シュートの弾道を「グラウンダー／低く速い／浮き上がる／曲がる／浮き球」に分類して、シュート表示に出します。
- ネットに当たった位置と速さを通知し、ゴールネットが膨らみます。

## 5. 光（優先度 C：ナイターの光と影）

- 四隅の照明塔のうち1本を影を落とす主光源にし、残り3本は影なしの補助光（画質で切り替え）。選手の向きと光の方向で明暗がはっきり出ます。
- 照明の色温度（暖色寄り・寒色寄り）を塔ごとに変え、空・観客席からの環境光、霧、夜空、反射（試合開始直後に1回だけ撮影）。
- ポスト処理：ブルーム（照明・LED が光る）、SSAO（足元や体の接する所が暗くなる）、ACES の色調、色味の調整、ビネット。
- 足元の接地影（両足と体）。

## 6. スタジアム

- メインスタンド（12段＋通路＋上段5段＋選手入場トンネル）、ゴール裏2面（9段）、向かい側（3段、屋根なし）。
- 階段・手すり・出入口・壁・屋根（片持ち、骨組み、光の帯）、LED 広告板（点の模様、ゴールで光る）、照明塔（格子の塔・ライト面・光のにじみ・光の筋）、ベンチ、遠くの街並み。
- 観客：近い列は立体の人、遠い列は板。GPU で腕や旗を振り、チャンスやゴールで盛り上がりウェーブが出ます。
- 芝：刈り目の縞、刈り方向の明暗、ゴール前のすり減り、近くの芝の葉。ラインを少し太く。
- 静的な物はまとめて描画（Static Batching）して負荷を下げています。

## 7. カメラ

Z キーで 中継（16 m）→ ワイド（24 m）→ 戦術（上から、30 m）→ 寄り（11 m）を切り替えます。v0.6 の 11 / 16 / 24 m はそのまま含まれています。高いボールのときは少し引きます。

## 8. 画質設定（F3）

| | Low | Medium（WebGL の初期値） | High（PC の初期値） | Ultra |
|---|---|---|---|---|
| 影 | 硬い影・30 m | 45 m | 60 m・高解像度 | 75 m・最高 |
| 補助光 | 頂点 | 1灯 | 2灯 | 3灯 |
| MSAA | なし | 2x | 4x | 8x |
| ポスト処理 | なし | ブルーム | ＋SSAO | ＋SSAO（高精度） |
| 立体の観客 | なし（板のみ） | 3列 | 5列 | 8列 |
| 芝の葉 | なし | なし | 2層 | 4層 |
| 描画解像度 | 80% | 100% | 100% | 100% |

「自動調整」をオンにすると、45 FPS を4秒続けて下回ったら1段下げます。

## 9. シーンの変更

シーンファイルは変えていません。v0.6 と同じく、`MatchManager` が起動時にピッチ・スタジアム・照明・カメラを組み立てます。ProjectSettings も変えていません。

## 10. ビルド・FPS・テストの結果

| 項目 | 結果 |
|---|---|
| C# コンパイル（Editor 以外すべて） | ✅ エラーなし（Unity 2021 の参照 DLL ＋ Unity 6 の名前の置き換えで確認） |
| v0.7 テスト 13件（`V07Tests`） | ✅ 13件すべて合格（Unity の数学部分を自作の代用品に置き換えて Mono で実行） |
| シェーダーのコンパイル | ❌ 未確認（Unity が必要） |
| Editor スクリプト | ❌ 未確認 |
| v0.6 の EditMode / PlayMode テスト | ❌ 未実行（Unity が必要） |
| WebGL ビルド | ❌ 未実行 |
| FPS | ❌ 未測定（F2 の表示で測ってください） |

## 11. まだ作っていないもの

- 本物のモーションキャプチャとモーションマッチング（入口だけ用意。データがないため）
- Humanoid の人体モデルと Animator（今は手続き型の 15 関節）
- URP への移行。手順案：① URP パッケージ導入と Pipeline Asset を Low〜Ultra で4つ作る ② `NFWL/*` シェーダーを Shader Graph か URP 用 HLSL に書き直す ③ `NFWLPostProcess` を URP の Volume（Bloom, SSAO, Tonemapping）に置き換え ④ WebGL で FPS を比べて決める
- 観客の声・実況などの録音音声
- 試合の天候・時間帯の切り替え

## 12. 必要な素材（入れれば質が上がるもの。今は入っていません）

- Humanoid リグ付きの選手モデル（FBX、自作か商用利用可のもの）
- 走り・止まり・旋回・キック・トラップのモーションキャプチャ（モーションマッチング用には数分〜数十分）
- スタジアムの観客の歓声・チャントの録音、ボールを蹴る音
- 芝・コンクリート・座席の写真ベースのテクスチャ（PBR 一式）
- 夜のスタジアム周辺の HDRI（反射・環境光用）

## 13. 残っている問題・リスク

- **シェーダーが一度もコンパイルされていません。** 失敗したものは自動で v0.6 の見た目に戻る作りですが、見た目の崩れはありえます。Console にシェーダーのエラーが出たら知らせてください。
- 描画解像度を 100% 未満にする処理（Low）は、環境によって画面が黒くなる可能性があります。その場合は Medium 以上にしてください。
- 観客・スタジアム・芝の葉の負荷は未測定です。WebGL で重い場合は Medium か Low にしてください。
- 実寸のボールは中継カメラから小さく見えます。見えにくければ F3 で大きめ表示に戻せます。

## 確認手順（Mac）

1. ブランチ `claude/project-thread-r5i86x` を開いて Unity で Play。Console に赤いエラーがないか確認。
2. 夜のスタジアム・照明塔・観客・影が出ているか、Z でカメラが4種類切り替わるか確認。
3. F3 で Low〜Ultra を切り替え、F2 で FPS を見る。
4. Window → General → Test Runner で EditMode と PlayMode を実行。
5. File → Build Settings → WebGL でビルドし、ブラウザで FPS を確認。
