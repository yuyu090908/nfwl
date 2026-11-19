# NFWL Unity v0.6「REAL FOOTBALL EVOLUTION」作業記録

v0.5 Crystalia（ブランチ `claude/project-thread-kdvpa7`）から段階的に進めています。
各STEPごとにコミットしています。画面右上のバージョン表示（例：`v0.6-dev STEP 2`）で、どのSTEPが動いているか確認できます。

## 確認のしかた（Mac）

1. ターミナルで：
   ```
   cd （nfwl をクローンしたフォルダ）
   git fetch origin
   git checkout claude/project-thread-kkc9n8
   git pull
   ```
2. Unity を一度閉じて、Unity Hub から同じ `nfwl/unity/NFWL` を開き直す
3. メニュー **NFWL → Open Main Scene** → ▶

## 調整ファイル

メニュー **NFWL → Create Tuning File** で `Assets/Resources/NFWLTuning.asset` ができます。
選択すると Inspector に移動・旋回などの数値が並び、変更は試合に反映されます（Play中の変更もEditorでは約1秒で反映）。
ファイルを作らなくても、コード内の初期値（v0.5 と同じ速度基準）で動きます。

---

## STEP 2：選手移動・加減速・旋回・重心（実装済み・実機未確認）

- 新規：`Core/NFWLTuning.cs`（調整値）、`Player/PlayerLocomotion.cs`（移動）
- 変更：`PlayerController.cs`（移動処理を PlayerLocomotion に置き換え）、`PlayerModel.cs`（傾き・横移動・後退・踏ん張りの表示）、`MatchManager.cs`（バージョン表示と移動状態の表示）、`Editor/NFWLProjectSetup.cs`（調整ファイル作成メニュー）
- 走る向き（速度）と体の向きを別々に管理。ダッシュ中に逆方向を押すと、その場で反転せず「踏ん張って減速 → 軸足で回る → 押し出して再加速」（約0.5秒）。
- カーブは Agility による横方向のグリップで曲がれる量が決まり、急な角度ほど減速。
- 前傾：速度と加速で前に傾き、急停止では上体が慣性で前に出てから戻る。カーブでは内側に傾く。
- 速いダッシュからの急旋回でバランスを崩し、回復中（Recovery）は少し遅く、腕を広げる。
- 守備：ボールを持った相手の4m以内で Shift を押していないと、相手を向いたまま横移動・後退（ジョッキー）。Shift で通常の走りに戻る。
- Speed / Acceleration / Agility が最高速・加速時間・旋回とバランス回復に効く（v0.5 の数値を初期値として維持）。

**見るところ**：画面右上のバージョンの下に、操作中の選手の状態（`Sprint` `SharpTurn` `PivotTurn` `Backpedal` `Jockey` など）・速度・傾き・バランスが出ます。Shift+D で走ってから A を押すと `SharpTurn → PivotTurn → Accelerate` と変わり、体がくるっと反転しないことを確認してください。

## STEP 3：足の接地・IK・自然な走行（実装済み・実機未確認）

- 新規：`Animation/LegIK.cs`（2ボーンIK：股関節→膝→足首）、`Animation/GaitController.cs`（足の接地・踏み出し）
- 変更：`PlayerModel.cs`（足首の関節を追加し、脚をIKで動かす。アニメは LateUpdate で移動後に計算）
- 接地中の足は地面に固定（Foot Locking）。次の着地点は体の速度から予測し、歩幅＝速度÷ピッチで自動的に合う（Stride Synchronization）。
- ピッチ（1秒あたりの歩数）は速度で上がり、接地時間は「脚が届く範囲（約0.8m）を体が通過する時間」に合わせるので、足が届かずに滑ることを防ぐ。
- 骨盤の高さ調整：接地足が届かないときだけ腰を必要な分だけ下げる。走行時は1歩ごとに上下動。
- 地面の高さと傾きをレイキャストで取り、足の高さと足裏の角度を合わせる（Slope Adaptation。今のピッチは平らなので見た目の変化はほぼなし）。
- 立ち止まって向きを変えると、足踏みで向き直る（体が回るのに足が滑らない）。ピボット中は軸足のつま先で回る。
- 横移動・ジョッキーは足幅を広げ、左右の足が交差しないよう着地点を補正。
- 走りの腕振りは実際の脚の位置から計算（左脚が前なら左腕は後ろ）、骨盤と肩が逆向きにひねられる。
- キックとGKのダイブ・ゴールのジャンプ中は、その脚のIKを切って従来の動き（STEP 5 で作り直し）。

**見るところ**：選手の靴が芝の上で滑らず、踏んでから離れること。走り出し・急停止・その場での向き直りで足踏みすること。右上表示の末尾 `foot` は、接地足が脚の長さで届かなかった距離（cm）で、ほぼ 0.0 なら滑っていません。
