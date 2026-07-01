# Cyclone SA+ Step Sequencer
<img src="https://github.com/user-attachments/assets/d44d5398-dc73-4928-8c56-3bd4ce34c164" align="right" width="256">

Vorze Cyclone SA+ を Windows の Bluetooth LE でコントロールするプログラムです。

最低限動作するところまで作った未完成のベータ版ですが、機器自体を使わなくなり完成を諦めたので**著作権完全フリー（CC0）** で公開します。改変・再配布等お好きにお使いください。

<p align="center">
  <a href="https://github.com/NMW0882/Cyclone-SA-plus-Step-Sequencer/releases/latest/download/CycloneSaPlusStepSequencer_v010.zip">
    <b>🍥Download🍥</b>
  </a>
</p>


### 主な機能
- **オリジナル回転パターン作成**  
  自由にステップを組んでパターンを作成可能。作成したパターンはプリセットとして保存・読み込みできます。
- **ランダム性**  
  回転速度や回転時間にランダム性を簡単に加えられます。単調になりにくい自然で複雑な動きが作れます。
- **MIDIによる映像同期機能**  
  MIDIファイルとMPC-HC/BEを同期させて、映像や音声に合わせた回転動作を実現できます。

  
### 基本的な使い方
1. Bluetooth対応のPCで本プログラムを起動
2. Cyclone SA+ の電源を入れると**自動的に認識**します

プログラムは **Sequencerモード** と **MIDI再生モード** で構成されています。
<br clear="right">
#### Sequencerモードの使い方
1. Sequencerタブの中央左にある **+** ボタンでステップを追加
2. Step DetailsからPatternを選択して編集
3. Playボタンで再生

**Pattern: Constant のランダム機能**  
`Enable Randomness` を有効にすると、以下のスライダーでランダムな動きを調整できます。  
- **Intensity**：ランダムな動きの大きさ・激しさを調整。値を大きくすると速く振れ幅の大きな動きになります。  
- **Stability**：中心値への引き寄せ具合を調整。高くすると細かく安定した動きに、低くすると大きくゆったりと漂う動きになります。

**スライダーの操作Tips**  
スライダーのつまみは **[●]** で構成されています。  
- ●をドラッグ → 通常の数値指定  
- 左右の `[]` を引き出す → 範囲内のランダム再生に切り替え

**MIDI割り込み**  
Sequencer再生中でもMIDIを優先して割り込ませることができます。  
Configタブ → **MIDI vs Sequencer Priority** で「MIDI playback prioritized」を選択してください。

#### MIDI同期再生の方法
1. MPC-HC/BEの設定から **Web Interface** を有効にする（デフォルトポート: 13579）
2. ソース動画と同じフォルダに、**同じファイル名・同じ再生時間の `.mid`ファイル**を配置
3. MPC-HC/BEで動画を再生すると自動でMIDIが同期します

#### MIDI仕様（CC#10 Pan）
- **0〜63**：左回転（0で最大速度）
- **64**：停止
- **65〜127**：右回転（127で最大速度）

DAWの動画読み込み機能を使ってCC#10のオートメーション編集を行います。

#### DAWのプレイバックで動作確認したいとき
loopMIDIなどの仮想MIDIデバイスを経由してルーティングしてください。
設定場所：**Config → Device Settings → MIDI Input Device**

### 必要環境
- Vorze Cyclone SA+
- Bluetooth LE対応アダプター
- Windows 10 / 11
- .NET 6.0 Windows Desktop Runtime

### セキュリティ警告について
初回起動時にWindows Defenderが「WindowsによってPCが保護されました」と表示される場合があります。  
その場合は **「詳細情報」→「実行」** をクリックしてください。

### バージョン情報
v0.1.0
今後このプログラムのアップデートや更新の予定はありません。
