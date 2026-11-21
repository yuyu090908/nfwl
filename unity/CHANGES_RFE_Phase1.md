# NFWL v0.6 Real Football Engine：Phase 1（移動・操作感）

v0.6 REAL FOOTBALL EVOLUTION（ブランチ `claude/project-thread-kkc9n8`）の上に作業しています。
画面右上のバージョン表示が `NFWL Unity v0.6 Real Football Engine  Phase 1` なら、このPhaseが動いています。

- 状態：コードはコンパイル確認済み（Unity 2021 の参照ライブラリ＋Unity 6 の名前の違いを置き換えて確認）。移動のテスト27件は Unity の外で同じコードを動かして全て合格。**Unity 実機ではまだ動かしていません。**
- 追加パッケージ・有料アセット：なし

---

## STEP 1：今のアーキテクチャ（解析結果）

- Unity 6000.6.9f1、標準レンダーパイプライン（URP / HDRP なし）。パッケージは Test Framework 1.4.5 と標準モジュールだけ。
- シーンは `Editor/NFWLProjectSetup.cs` が作り、試合の中身（ピッチ・選手・ボール・カメラ）はすべて実行時にコードで生成。
- アニメーションクリップ・Humanoid リグ・Animator・ルートモーションは無し。体は基本図形と結晶メッシュで、関節15個をコードで動かす（手続き的アニメーション）。
- 移動の流れ（人もAIも同じ）：
  `PlayerController.Update`（キー入力 or AI の判断 → moveInput / Sprinting）
  → `PlayerController.Move` → `PlayerLocomotion.Tick`（速度・体の向き・傾き・状態）
  → `CharacterController.Move` → 実際の移動量を速度として戻す（接触で遅くなる）
  → `PlayerModel.LateUpdate` → `GaitController.Tick`（足の接地）→ `LegIK`（膝の位置）
- 移動の値を読んでいる他の部品：TeamAI（到達時間）、KickPlanner（スルーパスの先読み）、DribbleController（タッチの強さ）、TackleController / PlayerContacts（バランス・状態）、MatchManager（確認用の表示）。

## STEP 2：問題点（Phase 1 の対象）

| # | 問題 | 変更前（能力70） | 現実の目安 |
|---|---|---|---|
| 1 | 急ブレーキ | 全力から 0.3秒・約1m で停止（約22 m/s²） | 0.8〜1.2秒・3〜5m（5〜9 m/s²） |
| 2 | 加速が早すぎ・ほぼ一定 | 最高速まで約1.3秒 | 2.2〜3.0秒。最初が強く、だんだん弱まる |
| 3 | 高速での小回り | 横グリップ 13〜20、全力の回転半径 約2.8m | 横加速 5〜10 m/s² |
| 4 | 体の向きと違う方向へ速く走れる | 体が後ろ向きのままダッシュに移れる | 腰を回してから加速する |
| 5 | 出だしの前傾が浅い | 約6度 | 最初の数歩で深く前傾 |
| 6 | キック動作中に押したキーが消える | `PerformKick` が動作中は何もせず、バッファも消していた | — |
| 7 | 減速ステップ・ターン角度の区別が無い | 停止は一瞬、ターンは1種類 | 2〜4歩で減速、角度で踏み込みが違う |
| 8 | AIの止まり方 | 目標の1.5m手前から減速するだけ | ブレーキが長くなると行き過ぎる |

## STEP 3：変更したファイル・追加したファイル

| ファイル | 内容 |
|---|---|
| 新規 `Scripts/Core/LocomotionTuning.cs` | 移動の調整値（Inspector で変更可）。NFWLTuning.cs から分けた |
| 新規 `Scripts/Core/InputBuffer.cs` | 入力バッファ（押したキーを短時間保持）と、直前の移動方向の記憶 |
| `Scripts/Player/PlayerLocomotion.cs` | 移動モデルを作り直し（下記） |
| `Scripts/Core/NFWLTuning.cs` | 移動の値を LocomotionTuning.cs へ移動、入力バッファの時間を追加 |
| `Scripts/Animation/GaitController.cs` | 出だし・減速・ドリブル時の歩調（歩幅と歩数） |
| `Scripts/PlayerModel.cs` | 新しい状態の姿勢（出だし、カーブ、踏ん張りの深さ、減速） |
| `Scripts/PlayerController.cs` | 入力バッファ（キック中・タックル）、AIの止まり方 |
| `Scripts/AI/TeamAI.cs` | 到達時間を新しい移動モデルで計算 |
| `Scripts/Player/KickPlanner.cs` | スルーパスの先読みを新しい加速で計算 |
| `Scripts/MatchManager.cs` | バージョン表示、確認用表示にターン角度と加速度を追加 |
| 新規 `Tests/EditMode/MovementTests.cs` | 移動テスト（下記） |

## STEP 4：Phase 1 で実装した内容

### 慣性移動・加速・減速（PlayerLocomotion.cs）
- **加速**：最初が強く、最高速に近づくほど弱まる（指数型）。最初の一歩の強さ `startAcceleration` と、最高速の95%に届くまでの時間 `timeTo95Speed` から自動で計算。
- **減速**：止まり始めた時の速度で減速度を決めて固定（減速度 = 速度 ÷ 停止時間）。だから「停止時間・停止距離・減速度」が必ず一致する（停止距離 = 速度 × 時間 ÷ 2）。通常走からの停止と全力からの停止で時間が違う。
- Shift を離した時など「走りながら遅くする」ときは弱めの減速（`paceDownDecel`）。
- **Gameplay Assist**：42mの小さいピッチ向け。0 = 現実のタイミング、1 = アーケード寄り（加速・停止が速く、よく曲がる）。初期値 0.25。

### 方向転換
- 移動方向と体の向きは別々。カーブは横グリップ（Agility）で曲がれる量が決まり、角度が大きいほど減速。
- 110度より急な方向転換は「踏ん張る → 元の向きのまま強めにブレーキ → 軸足でピボット → 押し出して再加速」。瞬間的な反転はしない。
- ターンを 45 / 90 / 135 / 180 度に分類（`TurnBand`）。アニメーションと確認用表示が使う。
- **体の向きで速度に上限**：守備の横移動（ジョッキー）以外では、体が向いていない方向へは速く走れない。後退から前進、横移動からダッシュに切り替えると、腰を回してから加速する。

### 姿勢・状態
- 出だしの数歩で最大25度の前傾、最高速では起き上がる。カーブ・切り返しで最大20度内側に傾く。
- 急停止では上体が慣性で前に出てから、後ろに傾いて踏ん張る。
- 新しい状態：`StartStep`（最初の一歩）、`Curve`（カーブ走）。Walk / Jog / Run / Sprint は別の状態として扱い、境目でちらつかないよう余裕（ヒステリシス）を持たせた。
- アニメーション用のパラメータ一式 `LocoAnimParams`（MoveSpeed, MoveDirection, Acceleration, TurnAngle, IsSprinting, HasBall, IsDefending, IsFatigued, Balance）を毎フレーム出す。将来 Animator のブレンドツリーやモーションマッチングにそのまま渡せる。

### 足の接地（GaitController.cs）
- 歩幅は「速度 ÷ 歩調」で決まるので、足の動きと実際の移動距離が常に一致する（ストライドの合わせ込み）。接地中の足は地面に固定（足が滑らない）。
- 出だしは短く速い歩数、減速は細かい歩数（数歩かけて止まる）、ボールを持つと少し歩幅が短い。
- 切り返し中は接地足のつま先を軸に回る（既存の仕組みを継続）。

### 入力バッファ（InputBuffer.cs / PlayerController.cs）
- キックの動作中に J / K / L を押すと、その動作が終わるまで保持して次のキックにする（以前は消えていた）。
- 守備で J を少し早く押した（2.6mより遠い、前のタックルの回復中）場合、0.25秒保持して届いた時にタックル。
- 直前の移動方向を0.3秒記憶（Phase 2 のファーストタッチで使う予定）。

### AI
- 目標に向かう時、ブレーキで止まれる速さに抑えて近づく（行き過ぎない）。
- ボールへの到達時間を「向きを変える → 逆に動いていたら止まる → 加速しながら走る」で計算。
- スルーパスの先読みも、受け手が加速しながら走る距離で計算。

## STEP 6：Inspector で調整できる値（移動）

メニュー **NFWL → Create Tuning File** → `Assets/Resources/NFWLTuning.asset` を選択 → Inspector の **Locomotion**。[40, 99] は能力値40のとき / 99のとき。

| 値 | 初期値 | 意味 |
|---|---|---|
| gameplayAssist | 0.25 | 0 = 現実のタイミング、1 = アーケード |
| assistAccelTime / assistStopTime / assistGrip | 0.55 / 0.6 / 1.5 | Assist 1 のときの倍率 |
| sprintTopSpeed | [7.5, 9.5] m/s | ダッシュの最高速（Speed） |
| runTopSpeed | [3.8, 5.0] m/s | 通常走（Speed） |
| withBallFactor | 0.92 | ボール保持時の最高速の倍率 |
| walkSpeed / jogSpeed / gaitHysteresis | 1.8 / 3.4 / 0.25 | 状態の境目 |
| timeTo95Speed | [3.0, 2.2] 秒 | 停止から最高速の95%まで（Acceleration） |
| startAcceleration | [6.5, 8.5] m/s² | 最初の一歩の加速（Acceleration） |
| pushOffBoost / pushOffSeconds | 1.2 / 0.25 | ピボット後の押し出し |
| stopTimeFromRun | [0.9, 0.5] 秒 | 通常走からの停止（Agility） |
| stopTimeFromSprint | [1.2, 0.8] 秒 | 全力からの停止（Agility） |
| paceDownDecel | 3 m/s² | 走りながら遅くするときの減速 |
| cutBrakeFactor | 1.25 | 切り返しのブレーキの強さ |
| bodyTurnRate | [420, 540] 度/秒 | 体の回転（Agility） |
| turnRateAtSprint | 0.45 | 最高速での回転の倍率 |
| lateralGrip | [6, 10] m/s² | 横グリップ（Agility） |
| sharpTurnAngle / pivotSpeed | 110度 / 1.6 m/s | 踏ん張る角度・ピボットできる速さ |
| cornerSpeedFactor | 0.6 | 急カーブでの速度の倍率 |
| facingLimitAt90 / At180 | 0.35 / 0.15 | 体の向きと違う方向へ走れる速さ（最高速の割合） |
| startLean / sprintLean | 25 / 8 度 | 出だしの前傾 / 最高速の前傾 |
| turnLeanMax / turnLeanPerAccel | 20度 / 2.2 | カーブの傾き |
| brakeLeanBack / brakeJolt | 7度 / 9 | 減速時の後傾 / 上体が前に出る強さ |

入力バッファは **Input** の `kickBusyBufferSeconds`（0.3秒）、`tackleBufferSeconds`（0.25秒）、`moveDirMemorySeconds`（0.3秒）。

※ 古い v0.6 の調整ファイルを作っていた場合、移動の値は名前を変えたので新しい初期値になります（昔の急ブレーキの値が残らないようにするため）。

## STEP 7：変更前と変更後（能力70、Gameplay Assist 0.25 の初期設定）

| 項目 | 変更前 | 変更後 |
|---|---|---|
| ダッシュ最高速 | 6.7 m/s | 8.5 m/s |
| 最高速まで | 約1.3秒 | 2.3秒（Assist 0 なら 2.6秒） |
| 全力からの停止 | 0.3秒・約1m | 0.9秒・3.9m（Assist 0 なら 1.0秒・4.3m） |
| 通常走からの停止 | 約0.2秒 | 0.6秒・1.4m |
| 全力でのいちばん小さい回転半径 | 約2.8m | 約4.9m |
| 180度切り返し（全力から） | 約0.5秒で逆走 | 0.6秒ブレーキ → ピボット → 1.7秒で逆方向70%の速さ |
| 90度ターン（全力から） | — | 速度を約8割保ったままカーブ |
| 出だしの前傾 | 約6度 | 約23度 |
| AI のボール到達時間の誤差（15m） | 距離÷最高速で計算 | 実際の走りとの差 0.03秒 |

フレームレートへの影響：計算は1人あたり毎フレーム数十回の四則演算が増えただけで、新しいメモリ確保（GC）はありません。

## STEP 5：テスト

`Window → General → Test Runner → EditMode → Run All`。新しい `MovementTests` 17種類（値違いを含め27件）：

- 停止から最高速までの時間（能力40 / 70 / 99）、加速が最初に強くだんだん弱まること、Assist で速くなること
- 全力からの停止（時間・距離・減速度が一致、隠れた急停止がない）、通常走からの停止、ボール保持中の停止
- 90度ターン（勢いを保つ、体が飛ばない、傾き20度以内）、180度ターン（踏ん張り → ピボット → 逆走）、ターン角度の分類
- 後退 → 前進、横移動 → ダッシュ（腰を回してから加速）、全力の回転半径
- 出だしの前傾、AI の到達時間・止まれる速さ、入力バッファ

既存のテスト（`Turn180_NoInstantFlip`、PlayMode の足滑り・試合が最後まで進む など）もそのまま残しています。足滑りは PlayMode の `Running_FeetDoNotSlide` で確認します。

## Mac での確認手順

1. ターミナルで：
   ```
   cd （nfwl をクローンしたフォルダ）
   git fetch origin
   git checkout claude/project-thread-3iyzor
   git pull
   ```
2. Unity を閉じて、Unity Hub から `nfwl/unity/NFWL` を開き直す
3. Console にエラー（赤）が出ていないか確認
4. メニュー **NFWL → Open Main Scene** → ▶
5. 画面右上の緑の文字（選手の状態）を見ながら試す：
   - **Shift + D** で走り出す → `StartStep → Accelerate → Sprint`。走り出しで体が前に傾き、最高速まで2秒ほどかかる
   - 走っている途中で**キーを全部離す** → `Decelerate → Stop`。細かい歩数で3〜4m滑るように止まる（ピタッと止まらない）
   - Shift + D で走って **S（下）** を押す → `Curve`。体を内側に傾けて曲がる
   - Shift + D で走って **A（逆）** を押す → `SharpTurn turn 180 → PivotTurn → StartStep`。踏ん張って止まってから回って走り出す
   - 相手がボールを持っている時、近くで J を少し早めに押す → 届いた時にタックルが出る
6. テスト：**Window → General → Test Runner** → EditMode → **Run All**、PlayMode → **Run All**

重い・遅いと感じたら、`NFWLTuning.asset` の **Gameplay Assist** を 0.5 くらいに上げてください（加速・停止・旋回がまとめて軽くなります）。

## STEP 8：未実装・必要な素材・残っている問題

- **Phase 2 以降**：ボール物理（Cd の速度変化・無回転など）、ファーストタッチ・トラップの種類、ドリブル、シュート・パスの種類は手を付けていません。
- **Humanoid リグとアニメーションクリップ**：今の体はコードで動かす簡易モデルなので、本物のモーション（モーションキャプチャ）を使うには人型のキャラクターモデルとアニメーション素材が必要です。候補は Mixamo（無料、Adobe アカウントが必要）や Unity Asset Store のサッカー用モーション集（有料）。導入する場合は事前に確認します。`LocoAnimParams` と GaitController / LegIK は人型リグでも使える形にしてあります。
- **モーションマッチング**：Unity 6 には標準のモーションマッチングがありません（有料アセットのみ）。今回は導入していません。
- **スタミナ・疲労**：Phase 5 の予定。`LocoAnimParams.isFatigued` は今は常に false。
- **AI の走り方の使い分け**（歩く・ジョグ・ダッシュ）：Phase 4 で本格的に。今回は止まり方と到達時間だけ直しました。
- **実機での調整**：数値は計算とテストで決めた初期値です。実際に操作して「重すぎる / 軽すぎる」を教えてください。
- GK も同じ移動モデルを使うので、ポジショニングが少し重くなっている可能性があります（Phase 3 で GK 専用の動きと一緒に見直します）。
