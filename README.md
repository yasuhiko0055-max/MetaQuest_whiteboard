# MetaQuest Whiteboard

Meta Quest 3 で、目の前の空間に巨大なボードを出して、数学の考えを自由に書き散らすためのアプリです。
パススルー(周りの部屋が見えたまま)で使い、コントローラーから出る光線で書きます。

いまは **第1段階(書けるボード)** です。付箋・線でつなぐ・数式・共同編集は次の段階で追加します。

## できること(第1段階)

- 幅4m × 高さ2m のボードが目の前 1.8m に現れる
- 9色のペン、5段階の太さ
- 消しゴム(触れた線をまるごと消す)、全消去
- 元に戻す / やり直し(全消去も戻せる)
- 背景の切り替え: 白板 → 黒板 → 方眼紙(5cm・25cm 目盛り、中心が原点)
- ボードをつかんで移動、両手で拡大・縮小
- 自動保存(アプリを閉じても次回続きから)、PNG 画像で書き出し
- 書いている間はコントローラーが軽く震える

## 操作方法

| ボタン | 動作 |
| --- | --- |
| 人差し指トリガー | 書く(消しゴム中は消す) |
| スティック 左右 | 色を変える |
| スティック 上下 | 太さを変える |
| A / X | 元に戻す |
| スティック押し込み | やり直し |
| B / Y 短押し | 消しゴム ⇔ ペン 切り替え |
| B / Y 1.5秒長押し | 全消去(A/X で戻せる) |
| 中指グリップ(片手) | ボードをつかんで動かす |
| 中指グリップ(両手) | 手を広げる・狭めるで拡大縮小 |
| 左手メニューボタン 短押し | 背景切り替え |
| 左手メニューボタン 1秒長押し | 保存 + PNG 書き出し |

左右どちらの手でも書けます。

## セットアップ手順(初回だけ)

### 1. 準備するもの

- Unity Hub と **Unity 6 (6000.0 LTS)**。インストール時に **Android Build Support**(OpenJDK・Android SDK & NDK を含む)にチェック
- Quest 3 を **開発者モード** にしておく(スマホの Meta Horizon アプリ → デバイス → 開発者モード)
- Quest 3 と PC をつなぐ USB-C ケーブル

### 2. Unity プロジェクトを作る

1. Unity Hub → 「新しいプロジェクト」→ テンプレート **Universal 3D** を選び、好きな場所に作成
2. メニュー Window > Package Manager を開き、**Meta XR Core SDK** を入れる
   (Unity Asset Store で「Meta XR Core SDK」を無料で「マイアセットに追加」→ Package Manager の My Assets からインストール。「Meta XR All-in-One SDK」でも可)
3. メニュー File > Build Profiles(または Build Settings)で **Android** に切り替える
4. メニュー Meta > Tools > **Project Setup Tool** を開き、「Fix All」「Apply All」を押す(Quest 向けの設定が自動で入ります)

### 3. このリポジトリのファイルを入れる

このリポジトリの `Assets/Whiteboard` フォルダを、作ったプロジェクトの `Assets` フォルダの中にコピーします。
(GitHub の「Code」→「Download ZIP」で取得できます)

### 4. シーンを組み立てる

1. メニュー Meta > Tools > **Building Blocks** を開き、「**Camera Rig**」と「**Passthrough**」をシーンに追加
2. 元からある Main Camera は削除(Camera Rig に入っています)
3. メニュー **Whiteboard > シーンにボードを配置** を押す → ボードと左右のペンが自動で配置されます
4. シーンを保存(Ctrl+S / Cmd+S)

### 5. Quest 3 で動かす

1. Quest 3 を USB で PC につなぎ、ヘッドセット内の「USB デバッグを許可」を OK
2. Build Profiles で Run Device に Quest 3 を選び **Build And Run**
3. ヘッドセットをかぶると目の前にボードが出ます

## 書き出した画像の場所

PNG は Quest 内の `Android/data/<アプリのパッケージ名>/files/Exports/` に保存されます。
Quest を USB で PC につなぐと、エクスプローラー(Mac は Android File Transfer)から取り出せます。

## フォルダ構成

```
Assets/Whiteboard/Scripts/
  WhiteboardBoard.cs   ボード本体(線の管理・Undo/Redo・保存・PNG書き出し・背景)
  Stroke.cs            1本の線
  PenController.cs     コントローラーをペンにする(光線・ボタン操作・振動)
  BoardGrabber.cs      ボードをつかんで移動・拡大縮小
  BoardTextures.cs     背景(白板・黒板・方眼紙)をプログラムで生成
  Editor/WhiteboardSetup.cs  メニュー「Whiteboard > シーンにボードを配置」
```

## 次の段階の予定

- 第2段階: 付箋、付箋や図を線でつなぐ、数式入力、定規・座標軸・グラフ、複数ボード、ハンドトラッキング(手で書く)
- 第3段階: 立体図形を置いて回す、PC から画面を映す・参加する
