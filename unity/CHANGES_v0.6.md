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
