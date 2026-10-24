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
  - **画像の保存形式を 5 から 8 へ拡張**しました（`EsImageFileFormat.JpegXr` / `Heif` / `JpegXl`）。 JPEG XR（`.jxr` / `.wdp`）は Windows 標準で常に使え、品質指定（`EsImageSaveOptions.JpegQuality`）も効きます。 HEIF（`.heic` / `.heif`）と JPEG XL（`.jxl`）はコーデック拡張が必要で、`EsReadOnlyBitmap.SupportedFileExtensions` は**実際にエンコーダーがある拡張子だけ**を返します（実行環境で判定し、結果を保持）。
  - **形式の一覧を実行環境から作るようになりました**（固定の一覧を廃止）。 `IWICImagingFactory::CreateComponentEnumerator` で登録済みのエンコーダーを列挙し、**8x8 の試し書きが通った形式だけ**を `SupportedFileExtensions` に残します。 この環境では 19 拡張子（`.bmp` / `.dib` / `.rle` / `.gif` / `.jpeg` / `.jpe` / `.jpg` / `.jfif` / `.exif` / `.png` / `.tiff` / `.tif` / `.wdp` / `.jxr` / `.dds` / `.heic` / `.heif` / `.hif` / `.jxl`）が並びます。
  - **減色（`ColorCount`）を受け付ける形式を固定しなくなりました**（エンコーダーの返答で決まります）。 実測では TIFF もパレット画像として保存できます（以前は「未検証」として拒否していました）。
  - `EsImageFileExtensions.All` に `.jxr` / `.wdp` / `.heic` / `.heif` / `.jxl` を追加（一覧に並べる判断は、読み書きできるかの判断と別であるため）。
- `CHANGELOG.md` / `.github/workflows/build.yml` / `.github/workflows/release.yml` / `.github/開発手順.md`（公開に向けた整備）。 **`CONTRIBUTING.md` は置きません**（本リポジトリは外部からのコントリビューションを受け付けないため。 開発の進め方は `.github/開発手順.md` が持ちます）。

### 変更（破壊的）

- **ルート `README.md` を再編しました**: プロジェクトの一覧を**ソリューションごと**に並べ（版は nuget.org のバッジ）、`使い方` / `検証` の節を外しました（使い方は各プロジェクトの `README.md`、手順は `.github/開発手順.md` / `.github/検証手順.md` が所有します）。 **`CONTRIBUTING.md` を削除**し、内容を `.github/開発手順.md` へ移しました（本リポジトリは外部からのコントリビューションを受け付けません。 フォークは自由です）。
- **WIC の相互運用を CsWin32 の生成物へ移行しました**（`EsUtil.Platform.Core` の内部実装。 公開 API は変わりません）。 手書きの `[GeneratedComInterface]`（20 ファイル）を削除し、`wincodec.h` から生成した型を生ポインターで使います。 これにより **vtable の位置ずれによるアクセス違反（`0xC0000005`）が構造的に起きなくなります**（実測で 2 回踏んでいました）。 併せて `EsUtil/Platform.Core/src/NativeMethods.json` に `"allowMarshaling": false` を追加したため、Win32 の宣言から**実行時マーシャリングが無くなりました**（`string` の代わりに `PCWSTR` / `Span<char>`、デリゲートの代わりに `delegate* unmanaged` を使います）。
- **減色の実装を WIC へ置き換えました**（`EsUtil.Platform.Core`）。 `EsReadOnlyBitmap.ReduceColors` / `EsBitmap.ReduceColors` / `EsPixmap.ReduceColors` は **WIC のパレット生成（中央値分割）と 8bpp インデックスへの変換**を使い、**自前の減色の計算（ヒストグラム・中央値分割・CIELAB の距離・ディザ）を削除しました**。 保存の `ColorCount` と同じパレットを使うため、**保存したファイルの画素と `ReduceColors` の結果が一致します**（テストが検証）。
- **減色の API が変わりました（破壊的）**: `ReduceColors(int colorCount, EsImageQuality quality = Mid, EsDitherMode dither = None)` → **`ReduceColors(int colorCount, EsDitherMode dither = None)`**（WIC には品質の段階が無い）。 `EsImageSaveOptions.ColorQuality` を削除しました。 `EsDitherMode.FloydSteinberg` を **`EsDitherMode.ErrorDiffusion`** へ改名しました。
- **減色は Windows 専用になりました**: `ReduceColors` は `[SupportedOSPlatform("windows10.0.17763")]` です（WIC は Windows にのみ存在するため）。 `Save` / `Load` と同じ制約です。
- **パレットの透過は「透明か不透明か」の 2 値になりました**: WIC のパレットは透明色を 1 つ持てるため、**完全透明はそのまま残り**、半透明は不透明な色へ寄ります（以前は 4 段階のアルファをパレットへ持てました）。
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
- **WinUI 3 のサンプルを `dotnet publish` した配布物が起動時に `0xC000027B` で終了していた**のを修正しました（`EnableMsixTooling` が無く、自分自身の `.pri` が発行されていませんでした）。
- **`IsAotCompatible` の宣言を実際に AOT で動く層だけへ限定しました**。 WinForms 層は宣言を外しています（Windows Forms 自体が AOT/トリミング非対応で `NETSDK1175` になります）。
- **WinUI 3 の Marquee のサンプルが Native AOT で起動しなかった**のを修正しました。 原因は `ItemsSource` へ managed コレクションを渡していたことで（AOT では CsWinRT が COM ラッパーを作れず `E_INVALIDARG`）、`Items.Add` で 1 件ずつ渡すよう変更しました。

- **MewUI の `Marquee` / `Loupe` が自分の位置を無視して描画していた**のを修正しました。 MewUI の描画コンテキストは**ウィンドウ絶対座標**（要素の左上は (0, 0) ではない）であるため、描画の前に自領域の左上へ原点を移します。 併せて `Clear` の使用をやめました（`Clear` は**描画面全体**を消すため、兄弟の要素まで消えていました。 実測 2026-10-24）。
- **WinUI 3 の `CompositionImageView` が `SetBounds` を呼ばない使い方で画像を表示しなかった**のを修正しました（スプライトの大きさが 0×0 のままでした。 割り当てられた大きさへ追従させます）。
- **`ColorComboBox`（Avalonia / MewUI / WinUI 3）の見た目を揃えました**: スウォッチ・入力欄・一覧ボタン・ピッカーボタンの間隔を無くし、スウォッチを入力欄の内側へ入れました。 WinUI 3 版のポップアップは**ウィンドウの外へはみ出せる**ようにし（`ShouldConstrainToRootBounds = false`）、ピッカーのポップアップは高さの上限を外して**スクロールしない**ようにしました。

### 既知の制限

- `EsUtil.Ui.WinForms.*`: Windows Forms がトリミング/AOT に対応しないため、Native AOT では使用できません。
- **WinUI 3 + Native AOT では managed コレクションを `ItemsSource` に渡せません**（`Items.Add` を使う。 ライブラリ側ではなく WinRT の制約です）。

## 公開前の版（0.x）

初回公開の版です。 すべてのプロジェクトが `0.x` で、**公開 API は破壊的変更を含みうる**ものとして扱ってください（`1.0` で安定させます）。
