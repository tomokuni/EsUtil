# 変更履歴

本リポジトリは**プロジェクトごとに版が異なります**（値の所有者は [`Directory.Build.props`](Directory.Build.props) のバージョン表）。 そのため、この文書は**利用者に影響する変更**を時系列で記録し、版番号は各パッケージのものを併記します。

- 版の 3 桁目（`Patch`）は [`buildScript/pack.ps1`](buildScript/pack.ps1) がソースの指紋から自動で +1 します（人が書きません）。
- 1・2 桁目（`Major` / `Minor`）は破壊的変更・機能追加のときに人が上げます。

## 未リリース

### 追加

- `EsUtil.Ui.AvaloniaUI.ColorComboBox`: 色名・16 進コードの入力とカラーピッカー（`ColorView` の Flyout）を統合した色選択コントロール。
- **`ColorComboBox`（Avalonia / MewUI / WinUI 3）のスウォッチと入力欄にツールチップを追加しました**
  （「色名 + 不透明度」と「`#AARRGGBB`」の 2 行）。
- **MewUI の `ColorComboBox` の「…」の上へ折り畳み時の見出し（「︿」）が出ていた**のを修正しました。
  ピッカー（`ColorPicker`）は 1×1 にして右下へ寄せていましたが、折り畳んだ見出しは
  自分の矩形の外へ描かれるため、行の上に見えていました。 1×1 の入れ物で切り取ります
  （ドロップダウン本体は別の面へ出るため、切り取りの影響を受けません。 実測 2026-10-25）。
- **Avalonia UI 版の一覧を自前の `ListBox`（Flyout）にしました**。 選択後にテキストが全選択されたままにならず、
  行の見た目（スウォッチ + 表記）をコントロール側で決められます。
- **`EsUtil.Ui.WinUI3.Interop` に `SolidBrushCache` を追加しました**。 ARGB ごとに `SolidColorBrush` を
  使い回します。 マーキー / ルーペの図形の層は毎フレーム数十個の矩形へ色を設定するため、
  反映のたびに `new SolidColorBrush(...)` で作ると WinRT の割り当てが積み上がり、
  ドラッグ中の描画が目に見えて重くなっていました（実測 2026-10-25）。
- **MewUI の `ColorComboBox` に入力欄の文字サイズ（`TextFontSize`）を追加しました**。 MewUI の要素は
  文字サイズを継承しないため、周囲の入力欄より 1 回り小さく表示されていました（他の UI は親の
  `FontSize` を継承するため揃います）。 ホストが同じ値を設定できます。
- **`EsUtil.Ui.WinUI3.Interop` の `CompositionImageView` に `WaitForSourceReadyAsync()` を追加しました**。
  `LoadedImageSurface` は**段階的に復号する**ため、読み込みの完了前に表示すると縮小された内容が
  1〜2 フレーム見えます（キャプチャのオーバーレイが一瞬だけ小さく出る原因。 実測 2026-10-25）。
- **入力欄の表記を選べるようになりました**: `EsUtil.Platform.Core.Palette.ColorTextFormat`（`NameWithAlpha` = 色名 + 不透明度 / `HexCode` = `#AARRGGBB`）を追加し、`ColorString.ToText(text, format)` と窓口 `EsColor.ToText` を追加しました。 `ColorComboBox`（Avalonia / MewUI / WinUI 3）の `TextFormat` プロパティから切り替えられます。
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

- **README に「実行に必要な Windows の下限」を書きました**（[`ドキュメント規約.md`](.github/ドキュメント規約.md) §3.4）。
  - **パッケージの `README.md`**: バッジを `Platform-Windows 10 (1809)+`（ビルド 17763）/ `Platform-Windows 10 (2004)+`（ビルド 19041）/ `Platform-Portable`（OS 固有の対象フレームワークを持たない）に分け、本文の「システム要件」（節が無いものはバッジの直後）にも同じ値を書きました。
  - **「指定なし」を「どの OS でも動く」と書かないようにしました**。 `net10.0` の層も `EsUtil.Platform.Core`（Windows 専用の型を持つ）を参照するため、**Windows 以外での動作は未検証**と書きます（検証は Windows でだけ行っています）。
  - **値は `.csproj` の実測**: `net10.0-windows10.0.17763.0` の層はビルド 17763、`EsUtil.Ui.WinUI3.*` は `<SupportedOSPlatformVersion>` = `10.0.19041.0` のため**ビルド 19041**（対象フレームワークの `22621` は API 面であり、実行の下限ではありません）、`net10.0` の層は下限なし（ただし **Windows 以外での動作は未検証**）です。 `EsUtil.Platform.Core` は Windows 専用の型（`Native.Win` 配下・画像の保存/読み込み）だけが 17763、`EsUtil.Feature.Thumbnail.Core` は `ThumbnailImageLoader`（WIC）だけが 17763 です。
  - **ルート `README.md` §3** の表に「Windows の下限」の列を足し、層ごとの差（WinUI 3 だけ 2004 以降）を 1 か所で読めるようにしました。
- **README のバッジを「その README が説明している対象に当てはまるものだけ」に整理しました**（[`ドキュメント規約.md`](.github/ドキュメント規約.md) §3.4）。
  - **共通基盤・機能層（`EsUtil.Platform.Core*` / `EsUtil.Feature.*`）から 4 つの UI バッジを外しました**。 UI を含まないパッケージに「Avalonia / MewUI / WinUI 3 / WinForms」を並べると、そのパッケージが UI を含むと読めてしまいます（4 つの UI 版から使えることはルート `README.md` の表が示します）。
  - **`AOT` バッジ（`AOT-Ready`）を追加しました**。 対象はルート `README.md` と AOT に対応するパッケージで、**WinForms 層は除きます**（[`ビルド設定規約.md`](.github/ビルド設定規約.md) §6.1 と同じ判断）。
  - **nuget.org の版のバッジのラベルを `Release` から `NuGet` へ変えました**（`Release` は `release.yml` の状態を指すため、同じラベルで 2 つの意味を持たせない）。 併せて `release.yml` のバッジをパッケージの README にも置きました。
  - 並び順は NuGet → Release → Build → Platform → .NET → AOT → 対応 UI → License です。
- **ルート `README.md` を再編しました**: プロジェクトの一覧を**ソリューションごと**に並べ（版は nuget.org のバッジ）、`使い方` / `検証` の節を外しました（使い方は各プロジェクトの `README.md`、手順は `.github/開発手順.md` / `.github/検証手順.md` が所有します）。 **`CONTRIBUTING.md` を削除**し、内容を `.github/開発手順.md` へ移しました（本リポジトリは外部からのコントリビューションを受け付けません。 フォークは自由です）。
- **WIC の相互運用を CsWin32 の生成物へ移行しました**（`EsUtil.Platform.Core` の内部実装。 公開 API は変わりません）。 手書きの `[GeneratedComInterface]`（20 ファイル）を削除し、`wincodec.h` から生成した型を生ポインターで使います。 これにより **vtable の位置ずれによるアクセス違反（`0xC0000005`）が構造的に起きなくなります**（実測で 2 回踏んでいました）。 併せて `EsUtil/Platform.Core/src/NativeMethods.json` に `"allowMarshaling": false` を追加したため、Win32 の宣言から**実行時マーシャリングが無くなりました**（`string` の代わりに `PCWSTR` / `Span<char>`、デリゲートの代わりに `delegate* unmanaged` を使います）。
- **減色の実装を WIC へ置き換えました**（`EsUtil.Platform.Core`）。 `EsReadOnlyBitmap.ReduceColors` / `EsBitmap.ReduceColors` / `EsPixmap.ReduceColors` は **WIC のパレット生成（中央値分割）と 8bpp インデックスへの変換**を使い、**自前の減色の計算（ヒストグラム・中央値分割・CIELAB の距離・ディザ）を削除しました**。 保存の `ColorCount` と同じパレットを使うため、**保存したファイルの画素と `ReduceColors` の結果が一致します**（テストが検証）。
- **減色の API が変わりました（破壊的）**: `ReduceColors(int colorCount, EsImageQuality quality = Mid, EsDitherMode dither = None)` → **`ReduceColors(int colorCount, EsDitherMode dither = None)`**（WIC には品質の段階が無い）。 `EsImageSaveOptions.ColorQuality` を削除しました。 `EsDitherMode.FloydSteinberg` を **`EsDitherMode.ErrorDiffusion`** へ改名しました。
- **減色は Windows 専用になりました**: `ReduceColors` は `[SupportedOSPlatform("windows10.0.17763")]` です（WIC は Windows にのみ存在するため）。 `Save` / `Load` と同じ制約です。
- **パレットの透過は「透明か不透明か」の 2 値になりました**: WIC のパレットは透明色を 1 つ持てるため、**完全透明はそのまま残り**、半透明は不透明な色へ寄ります（以前は 4 段階のアルファをパレットへ持てました）。
- **`EsUtil.Platform.Core` のルート名前空間は機能の窓口だけになりました**。 型は機能フォルダ（`Input` / `Window` / `Capture` / `Common`）へ移りました。 型を名指しする利用側は、その機能の名前空間を `using` してください
  （例: `using EsUtil.Platform.Core.Input;` で `EsKey`。 `EsInput.TryParseKey` などの窓口は `using EsUtil.Platform.Core;` だけで使えます）。
- `EsUtil.Ui.AvaloniaUI.ColorPicker` を **`EsUtil.Ui.AvaloniaUI.ColorComboBox`** へ改名しました（型名 `ColorPickerControl` → `ColorComboBoxControl`）。
- **`ColorComboBox`（Avalonia UI 版）のレイアウト用プロパティを改名しました（破壊的）**: `ComboBoxWidth` / `ComboBoxMinWidth` / `ComboBoxMaxWidth` / `ComboBoxPadding` / `ComboBoxMargin` → **`TextWidth` / `TextMinWidth` / `TextMaxWidth` / `TextPadding` / `TextMargin`**（外枠を 1 つにしたため、調整の対象は入力欄であることを名前に表す）。
- **`EsUtil.Ui.AvaloniaUI.ColorComboBox` から次の公開型を削除しました（破壊的）**: `ColorDisplayConverter`（表記の変換は `ColorTextFormat` と `ColorString.ToText` がコアで単一所有）と `TextBoxSelectionHelper`（一覧のポップアップを自前で持つため選択解除が不要）。
- 機能層・アプリ層にあった次の型を `EsUtil.Platform.Core` へ統合しました（利用側の実装は削除できます）。
  - ウィンドウ配置の復元規則 → `Window.EsWindowPlacementMath`
  - ホットキー文字列の解釈 → `Input.HotKeyParser`
  - `EsRectInt` の JSON コンバーター → `Geometry.EsRectIntJsonConverter`
  - 影の余白（4 辺の `double`）→ `Geometry.EsThickness`
- `EsUtil.Ui.WinUI3` の `LoupeRect` / `LoupeRectList` / `MarqueeRect` / `MarqueeRectList` を削除しました。 追加描画は `Interop.Drawing.DrawRect` / `DrawList` を使います（公開メソッド `AddRect(x, y, width, height, argb)` の形は変わりません）。
- UI コントロールが持っていた変換・描画の複製（18 型）を削除し、`EsUtil.Ui.<UI>.Interop` を参照するようにしました。 **UI 層は同一 UI の `Interop` を参照できます**（[依存関係](.github/依存関係.md) §3）。

### 修正

- **WinUI 3 のサムネイル一覧の初回描画が遅かった**のを修正しました。 影を 1 画素の帯
  （`IThumbnailShadowPlan.BuildDeviceBands`）ごとに `Rectangle` として置いていたため、1 件あたり
  532 個（125% の画面）、31 件で 16,000 個を超える要素になり、初回の描画に 258 ms かかっていました
  （実測 2026-10-25）。 計画が持つ停止点（4 辺 = `Edges` → `LinearGradientBrush` / 4 隅 =
  `CornerStops` → `RadialGradientBrush`）を写した**8 個の矩形**にすると 44 ms になります。
  濃さの変化は画像の位置・大きさに依存しないため、ブラシは一覧の全項目で共有します
  （`ThumbnailShadowBrushes`）。
- **WinUI 3 のマーキーが自分の大きさの変化を描き直していなかった**のを修正しました。 描画計画は
  表示の大きさで解決しますが、**レイアウト前（0×0）に解決した計画を保持**していたため、暗転マスクが
  出ず、ポインターを動かして初めて描き直されていました（実測 2026-10-25）。 表示の大きさが変わった
  時点で計画を解決し直し、描き直します。
- **マーキー / ルーペの描画が重かった**のを修正しました。 図形の層が矩形ごとに
  `new SolidColorBrush(...)` で色を作っていたため、ドラッグのたびに WinRT オブジェクトの
  割り当てが数十件発生していました（実測 2026-10-25）。 `SolidBrushCache` で色ごとに 1 つだけ
  作って使い回し、同じ色なら代入もしません。 ルーペの拡大画像も画素の書き込みストリームを
  保持して使い回します（`PixelBuffer.AsStream()` を毎フレーム作らない）。
- **WinUI 3 のルーペが自分の矩形で切り取っていなかった**のを修正しました。 十字線・破線は
  「ルーペの面」の座標で作られますが、選択矩形が切り出し領域より大きいと破線は面の外まで伸び、
  `Canvas` は子を切り取らないため**ウィンドウ全体へ線が描かれていました**（実測 2026-10-25）。
  ルーペの矩形で切り取ります。 併せて `Trace` で割り当て（DIP）と面（物理ピクセル）の対応を
  記録するようにしました（DPI の解釈がずれた場合にログだけで特定できます）。
- **WinUI 3 のルーペが拡大画像を表示しなかった**のを修正しました。 2 つの原因がありました。
  1 つは表示の層（`LoupeVisualLayer`）が `Canvas` の子として既定の 0×0 のまま配置され、
  拡大画像の層が自分自身の矩形で切り取るため**常に空の切り取り**になっていたことです。
  もう 1 つは**レイアウト前（`ActualWidth` = 0）に解決した描画計画が固定されていた**ことで、
  面（`ClientPixelSize`）が 1 画素のままになり、拡大画像も十字線も 1 画素で描かれていました。
  大きさが決まった時点で計画を解決し直します（`SizeChanged`）。
- **WinUI 3 のルーペの拡大画像を CPU で切り出すようにしました**。 これまでは撮影画像全体の
  無圧縮 BMP（4K で 50 MB 超）を作って `LoadedImageSurface` へ渡していましたが、
  ルーペの面（1 辺 160 DIP 程度）の画素だけを `LoupeImageSampler` で最近傍のまま切り出し、
  `WriteableBitmap` へ書いて画像要素で表示します（画素を検証できるようになりました）。
- **MewUI のルーペが描画面全体を消していた**のを修正しました。 ペーストボード（画像外の色）を
  `IGraphicsContext.Clear` で塗っていたため、**背後に描いた撮影画像までルーペの色（既定 `#535353`）で
  塗り潰されていました**（実測 2026-10-25: 領域選択の背景が一色になり、撮影結果が見えない）。
  ルーペの矩形だけを塗るよう変更しました。
- **MewUI のルーペの図形・拡大画像が 1.25 倍に描かれていた**のを修正しました。 描画計画の矩形は
  物理ピクセルですが、描画コンテキストは DIP のため、125% の環境では十字線・枠線・拡大画像が
  ルーペの矩形からはみ出していました。 `1 / ClientToPixelScaling` の倍率を掛けてから描きます。
  併せてルーペの要素で切り取りを有効にしました。
- **WinUI 3 のルーペが拡大画像を表示しなかった**のを修正しました。 表示の層（`LoupeVisualLayer`）が
  `Canvas` の子として既定の 0×0 のまま配置され、拡大画像の層が自分自身の矩形で切り取るため
  **常に空の切り取り**になっていました。 層の大きさをルーペの表示サイズへ合わせました。
- `EnumCanonical.TryParse<TTarget>(string, out TTarget)` が失敗時に未定義値（例: `"999"` → `(EsKey)999`）を `out` に残していたのを `default` へ戻すよう修正しました。
- 各 UI 版の設定画面で、ホットキー入力の解釈と「未設定」の表現を窓口へ統一しました。
- **WinUI 3 のサンプルを `dotnet publish` した配布物が起動時に `0xC000027B` で終了していた**のを修正しました（`EnableMsixTooling` が無く、自分自身の `.pri` が発行されていませんでした）。
- **`IsAotCompatible` の宣言を実際に AOT で動く層だけへ限定しました**。 WinForms 層は宣言を外しています（Windows Forms 自体が AOT/トリミング非対応で `NETSDK1175` になります）。
- **WinUI 3 の Marquee のサンプルが Native AOT で起動しなかった**のを修正しました。 原因は `ItemsSource` へ managed コレクションを渡していたことで（AOT では CsWinRT が COM ラッパーを作れず `E_INVALIDARG`）、`Items.Add` で 1 件ずつ渡すよう変更しました。
- **MewUI の `Marquee` / `Loupe` が自分の位置を無視して描画していた**のを修正しました。 MewUI の描画コンテキストは**ウィンドウ絶対座標**（要素の左上は (0, 0) ではない）であるため、描画の前に自領域の左上へ原点を移します。 併せて `Clear` の使用をやめました（`Clear` は**描画面全体**を消すため、兄弟の要素まで消えていました。 実測 2026-10-24）。
- **WinUI 3 の `CompositionImageView` が `SetBounds` を呼ばない使い方で画像を表示しなかった**のを修正しました（スプライトの大きさが 0×0 のままでした。 割り当てられた大きさへ追従させます）。
- **`ColorComboBox`（Avalonia / MewUI / WinUI 3）の見た目を揃えました**: スウォッチ・入力欄・一覧ボタン・ピッカーボタンの間隔を無くし、スウォッチを入力欄の内側へ入れました。 WinUI 3 版のポップアップは**ウィンドウの外へはみ出せる**ようにし（`ShouldConstrainToRootBounds = false`）、ピッカーのポップアップは高さの上限を外して**スクロールしない**ようにしました。 **枠は外周の 1 つだけ**にし、区画の区切りは枠より薄い 1 ピクセルの垂直線にしました（1 つの入力欄に見えるようにするため）。

- **MewUI の画像描画が拡大・位置ずれしていた**のを修正しました。 `IGraphicsContext.DrawImage` の 3 引数版は
  **`(image, 描画先, 元矩形)`** の順ですが、`ImageRenderer` / `ThumbnailViewport` / `MewImageBinding`（Loupe・
  Marquee・Thumbnail）が**元矩形と描画先を逆に渡していました**（画像が元の画素数で描かれ、切り出し位置も
  ずれる）。 実測 2026-10-24: 修正前はプレビューの画像が領域いっぱいに引き伸ばされ、修正後は縦横比を保った
  表示矩形へ描かれます。
- **`EsUtil.Platform.Core` の EXIF の向きの読み取りで例外が発生していた**のを修正しました。 名前が無い
  メタデータを読むと WIC が毎回 `COMException`（`0x88982F40`）を投げ、デバッガーの出力へ
  「例外がスローされました」が並んでいました。 読み取り器の**先頭の階層を列挙**してから読みに行くように
  変え、BMP / PNG（階層を持たない形式）では例外が出ません（実測: PNG の読み込みで 0 件）。
- **WinUI 3 の `ColorComboBox` の入力欄の文字が上へ寄っていた**のを修正しました。 スウォッチの分だけ左の
  余白を足すときに上下の余白まで 0 にしていたためです（テーマの既定値を保つように変更）。
- **WinUI 3 の Loupe が像を 10 倍に拡大していた**のを修正しました。 `XamlRoot.RasterizationScale`（1.25）を
  `DpiScaler.ResolveClientScale(クライアント画素, クライアント DIP, 既定)` の**第 1 引数**へ渡していたため、
  倍率が `1.25 / 160 ≈ 0.0078` となり下限の `0.1` へ丸められていました（実測: ログの
  `ClientToPixelScaling` が `1` → `0.1`）。 `NormalizeScaling(RasterizationScale)` を使うよう変更しました。
- **WinUI 3 の `MarqueeControl` が選択枠を描画しなかった**のを修正しました。 追加描画のレイヤーを
  `Children` へ入れていませんでした（`LoupeControl` は入れていました）。 併せてレイヤーの大きさを
  `ActualWidth` / `ActualHeight` へ追従させました。
- **WinUI 3 の `Thumbnail` の影がずれていた**のを修正しました。 `ThumbnailShadowPlan.BuildDeviceBands` は
  **入力が DIP・出力が物理画素**ですが、呼び出し側が入力まで倍率を掛けていました。
- **MewUI の `ThumbnailViewport` がすべてのサムネイルをウィンドウ左上へ描いていた**のを修正しました
  （MewUI の描画コンテキストはウィンドウ絶対座標のため、自領域の左上へ原点を移します）。

- **MewUI の `ColorComboBox` のポップアップがウィンドウ左上へ出ていた**のを修正しました（アンカーは
  **ウィンドウ座標**が必要ですが、自領域の矩形を渡していました）。 ピッカーのポップアップは内容の幅を
  明示し、極端に小さくならないようにしました。

### 既知の制限

- `EsUtil.Ui.WinForms.*`: Windows Forms がトリミング/AOT に対応しないため、Native AOT では使用できません。
- **WinUI 3 + Native AOT では managed コレクションを `ItemsSource` に渡せません**（`Items.Add` を使う。 ライブラリ側ではなく WinRT の制約です）。

## 公開前の版（0.x）

初回公開の版です。 すべてのプロジェクトが `0.x` で、**公開 API は破壊的変更を含みうる**ものとして扱ってください（`1.0` で安定させます）。
