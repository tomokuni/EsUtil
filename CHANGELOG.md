# 変更履歴

本リポジトリは**プロジェクトごとに版が異なります**（値の所有者は [`Directory.Build.props`](Directory.Build.props) のバージョン表）。 そのため、この文書は**利用者に影響する変更**を時系列で記録し、版番号は各パッケージのものを併記します。

- 版の 3 桁目（`Patch`）は [`buildScript/pack.ps1`](buildScript/pack.ps1) がソースの指紋から自動で +1 します（人が書きません）。
- 1・2 桁目（`Major` / `Minor`）は破壊的変更・機能追加のときに人が上げます。

## 未リリース

### 追加

- `EsUtil.Ui.AvaloniaUI.ColorComboBox`: 色名・16 進コードの入力とカラーピッカー（`ColorView` の Flyout）を統合した色選択コントロール。
- `EsUtil.Ui.*.Interop` に `TextMeasure` / `GdiFill.Rects` / `EsBitmapDrawingInterop.CopyRegion` / `DrawList` / `DrawRect` を追加（UI コントロールが持っていた複製を置き換えるため）。
- `EsUtil.Platform.Core`:
  - `EsInput` / `EsWindow` / `EsCapture` / `EsErrors` / `EsCallbacks`（機能の窓口）。
  - `Input.HotKeyParser`（ホットキー文字列の解釈）/ `Window.EsWindowPlacementMath`（ウィンドウ配置の規則）/ `Geometry.EsRectIntJsonConverter`（設定ファイルの保存形式）/ `Native.Win.WinVirtualScreen`。
  - `DpiScaler.ToPoint` / `DpiScaler.ResolveClientScale` / `EsSizeInt.ToSizeText` / `DrawingColorExtensions.TryParseHexText` / `EsInput.NoHotKey`。
- `CONTRIBUTING.md` / `CHANGELOG.md` / `.github/workflows/build.yml`（公開に向けた整備）。

### 変更（破壊的）

- **`EsUtil.Platform.Core` のルート名前空間は機能の窓口だけになりました**。 型は機能フォルダ（`Input` / `Window` / `Capture` / `Common`）へ移りました。 型を名指しする利用側は、その機能の名前空間を `using` してください
  （例: `using EsUtil.Platform.Core.Input;` で `EsKey`。 `EsInput.TryParseKey` などの窓口は `using EsUtil.Platform.Core;` だけで使えます）。
- `EsUtil.Ui.AvaloniaUI.ColorPicker` を **`EsUtil.Ui.AvaloniaUI.ColorComboBox`** へ改名しました（型名 `ColorPickerControl` → `ColorComboBoxControl`）。
- 機能層・アプリ層にあった次の型を `EsUtil.Platform.Core` へ統合しました（利用側の実装は削除できます）。
  - ウィンドウ配置の復元規則 → `Window.EsWindowPlacementMath`
  - ホットキー文字列の解釈 → `Input.HotKeyParser`
  - `EsRectInt` の JSON コンバーター → `Geometry.EsRectIntJsonConverter`
  - 影の余白（4 辺の `double`）→ `Geometry.EsThickness`
- `EsUtil.Ui.WinUI3` の `LoupeRect` / `LoupeRectList` / `MarqueeRect` / `MarqueeRectList` を削除しました。 追加描画は `Interop.Drawing.DrawRect` / `DrawList` を使います（公開メソッド `AddRect(x, y, width, height, argb)` の形は変わりません）。
- UI コントロールが持っていた変換・描画の複製（18 型）を削除し、`EsUtil.Ui.<UI>.Interop` を参照するようにしました。 **UI 層は同一 UI の `Interop` を参照できます**（[依存関係](.github/依存関係.md) §3）。

### 修正

- `EnumCanonical.TryParse<TTarget>(string, out TTarget)` が失敗時に未定義値（例: `"999"` → `(EsKey)999`）を `out` に残していたのを `default` へ戻すよう修正しました。
- 各 UI 版の設定画面で、ホットキー入力の解釈と「未設定」の表現を窓口へ統一しました。

## 公開前の版（0.x）

初回公開の版です。 すべてのプロジェクトが `0.x` で、**公開 API は破壊的変更を含みうる**ものとして扱ってください（`1.0` で安定させます）。
