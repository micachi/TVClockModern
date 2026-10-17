English | [简体中文](README.zh-Hans.md) | [한국어](README.ko.md) | **日本語**

📖 [ユーザーマニュアル](docs/USER_MANUAL.md) — User Manual: [English](docs/USER_MANUAL.en.md) / [简体中文](docs/USER_MANUAL.zh-Hans.md) / [한국어](docs/USER_MANUAL.ko.md)

# TVClock Modern

<p align="center">
  <img src="assets/icon-512.png" width="128" alt="TVClock Modern">
</p>

> **注意: 本プロジェクトは TVClock 及びその作者とは一切関係ありません。**
> 独立したオープンソースプロジェクトであり、オリジナルのコード・リソース・商標を使用していません。

Windows デスクトップ常駐のデジタル時計。**クリーンルーム方式で新規実装**した汎用デスクトップ時計です。
.NET 8 / WPF 製。単一 exe・常駐・省負荷・クラッシュしにくい設計。

![.NET 8.0](https://img.shields.io/badge/.NET-8.0--windows-blue) ![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey)

## 特徴

- **ガジェット的常駐**: 常に手前 / 枠なし / 背景透明 / Alt-Tab 非表示 / 単一 exe
- **マルチモニタ対応**: 全モニタへの同時表示、モニタ指定、モニタ構成変更の自動追従、Per-Monitor V2 DPI
- **豊かな表示**: フォント / サイズ / 色 / 縁取り / 影 / 不透明度 / 時刻・日付フォーマット自由書式
- **文字単位レンダリング**: 文字間隔・区切り間隔の調整
- **太字**: 時刻・日付を太字で表示可能(文字タブ)
- **切り替え効果**: 横回転 / 縦回転 / せり上げ / せり下げ / フェードイン・アウト / クロスフェード
- **マウス接近フェード**: 接近時に透明化。透明解除キー(Ctrl 等)で一時的に操作可能に
- **クリックスルー**: 時計の下を操作可能(トレイから常にアクセス可能なので閉じ込められない)
- **更新通知**: GitHub Releases を 24h に 1 回自動確認。バルーン通知＋情報タブから確認、リリースページへ
- **多言語UI**: 日本語 / English / 简体中文 / 한국語(動作タブで切替、即時反映)
- **位置プリセット**: 左上 / 右上 / 左下 / 右下 / 中央。桁数変化でも自動再整列
- **設定のインポート / エクスポート**: JSON で持ち運び

## スクリーンショット

**デスクトップ常駐**(縁取り・文字間隔・区切り間隔を調整した例)

![](docs/shot-clock.png)

**設定画面**(変更は「適用」で反映)

![](docs/shot-settings.png)

**時刻の切り替え効果** — 秒が変わるたびに数字がアニメーションで入れ替わります(効果時間 50〜500ms):

| 効果 | アニメーション |
|---|---|
| 横に回転 | ![](docs/effect-flipx.gif) |
| 縦に回転 | ![](docs/effect-flipy.gif) |
| せり上げ | ![](docs/effect-slideup.gif) |
| せり下げ | ![](docs/effect-slidedown.gif) |
| フェードイン・アウト | ![](docs/effect-fade.gif) |
| クロスフェード | ![](docs/effect-crossfade.gif) |

## クラッシュしにくい設計

- 3重の例外ハンドラ(`Dispatcher` / `AppDomain` / `TaskScheduler`)— UI 例外は記録して継続、致命的終了時は**自動で自己再起動**
- 設定ファイルは**原子的書き込み + 3段階フォールバック**(本体 → バックアップ → 既定値)、破損ファイルは退避
- **秒同期タイマー**(毎秒1回の起床)、フェードは GPU コンポジション、近接監視は 60Hz ポーリングのみ
- ログ: `%LOCALAPPDATA%\TVClockModern\logs\`(7日分自動ローテーション)

## 動作環境

- Windows 10 / 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)(自己完結版ビルドなら不要)

## ビルド

```powershell
# フレームワーク依存(約150KB)
dotnet publish -c Release -o publish

# 自己完結型・単一 exe(ランタイム不要)
dotnet publish -c Release -o publish-sc -p:SelfContained=true
```

## 使い方

| 操作 | 動作 |
|---|---|
| 起動 | `publish\TVClockModern.exe` |
| ドラッグ | 移動(位置は自動保存) |
| ダブルクリック | 設定画面 |
| 右クリック | メニュー |
| タスクトレイ | 全機能(クリックスルー中の解除もここから) |
| `--settings` 引数 | 設定画面付きで起動(自動化用) |

設定は `%APPDATA%\TVClockModern\settings.json` に保存されます。

**詳しい使い方は [ユーザーマニュアル](docs/USER_MANUAL.md) を参照してください。**
(全設定項目の解説、書式指定子一覧、マルチモニタの挙動、トラブルシューティング等)

変更履歴は [CHANGELOG.md](CHANGELOG.md) を参照してください。

## 免責事項(無保証・現状有姿)

本ソフトウェアは**現状有姿(AS IS)**で提供されます。明示的か暗黙的かを問わず、商品性、特定目的への適合性、非侵害を含むいかなる保証も行いません。

- 本ソフトウェアの使用または使用不能により生じたいかなる損害(データ消失、障害、金銭的損失等)についても、作者は一切の責任を負いません
- 常駐ソフトウェアの特性上、ご利用の環境により予期しない動作が発生する可能性があります。重要な作業環境への導入前に、動作確認の上ご利用ください
- 設定ファイル・ログは自己責任で管理してください

## ライセンス

[MIT](LICENSE)

## 名称について

「TVClock Modern」という名称は、既存のデスクトップ時計アプリに着想を得た**独立プロジェクト**であることを示すためのものです。以下に該当しません:

- 既存の TVClock(またはその他の時計ソフト)のフォーク・改変・逆アセンブル
- 既存ソフトの作者・権利者との提携・承認・後援
- 既存ソフトのリソース(画像・アイコン・コード)の利用
