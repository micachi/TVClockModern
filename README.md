# TVClock Modern

TVClock と同等のデスクトップ常駐時計を**クリーンルーム方式**(元コード非参照)で再実装したものです。
.NET 8 / WPF 製。Windows 10 / 11 でクラッシュしにくい設計にしています。

## 起動

```
publish\TVClockModern.exe
```

## 操作

| 操作 | 動作 |
|---|---|
| ドラッグ | 時計の移動(位置は自動保存) |
| ダブルクリック | 設定画面 |
| 右クリック | メニュー(設定 / 常に手前 / クリックスルー / 終了) |
| タスクトレイのアイコン | 全メニュー(クリックスルー中の解除はここから) |

## 機能

- 常に手前に表示 / 枠なし / 背景透明 / Alt-Tab 非表示
- クリックスルー(時計の下の画面を操作可能。トレイから解除可能なので閉じ込められない)
- 時刻・日付フォーマット(自由書式、プリセット付き)
- フォント / サイズ / 文字色 / 影 / 不透明度
- 単一インスタンス(二重起動しない)
- Windows 起動時の自動開始(HKCU Run 登録・管理者権限不要)
- Per-Monitor V2 DPI 対応(拡大縮小の異なる複数モニタで正しく表示)
- モニタ構成変更・解像度変更時に表示位置を自動補正(画面外に消えない)

## クラッシュ耐性の設計

1. **3重の例外ハンドラ**
   - `DispatcherUnhandledException` → ログに記録し、UI スレッドは生かす
   - `AppDomain.UnhandledException` → 記録後、致命的終了時は**自動で自己再起動**
   - `TaskScheduler.UnobservedTaskException` → 記録して継続
2. **設定ファイルの破損に強い**
   - 保存は「バックアップ → 一時ファイル → 原子的置換」
   - 読み込みは「本体 → バックアップ → 既定値」の3段階フォールバック
   - 破損ファイルは `.corrupt-日時` として退避(データ消失しない)
3. **ログ**: `%LOCALAPPDATA%\TVClockModern\logs\app-YYYYMMDD.log`(7日分自動削除、ログ書き込み失敗でもアプリは落とさない)
4. **異常値の丸め**: フォントサイズ・座標・不透明度は範囲クランプ。壊れた設定でもUIが爆発しない
5. **設定保存は毎秒ではなく間引き**(ディスク負荷の低減)

## ビルド / 配布

SDK があれば:

```powershell
# フレームワーク依存(約150KB、.NET 8 Desktop Runtime が必要)
dotnet publish -c Release -o publish

# 自己完結型(ランタイム不要・単一exe、約70MB)
dotnet publish -c Release -o publish-sc -p:SelfContained=true
```

## 設定ファイル

`%APPDATA%\TVClockModern\settings.json`

## ライセンス / 注意

- 本作は TVClock のコード・リソースを一切使用していません(クリーンルーム実装)。
- 「TVClock」の名称・外観の商標的利用は各自の判断で。
