# EsUtil.Platform.Core API リファレンス

UI フレームワーク非依存の共有ライブラリが提供する公開 API のリファレンスです。 使い方（概要・クイックスタート・カスタマイズ）は [`../README.md`](../README.md)、アーキテクチャと設計判断は [`SPEC_System.md`](SPEC_System.md) を参照してください。

## 目次

- [1. 名前空間と型](#1-名前空間と型)
- [2. 公開 API](#2-公開-api)
  - [2.1 `EsKey`（enum）](#21-eskeyenum)
  - [2.2 `EsWindowState`（enum）](#22-eswindowstateenum)
  - [2.3 `CaptureType`（enum）](#23-capturetypeenum)
  - [2.4 `HotKeyModifiers`（`[Flags]` enum）](#24-hotkeymodifiersflags-enum)
  - [2.5 `HotKeySetting`（record）](#25-hotkeysettingrecord)
  - [2.6 `Coord.ScrollMath`（static class）](#26-coordscrollmathstatic-class)
  - [2.7 `Coord.DpiScaler`（static class）](#27-coorddpiscalerstatic-class)
  - [2.8 `Geometry.EsAxis`（enum）](#28-geometryesaxisenum)
  - [2.9 `Geometry.EsRounding`（enum）](#29-geometryesroundingenum)
  - [2.10 `Geometry.EsRectInterval`（enum）](#210-geometryesrectintervalenum)
  - [2.11 `LoggerFallback`（static class）](#211-loggerfallbackstatic-class)
  - [2.12 `Imaging.DrawingColorExtensions`（static class）](#212-imagingdrawingcolorextensionsstatic-class)
  - [2.13 `Geometry.EsPoint`（readonly record struct）](#213-geometryespointreadonly-record-struct)
  - [2.14 `Geometry.EsSize` / `Geometry.EsVector` / `Geometry.EsRect`（readonly record struct）](#214-geometryessize--geometryesvector--geometryesrectreadonly-record-struct)
  - [2.13 `Geometry.EsPointInt` / `Geometry.EsSizeInt` / `Geometry.EsRectInt`（readonly record struct）](#213-geometryespointint--geometryessizeint--geometryesrectintreadonly-record-struct)
  - [2.14 `Geometry.EsThickness`（readonly record struct）](#214-geometryesthicknessreadonly-record-struct)
  - [2.13 `Geometry.EsRectEdges`（readonly struct）](#213-geometryesrectedgesreadonly-struct)
  - [2.14 各フレームワークの操作との対照](#214-各フレームワークの操作との対照)
  - [2.13 `EsPlatformHandle`（readonly record struct）](#213-esplatformhandlereadonly-record-struct)
  - [2.14 `Native.ICaptureService`（interface）](#214-nativeicaptureserviceinterface)
  - [2.13 `Native.ICursorService`（interface）](#213-nativeicursorserviceinterface)
  - [2.14 `Native.IWindowService`（interface）](#214-nativeiwindowserviceinterface)
  - [2.13 `Native.IElementFinder`（interface）](#213-nativeielementfinderinterface)
  - [2.14 `Native.ICursorOverrideGuard`（interface : IDisposable）](#214-nativeicursoroverrideguardinterface--idisposable)
  - [2.13 `Native.IGlobalHotKeyService<T>`（interface : IDisposable）](#213-nativeiglobalhotkeyservicetinterface--idisposable)
  - [2.14 `Native.ElementFinderResults`（readonly record struct）](#214-nativeelementfinderresultsreadonly-record-struct)
  - [2.13 `Native.Win.*`（Windows 実装）](#213-nativewinwindows-実装)
  - [2.14 `Native.Imaging.EsBitmapDrawingExtensions`（static class）](#214-nativeimagingesbitmapdrawingextensionsstatic-class)
  - [2.13 `Coord.ICoordTransform`（interface）](#213-coordicoordtransforminterface)
  - [2.14 `Coord.CoordTransform`（sealed class : ICoordTransform）](#214-coordcoordtransformsealed-class--icoordtransform)
  - [2.13 `Imaging.EsBitmap`（sealed class : IDisposable）](#213-imagingesbitmapsealed-class--idisposable)
- [3. 使用例](#3-使用例)
  - [3.1 幾何と座標変換](#31-幾何と座標変換)
  - [3.2 OS サービスの注入（上位のアプリ）](#32-os-サービスの注入上位のアプリ)
  - [3.3 ロガーの null 吸収](#33-ロガーの-null-吸収)
  - [3.4 ピクセルコンテナ](#34-ピクセルコンテナ)
- [4. 制限事項](#4-制限事項)

## 1. 名前空間と型

| 名前空間 | 型 | 用途 |
| --- | --- | --- |
| `EsUtil.Platform.Core` | `EsKey` / `EsWindowState` / `HotKeyModifiers` / `HotKeySetting` / `CaptureType` / `EsPlatformHandle` / `LoggerFallback` | キーと入力・ウィンドウ状態・ホットキー・ロガーの補助 |
| `.Geometry` | `EsPoint` / `EsSize` / `EsVector` / `EsRect` とその整数版、`EsThickness` / `EsRounding` / `EsRectInterval` / `EsAxis` | DIP 相当の幾何値型と領域の規則 |
| `.Coord` | `ICoordTransform` / `CoordTransform` / `ScrollMath` / `DpiScaler` / `CoordTransformExtensions` | 座標系の変換ポートとスクロール・DPI の純粋計算 |
| `.Imaging` | `EsBitmap` / `DrawingColorExtensions` | ピクセルコンテナと描画色の判定 |
| `.Native` | `ICaptureService` / `ICursorService` / `IWindowService` / `IElementFinder` / `ICursorOverrideGuard` / `IGlobalHotKeyService<T>` / `ElementFinderResults` | OS サービスの抽象（UI 非依存） |
| `.Native.Win` | `WinCaptureService` / `WinCursorService` / `WinWindowService` / `WinElementFinder` / `WinCursorOverrideGuard` / `WinGlobalHotKeyService<T>` | 上記抽象の Windows 実装（ホストが生成して注入する） |
| `.Native.Imaging` | `EsBitmapDrawingExtensions` | `System.Drawing.Bitmap` との相互変換（Windows 専用） |

可視性は public のみを記載します（`internal` は [`SPEC_System.md`](SPEC_System.md) の可視性ポリシーを参照）。

## 2. 公開 API

公開型を 1 型 1 節で記載します。 型名の接頭辞は名前空間を表します（`Coord.` / `Geometry.` / `Imaging.` / `Native.` を省略した型はルート名前空間）。

### 2.1 `EsKey`（enum）

UI フレームワーク非依存のキー識別子。 `Avalonia.Input.Key` と同一のメンバー名を持ち、ホットキー設定の文字列表現・JSON シリアライズ（`JsonStringEnumConverter` 使用時）の互換性を保ちます。

### 2.2 `EsWindowState`（enum）

UI フレームワーク非依存のウィンドウ表示状態。 `Avalonia.Controls.WindowState` と同一のメンバー名（`Normal` / `Minimized` / `Maximized` / `FullScreen`）を持ち、設定ファイルの JSON シリアライズ（`JsonStringEnumConverter` 使用時）の互換性を保ちます。

### 2.3 `CaptureType`（enum）

キャプチャの開始方法を表します。

| メンバー | 説明 |
| --- | --- |
| `ScreenSelect` | 全画面（カーソル位置のスクリーンを対象） |
| `WindowSelect` | ウィンドウ選択（カーソル位置の最前面ウィンドウを対象） |
| `AreaSelect` | 範囲選択（ドラッグで任意矩形） |

UI フレームワーク非依存のキャプチャ種別。 上位のライブラリとホストが共有します。 ホストではグローバルホットキーのアクション識別子（`IGlobalHotKeyService<CaptureType>`）としても使用します。

### 2.4 `HotKeyModifiers`（`[Flags]` enum）

| メンバー | 値 | 説明 |
| --- | --- | --- |
| `None` | `0x0` | 修飾キーなし |
| `Alt` | `0x1` | Alt キー |
| `Control` | `0x2` | Ctrl キー |
| `Shift` | `0x4` | Shift キー |
| `Windows` | `0x8` | Windows キー |

値は Win32 `RegisterHotKey` の `MOD_*` 定数と一致します。

### 2.5 `HotKeySetting`（record）

```csharp
public sealed record HotKeySetting(HotKeyModifiers Modifiers, EsKey Key)
{
    public override string ToString();
}
```

- `ToString()`: "Ctrl + Shift + Q" のようなユーザー可読な文字列を返す。 数字キー（`D0`〜`D9`）・記号キー（`OemMinus` 等）は対応する記号で表示する。
- レコード型のため、`Modifiers` と `Key` が一致すれば等価。

### 2.6 `Coord.ScrollMath`（static class）

- `MaxOffset(double sourceSize, double viewportSize, double scale)`: スクロール範囲の最大オフセットを返す。
- `ViewportSize(double viewportSize, double scale)`: コンテンツ座標での表示範囲を返す。
- スクロールバー計算で共用する（スクロール位置の上限を 1 箇所へ集約するため）。

### 2.7 `Coord.DpiScaler`（static class）

- `StandardDpi`: DIP（デバイス非依存ピクセル）の基準 DPI（`96.0` = 1 ピクセル = 1 DIP）。 スケーリング係数の算出と、DIP を 1:1 で描く描画先の DPI が共用する。
- `StandardScaling`: スケーリング係数の基準値（`1.0` = DIP と物理ピクセルが 1:1）。 係数が未確定のときに使う値。
- `ToScaling(double dpi)`: DPI をスケーリング係数（表示 DIP → 物理ピクセル）へ換算する（`StandardDpi` を 1.0 とする倍率）。
- `IsValidScaling(double scaling)`: スケーリング係数が有効（描画・変換に使える）かどうかを判定する。 **有限の正の値のみ有効**とし、0 以下と非数（NaN / 無限大）は無効とする（乗算で座標が整数の上限へ溢れてウィンドウが壊れるのを防ぐ）。
- `NormalizeScaling(double scaling)`: スケーリング係数を有効値へ正規化する（無効な値は `StandardScaling`）。
- `ScaleToPixel(int/double dipValue, double scaling)`: DIP 値を物理ピクセルへ変換（切り上げ・最低 1。 浮動小数誤差を吸収）。
- `ToDip(int/double physical, double scaling)`: 物理ピクセルを DIP へ変換（`ScaleToPixel` の逆変換。 **丸め・最低値の規則は適用しない**）。 ウィンドウの位置・サイズを物理ピクセルから DIP へ戻す場面（全モニタの包含矩形をウィンドウへ適用する等）で共用する。
- `ToDip(EsSize physicalSize, double scaling)`: 物理ピクセルのサイズを DIP のサイズへ変換する（成分ごとの換算を上記 `ToDip` へ委譲する）。 コア型同士の換算を UI 側で組み立てないための入口。
- 表示サイズ・市松模様のサイズ・外枠の太さの物理ピクセル換算と、ウィンドウの位置・サイズの換算で共用する。

### 2.8 `Geometry.EsAxis`（enum）

レイアウト計算で扱う軸（`X` = 幅 / `Y` = 高さ）です。 軸を `bool` で渡すと `true` が横か縦か読めなくなるため、列挙型で指定します。

### 2.9 `Geometry.EsRounding`（enum）

小数を整数へ丸める方法（`Truncate` = 0 の方向へ切り捨て / `Round` = 最も近い整数（0.5 は 0 から遠い方）/ `Ceiling` = 正の方向へ切り上げ）です。 `bool` で「切り捨てか四捨五入か」を指定すると呼び出し側で意味が読めないため列挙型にし、丸め規則は整数型の `FromEsXxx(..., EsRounding)` へ集約します。

### 2.10 `Geometry.EsRectInterval`（enum）

矩形の幅・高さが**終点を含むかどうか**（区間の流儀）を表します。 同じ「幅 10」でも、始点のピクセルから 10 個を含むのか（`HalfOpen`）、終点のピクセルを含めて 11 個になるのか（`Closed`）で実際のピクセル数が変わります。 `bool` で受け渡すと意味が読めないため列挙型にし、ピクセル数へ変換するときの `+1` の判断を `EsRect.Interval` が単一所有します（下流の層は再判定しません）。

| メンバー | 意味 |
| --- | --- |
| `HalfOpen` | 始点を含み終点を含まない（幅 = ピクセル数。 既定） |
| `Closed` | 始点と終点の両方を含む（幅 = ピクセル数 − 1） |

**使い分け**: ドラッグで作る矩形は「始点から終点まで（両端を含む）」を意図するため `Closed`、OS の検出結果（`RECT` 等）は終点を含まないピクセル数のため `HalfOpen` として扱います。

### 2.11 `LoggerFallback`（static class）

ロガーの null を `NullLogger.Instance` へ吸収する共通ヘルパー。 拡張メソッドとして提供します。

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 拡張メソッド | `OrNullLogger` | `static ILogger OrNullLogger(this ILogger? logger)` | 指定されたロガーを返す。 null の場合は `NullLogger.Instance`（出力なし）を返す |

```csharp
private readonly ILogger _log = logger.OrNullLogger();   // logger が null でも安全
```

「ロガー未設定（null）は出力なしとして扱う」規則を単一所有します。 各コントロール・サービスは `logger ?? NullLogger.Instance` を自前で書かず本メソッドへ委譲してください。

### 2.12 `Imaging.DrawingColorExtensions`（static class）

描画色の判定を提供する拡張メソッドです。

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 拡張メソッド | `IsVisible` | `static bool IsVisible(this DrawingColor color)` | アルファが 0 より大きい場合に true（**アルファ 0 は「描かない」**を意味する） |

```csharp
// ホストの色設定をコアの明示フラグへ写す（色を指定しない = 描かない）
ShowOverlay = settings.OverlayColor!.Value.IsVisible(),
```

描画の可否は**描画にだけ必要な設定を所有する上位層の外観の明示フラグ**が単一所有します。 本メソッドは「ホストの色設定（省略可能な色）をフラグへ写す」場面で使います（`color.A > 0` を各所に書くと、同じ意味の判定が散らばる）。

### 2.13 `Geometry.EsPoint`（readonly record struct）

```csharp
public readonly record struct EsPoint(double X, double Y);
```

UI フレームワーク非依存の 2 次元座標。 各 UI 実装は、自身の座標型との相互変換を Interop 層で提供します。

座標ラベルの文言は `Format(int decimals = 0)` が単一所有します（`(x, y)` 形式。 `decimals` で小数点以下の桁数を 0〜15 から指定し、既定は 0 = 整数へ丸め）。 `ToString()` はレコードの診断表現のままです（デバッガー・ログ・テストの失敗表示から小数を消さないため）。

```csharp
// カーソル座標ラベル（マーキー）・中心座標ラベル（ルーペ）が同じ書式を共用する
var text = mousePosition.Format();      // "(123, 456)"
var precise = mousePosition.Format(2);  // "(123.40, 456.70)"
```

`EsPoint` は座標の操作を一式提供します（`Zero` / `IsEmpty` / `WithX` / `WithY` / `Offset` / `DistanceTo` / `NearlyEquals` / `ToEsVector` / `FromTuple` / `ToTuple` / 演算子 `+ - * /`（単項 `-` を含む））。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Zero` | `static readonly EsPoint Zero` | 原点（0, 0） |
| `IsEmpty` | `bool IsEmpty { get; }` | X・Y とも 0（`Zero` と同じ判定） |
| `WithX` / `WithY` | `EsPoint WithX(double x)` / `EsPoint WithY(double y)` | 指定した成分だけを差し替えた座標 |
| `Offset` | `EsPoint Offset(double dx, double dy)` | 指定量だけ平行移動した座標（ベクターでの移動は `p + v` を使う） |
| `DistanceTo` | `double DistanceTo(EsPoint other)` | 相手までのユークリッド距離（常に 0 以上） |
| `NearlyEquals` | `bool NearlyEquals(EsPoint other)` | 許容誤差（既定 1e-9。 相対誤差も見る）の範囲で等しいか |
| `ToEsVector` | `EsVector ToEsVector()` | 成分をそのまま引き継いだベクター |
| `FromTuple` / `ToTuple` | `static EsPoint FromTuple((double X, double Y) value)` / `(double X, double Y) ToTuple()` | タプルとの相互変換 |
| 演算子 | `+`（座標同士 / ベクター）・`-`（座標同士は `EsVector` を返す / ベクター）・単項 `-`・`* /`（係数は左右とも可） | `(a + v)` は並行移動、`(a - b)` は差分ベクター |

タプルへの分解（`var (x, y) = point;`）はレコードの自動生成が提供します（同じシグネチャを手書きすると二重定義になるため追加しません）。

### 2.14 `Geometry.EsSize` / `Geometry.EsVector` / `Geometry.EsRect`（readonly record struct）

```csharp
public readonly record struct EsSize(double Width, double Height);
public readonly record struct EsVector(double X, double Y);
public readonly record struct EsRect(double X, double Y, double Width, double Height, EsRectInterval Interval = EsRectInterval.HalfOpen);
```

UI フレームワーク非依存のサイズ・ベクター・矩形。 各 UI 実装は、自身のサイズ型・ベクター型・矩形型との相互変換を Interop 層で提供します。

`EsRect` は `Right`（`X + Width`）・`Bottom`（`Y + Height`）・`IsEmpty`（幅または高さが 0 以下）に加え、コントロールに依存しない 2 つの規則を単一所有します（各コアが個別に書くと同じ意味の式が散らばり、片方だけ端の扱いを変えると同じ矩形から違う結果になる）。

`EsRect.Interval` は**幅・高さが終点を含むかどうか**（`Geometry.EsRectInterval`）を値として持ちます。 幅の意味は変えず（`Width = |終点 − 始点|`）、ピクセル数へ変換するときの `+1` の判断だけを本値が単一所有します（下流の層は再判定しません）。 既定は `HalfOpen` であり、省略した場合の挙動は従来と同じです。

**区間の流儀を引き継ぐメソッド**: `WithX` / `WithY` / `WithWidth` / `WithHeight` / `WithPosition` / `WithSize` / `Offset` / `Inflate` / `Deflate` / `Normalize` / `ClampToArea` / `Intersect` / `Union` は `Interval` を保ったまま幾何だけを差し替えます。 `CenterRect(rect)` はサイズを引き継ぐ相手（引数）の流儀を使います。 `FromLTRB` / `FromPoints` / `FromRectInt` / `FromTuple` は流儀を引数に取らないため `HalfOpen` を返します（流儀を指定する場合は `WithInterval` を続けて呼ぶ）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `ResolveInnerEdges` | `EsRectEdges ResolveInnerEdges(double thickness)` | 上・下・左・右の辺の矩形（4 個）をヒープ確保なしで返す。 左右の高さは負にならず 0 にクランプする。 太さの下限正規化は行わない（用途で異なるため呼び出し側が行う） |
| `ComputeInnerEdges` | `IReadOnlyList<EsRect> ComputeInnerEdges(double thickness)` | 同上の配列版（`ResolveInnerEdges` へ委譲。 1 回で 4 要素の配列を確保するため、毎フレームの解決は `ResolveInnerEdges` を使う） |
| `ClampToArea` | `EsRect ClampToArea(EsSize area)` | 四隅を領域（左上を原点・指定サイズ）内へ個別にクランプし、その包絡矩形を返す（負の辺は 0） |
| `Intersect` | `EsRect Intersect(EsRect other)` | 2 つの矩形の重なり部分を返す（重なりが無い場合は空。 幅・高さは負にならない） |

```csharp
// 選択矩形の枠線と拡大表示の外枠が同じ式を共用する
var edges = hole.ComputeInnerEdges(Math.Max(0.5, borderThickness));
```

`EsSize` は「レイアウト前の領域（幅・高さが 0）ではクランプしない」という規則を単一所有します（各コア・各 UI が書くと、同じ意味の述語が散らばり、片方だけが要素を勝手に動かす）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `HasArea` | `bool HasArea { get; }` | 幅・高さとも 0 より大きい場合に true（レイアウト前・親未接続の領域は false） |
| `ClampToArea` | `double ClampToArea(double size)` | サイズを領域の最小辺へクランプする。 領域が未確定の場合はクランプしない |
| `ClampOffset` | `double ClampOffset(double offset, double size, EsAxis axis)` | 1 軸分の位置を `0〜領域の長さ − サイズ` へクランプする（対象の軸の長さが 0 以下ならクランプしない。 対象が収まらない場合は 0） |

`EsSize` はさらにサイズの操作を提供します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Zero` / `Infinity` | `static readonly EsSize Zero` / `static readonly EsSize Infinity` | 幅・高さとも 0 / とも正の無限大（上限なし） |
| `AspectRatio` | `double AspectRatio { get; }` | 幅 ÷ 高さ |
| `WithWidth` / `WithHeight` | `EsSize WithWidth(double width)` / `EsSize WithHeight(double height)` | 指定した成分だけを差し替えたサイズ |
| `Constrain` | `EsSize Constrain(EsSize constraint)` | 成分ごとに小さい方を採る（上限の適用） |
| `Inflate` / `Deflate` | `EsSize Inflate(EsThickness thickness)` / `EsSize Deflate(EsThickness thickness)` | 4 辺の厚さ分だけ拡大 / 縮小（`Deflate` は 0 でクランプ） |
| `NearlyEquals` | `bool NearlyEquals(EsSize other)` | 許容誤差の範囲で等しいか |
| `FromTuple` / `ToTuple` | `static EsSize FromTuple((double Width, double Height) value)` / `(double Width, double Height) ToTuple()` | タプルとの相互変換 |
| 演算子 | `+` / `-`（サイズ同士）・`* /`（係数）・`*`（`EsVector` と成分ごと）・`/`（サイズ同士は `EsVector` を返す） | 成分ごとの計算。 サイズ同士の除算は比をベクターで返す（縦横比の差を求める） |

`EsRect` は矩形の操作を一式提供します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Empty` | `static readonly EsRect Empty` | 全て 0 の矩形（流儀は `HalfOpen`） |
| `Size` | `EsSize Size { get; }` | 幅・高さをそのまま引き継いだサイズ |
| `IsClosedInterval` | `bool IsClosedInterval { get; }` | 閉区間（`Interval == EsRectInterval.Closed`）かどうか |
| `WithInterval` | `EsRect WithInterval(EsRectInterval interval)` | 座標・幅・高さを保ったまま区間の流儀だけを差し替える |
| `FromLTRB` | `static EsRect FromLTRB(double left, double top, double right, double bottom)` | 左・上・右・下の座標から生成（右が左より小さければ負の幅） |
| `FromPoints` | `static EsRect FromPoints(EsPoint topLeft, EsPoint bottomRight)` | 2 つの対角点から生成 |
| `FromRectInt` | `static EsRect FromRectInt(EsRectInt rect)` | 整数矩形から生成 |
| `WithX` 〜 `WithSize` | `EsRect WithX(double x)` / `WithY` / `WithWidth` / `WithHeight` / `WithPosition(EsPoint)` / `WithSize(EsSize)` | 指定した成分だけを差し替えた矩形 |
| `Offset` | `EsRect Offset(double dx, double dy)` | 位置だけを平行移動（幅・高さは不変） |
| `Inflate` | `EsRect Inflate(double dx, double dy)` / `EsRect Inflate(EsThickness thickness)` | 4 辺を広げる（左上は移動し、幅・高さは 2 倍量 / 左右・上下の合計を加える） |
| `Deflate` | `EsRect Deflate(EsThickness thickness)` | 4 辺を狭める（負のサイズもそのまま返す。 クランプは `Normalize` を使う） |
| `Normalize` | `EsRect Normalize()` | 幅・高さを正にする（非数を含む場合は `Empty`） |
| `ContainsExclusive` | `bool ContainsExclusive(EsPoint point)` | 右端・下端を含まない判定（MewUI・WinUI3 の `Contains` と同じ半開区間） |
| `Contains` | `bool Contains(EsRect rect)` | 別の矩形を完全に含むか（左右端・上下端を含む） |
| `Intersects` | `bool Intersects(EsRect other)` | 重なりがあるか（接触のみは false） |
| `Union` | `EsRect Union(EsRect other)` / `EsRect Union(EsPoint point)` | 両者 / 点を包含する最小の矩形（空の矩形は相手を返す） |
| `CenterRect` | `EsRect CenterRect(EsRect rect)` | 指定した矩形を本矩形の中央へ配置した矩形 |
| `NearlyEquals` | `bool NearlyEquals(EsRect other)` | 全ての成分が許容誤差の範囲で等しいか |
| `ToEsRectInt` | `EsRectInt ToEsRectInt(EsRounding rounding = EsRounding.Round)` | 整数矩形への変換（成分ごとに丸める） |
| `ResolveInnerEdges` | `EsRectEdges ResolveInnerEdges(double thickness)` | 4 辺の矩形をヒープ確保なしで返す（描画計画の解決で使う） |
| `FromTuple` / `ToTuple` | `static EsRect FromTuple((double X, double Y, double Width, double Height) value)` / `(double X, double Y, double Width, double Height) ToTuple()` | タプルとの相互変換（流儀は対象外。 `FromTuple` は `HalfOpen`） |
| `Deconstruct` | `void Deconstruct(out double x, out double y, out double width, out double height)` | 幾何の 4 成分へ分解する（流儀を含む 5 成分版はレコードの自動生成が提供） |

**名前を付けなかったもの**: `Left` / `Top` / `Position` は `X` / `Y` / `TopLeft` と同値のため設けません（同じ内容の別名を増やさない）。 `Translate(EsVector)` は `Offset` と同内容のため設けません（ベクターで移す場合は `rect.Offset(v.X, v.Y)`）。

### 2.13 `Geometry.EsPointInt` / `Geometry.EsSizeInt` / `Geometry.EsRectInt`（readonly record struct）

```csharp
public readonly record struct EsPointInt(int X, int Y);
public readonly record struct EsSizeInt(int Width, int Height);
public readonly record struct EsRectInt(int X, int Y, int Width, int Height);
```

`EsPoint` / `EsSize` / `EsRect` の**整数版**（画面物理ピクセル座標・画像ピクセル座標とその矩形・寸法に使用する）。 `EsRectInt` は `Right` / `Bottom` / `TopLeft` / `TopRight` / `BottomLeft` / `BottomRight` / `Center` / `Size` を提供し、`Contains(EsPointInt)`・`Contains(EsRectInt)`（半開区間）・`Intersects`・`Intersect`・`Union(EsRectInt)`・`Union(EsPointInt)` を持ちます。 `TopLeft` は矩形の原点（画像の原点等）を座標の手組みなしで取り出すために使います。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `EsPointInt.ToEsPoint` | `EsPoint ToEsPoint()` | 小数を扱う `EsPoint` への変換（UI が成分を書き写さない） |
| `EsPointInt.FromEsPoint` | `static EsPointInt FromEsPoint(EsPoint point, EsRounding rounding = EsRounding.Round)` | 小数座標から生成（丸め規則を `EsMath.RoundToInt` が単一所有。 `Point.Truncate` / `Round` / `Ceiling` に対応） |
| `EsSizeInt.ToEsSize` / `FromEsSize` | `EsSize ToEsSize()` / `static EsSizeInt FromEsSize(EsSize size, EsRounding rounding = EsRounding.Round)` | 整数の寸法との相互変換（丸め規則は同じ） |
| `EsRectInt.FromEsRect` | `static EsRectInt FromEsRect(EsRect rect, EsRounding rounding = EsRounding.Round)` | 小数矩形から生成（X・Y・幅・高さを個別に丸める。 四隅を丸めて差を取る規則ではない） |
| `EsRectInt.ToEsRect` | `EsRect ToEsRect()` | 小数矩形への変換 |
| `EsRectInt.Empty` / `EsPointInt.Zero` / `EsSizeInt.Zero` | `static readonly` | 全て 0 の値 |
| `IsEmpty` | `bool IsEmpty { get; }` | 幅（または高さ）が 0 以下か。 点は X・Y とも 0 か |
| `FromLTRB` / `FromPoints` | `static EsRectInt FromLTRB(int left, int top, int right, int bottom)` / `FromPoints(EsPointInt topLeft, EsPointInt bottomRight)` | 整数矩形の生成（`Rectangle.FromLTRB` に対応） |
| `WithX` 〜 `WithHeight` | `EsRectInt WithX(int x)` / `WithY` / `WithWidth` / `WithHeight` | 指定した成分だけを差し替えた矩形 |
| `Offset` / `Inflate` | `EsRectInt Offset(int dx, int dy)` / `EsRectInt Inflate(int dx, int dy)` | 位置の平行移動 / 4 辺を広げる（`Rectangle.Offset` / `Inflate` に対応） |
| `Offset` / `WithX` 〜 | `EsPointInt Offset(int dx, int dy)` / `WithX` / `WithY` | 点の平行移動と成分の差し替え |
| `WithWidth` / `WithHeight` | `EsSizeInt WithWidth(int width)` / `WithHeight(int height)` | サイズの成分の差し替え |
| 演算子 | `+` / `-`（点・サイズの成分ごと） | 整数の加減算（オーバーフローは .NET の既定どおり） |
| `FromTuple` / `ToTuple` | `static Xxx FromTuple((...))` / `(...) ToTuple()` | タプルとの相互変換（全 3 型） |

### 2.14 `Geometry.EsThickness`（readonly record struct）

```csharp
public readonly record struct EsThickness(double Left, double Top, double Right, double Bottom);
```

UI フレームワーク非依存の 4 辺の厚さ（Avalonia / MewUI の `Thickness` に対応）。 `new EsThickness(double uniform)`（全辺同値）・`Horizontal`（左 + 右）・`Vertical`（上 + 下）・`FromTuple` / `ToTuple` を提供し、`EsSize` / `EsRect` の `Inflate` / `Deflate` の引数になります。

### 2.13 `Geometry.EsRectEdges`（readonly struct）

4 辺（上・下・左・右）の矩形を保持する固定長コンテナ（128 バイト）です。 `EsRect.ResolveInnerEdges` の戻り値であり、1 回あたり 4 要素の配列（約 160 バイト）を確保する `IReadOnlyList<EsRect>` の代替として、毎フレーム呼ばれる描画計画の解決で使います。 `Count`（= 4）・プロパティ（`Top` / `Bottom` / `Left` / `Right`）・インデクサ（0〜3、範囲外は `ArgumentOutOfRangeException`）・`ToArray` を提供します。 **`IReadOnlyList<T>` を実装しません**（値型をインターフェース経由で扱うとボックス化するため）。

### 2.14 各フレームワークの操作との対照

コアの幾何値型は、UI フレームワーク非依存のまま .NET 標準（`System.Drawing.Primitives` / `System.Numerics`）・WinUI3（`Windows.Foundation`）・Avalonia・MewUI の操作を写して提供します（UI 型そのものは参照しません）。

| 操作 | .NET 標準 | WinUI3 | Avalonia | MewUI | 本ライブラリ |
| --- | --- | --- | --- | --- | --- |
| タプル生成 / 分解 | – | – | 分解のみ | – | `FromTuple` / `ToTuple`（分解はレコードの自動生成） |
| `WithX` / `WithY` 等 | – | – | 点・サイズ・ベクター・矩形 | 点・サイズ・矩形 | 全型 |
| `IsEmpty` | 点 = (0,0) / サイズ = (0,0) | サイズ = (0,0) / 矩形 = 幅 0 または高さ 0 | 矩形は内部のみ | サイズ = (0,0) / 矩形 = 幅 0 または高さ 0 | 点 = `IsEmpty`（原点）/ サイズ・矩形 = `IsEmpty`（**0 以下 = 面積なし**）。 「ちょうど 0」の判定は `HasArea` と併用する |
| `Contains`（点） | 閉区間 | 半開区間 | 閉区間 | 半開区間 | `Contains`（閉区間。 現行互換）と `ContainsExclusive`（半開区間） |
| `Intersect`（非交差時） | `Empty` | `Empty` | `default` | 空の矩形 | 左上座標を保った空の矩形（現行互換） |
| `Union` | 矩形のみ | 矩形 / 点 | 矩形のみ | 矩形のみ | 矩形と点の両方 |
| 平行移動 | `Offset` | – | `Translate(Vector)` | `Offset` | `Offset(double, double)`（単一実装。 `Translate` は設けない） |
| 拡大縮小 | `Inflate` | – | `Inflate` / `Deflate`（`Thickness`） | `Inflate` / `Deflate`（`Thickness`） | `EsRect.Inflate` / `Deflate`（`EsThickness` を受ける版を含む）・`EsSize.Inflate` / `Deflate` |
| 整数丸め | `Point` / `Size` / `Rectangle` の `Ceiling` / `Round` / `Truncate` | – | – | – | `EsRounding` を渡す `FromEsXxx` に集約（`EsMath.RoundToInt` が単一所有） |
| 浮動小数の近似比較 | – | – | `NearlyEquals` | – | `NearlyEquals`（`EsMath.AreClose` が単一所有） |
| ベクターの長さ | `Vector2.Length` / `LengthSquared` | –（単純なベクター型なし） | `Length` / `SquaredLength` | `Length` / `LengthSquared` | `Length` / `LengthSquared` |
| 負のサイズ | 保持 | 保持 | 保持 | **生成時に 0 へクランプ** | 保持（`EsSize.Deflate` と `EsRect.Normalize` のみ正へ正規化） |

**WinUI3** は本ソリューションに参照アセンブリが無いため、相互変換（Interop）ではなく操作の対照のみを提供します。

### 2.13 `EsPlatformHandle`（readonly record struct）

```csharp
public readonly record struct EsPlatformHandle(nint Handle, string? Descriptor = null);
```

ネイティブウィンドウ識別子（Win32: HWND 等）。 `IsValid`（ハンドル値が 0 以外か）を提供します。 座標・寸法ではないためルート名前空間（`EsUtil.Platform.Core`）にあります。

### 2.14 `Native.ICaptureService`（interface）

画面領域のキャプチャを抽象化します。 公開 API は UI フレームワーク非依存の型のみを使用します。 **ネイティブ実装を指す抽象は `EsUtil.Platform.Core.Native` 名前空間（`Native/`）に置きます**（値型・幾何はルート〜`Geometry` / `Imaging` / `Coord`）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `CaptureArea` | `EsBitmap? CaptureArea(EsRectInt bounds)` | 指定画面領域（画面物理座標）をキャプチャ。 失敗時は null |
| `CaptureVirtualScreen` | `EsBitmap? CaptureVirtualScreen(out EsPointInt origin)` | 仮想スクリーン全体を 1 回でキャプチャ |

### 2.13 `Native.ICursorService`（interface）

OS 依存のカーソル操作を抽象化します（UI フレームワーク標準で代替可能な機能は含めません）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `GetCursorScreenPoint` | `EsPointInt GetCursorScreenPoint()` | 現在のカーソル位置を画面物理座標で取得 |
| `SetCursorScreenPoint` | `void SetCursorScreenPoint(EsPointInt point)` | カーソルを画面物理座標へ移動 |
| `ShowCursor` | `void ShowCursor(bool show)` | マウスポインターの表示/非表示を切替 |

### 2.14 `Native.IWindowService`（interface）

ネイティブウィンドウ操作を抽象化します。 ウィンドウは `EsPlatformHandle` で識別します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `HideFromTaskSwitcher` | `void HideFromTaskSwitcher(EsPlatformHandle handle)` | タスクバー / Alt+Tab 非表示 |
| `SetCaptureExclusion` | `bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude)` | キャプチャ除外 / 解除（未対応環境は false） |
| `SetBounds` | `bool SetBounds(EsPlatformHandle handle, EsRectInt bounds)` | 位置・サイズを画面物理ピクセルで指定（未対応環境は false。 全モニターを跨ぐ配置に使う） |
| `EvacuateWindow` | `void EvacuateWindow(EsPlatformHandle handle)` | 最前面解除・非表示・クローズ（UI ハング時の退避） |

### 2.13 `Native.IElementFinder`（interface）

指定位置の要素（ウィンドウ・スクリーン）検出を抽象化します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `FindWindowUnderCursor` | `ElementFinderResults FindWindowUnderCursor(EsPointInt screenPoint)` | 指定位置の最前面ウィンドウを検出 |
| `FindScreenUnderCursor` | `ElementFinderResults FindScreenUnderCursor(EsPointInt screenPoint)` | 指定位置が含まれるスクリーンを検出 |
| `ListScreens` | `IReadOnlyList<ElementFinderResults> ListScreens()` | 全スクリーン一覧 |
| `ListWindows` | `IReadOnlyList<ElementFinderResults> ListWindows()` | 全ウィンドウ一覧 |

### 2.14 `Native.ICursorOverrideGuard`（interface : IDisposable）

選択操作中のカーソル上書き抑止を抽象化します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `IsAttached` | `bool IsAttached { get; }` | 抑止がアタッチ済みか |
| `Attach` | `void Attach(EsPlatformHandle handle)` | 指定ウィンドウに抑止をアタッチ |
| `Dispose` | `void Dispose()` | 抑止を解除 |

### 2.13 `Native.IGlobalHotKeyService<T>`（interface : IDisposable）

グローバルホットキーの登録・解除・アクション配信を抽象化します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Initialize` | `void Initialize(IntPtr hWnd)` | ウィンドウハンドルを設定しフックを設定 |
| `RegisterAll` | `void RegisterAll(IReadOnlyList<(T Action, HotKeySetting Setting)> registrations)` | ホットキーとアクションの対応を登録 |
| `UnregisterAll` | `void UnregisterAll()` | 全ホットキーを解除 |
| `HotKeyPressed` | `event Action<T>? HotKeyPressed` | ホットキー押下時にアクションを配信 |

### 2.14 `Native.ElementFinderResults`（readonly record struct）

```csharp
public readonly record struct ElementFinderResults(
    EsRectInt Bounds,
    bool IsDetected,
    string? Title = null,
    EsPlatformHandle? Handle = null,
    string? ClassName = null);
```

要素検出の結果。 座標は入力と同じ画面物理座標で統一されます。

### 2.13 `Native.Win.*`（Windows 実装）

上記抽象の Windows 実装です（`EsUtil.Platform.Core.Native.Win`）。 **実行時のプラットフォーム分岐（ファクトリ）は持たず、ホストが生成して抽象型として注入します**。 テストは同じ抽象を実装したモックを注入するため、Win32 環境を必要としません。

| クラス | 実装する抽象 |
| --- | --- |
| `WinCaptureService` | `ICaptureService`（`ILogger?` を省略可） |
| `WinCursorService` | `ICursorService` |
| `WinWindowService` | `IWindowService`（`ILogger?` を省略可） |
| `WinElementFinder` | `IElementFinder` |
| `WinCursorOverrideGuard` | `ICursorOverrideGuard` |
| `WinGlobalHotKeyService<T>` | `IGlobalHotKeyService<T>`（`T : notnull`） |

`Native.Win.WinNative`（internal）が Win32 P/Invoke の宣言を集約し、`Native.NativeLogMessage`（internal）が本層のログ定義を担います。

### 2.14 `Native.Imaging.EsBitmapDrawingExtensions`（static class）

`System.Drawing.Bitmap` と `EsBitmap` の相互変換です（`EsUtil.Platform.Core.Native.Imaging`。 `System.Drawing.Common` を使う Windows 専用）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `ToEsBitmap` | `static EsBitmap ToEsBitmap(this DrawingBitmap bitmap)` | `System.Drawing.Bitmap` から `EsBitmap` を作る（複製。 元の Bitmap は変更しない） |
| `ToDrawingBitmap` | `static DrawingBitmap ToDrawingBitmap(this EsBitmap bitmap)` | `EsBitmap` から `System.Drawing.Bitmap`（`Format32bppPArgb`）を作る（複製） |

### 2.13 `Coord.ICoordTransform`（interface）

ローカル DIP と対象座標の双方向変換ポート。 座標はコア幾何型で扱います。 軸はこの 1 つだけであり、DIP とデバイスピクセルの換算は `DpiScaler` が単一所有します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `ToSource` | `EsPoint ToSource(EsPoint dip)` | ローカル DIP 座標 → 対象座標 |
| `ToDip` | `EsPoint ToDip(EsPoint source)` | 対象座標 → ローカル DIP 座標 |
| `SourceArea` | `EsSize? SourceArea { get; }` | 対象座標の境界（null = 無制限） |

### 2.14 `Coord.CoordTransform`（sealed class : ICoordTransform）

`Func` 委譲による変換ポートを提供します。 倍率の向きは 1 通り（対象座標 / ローカル DIP）だけです。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Identity` | `static ICoordTransform Identity { get; }` | 等価変換（ローカル DIP = 対象座標・境界なし）の既定ポート |
| `ClientToSource` | `static ICoordTransform ClientToSource(Func<double> sourcePerDip, Func<EsSize?>? sourceArea = null)` | 対象 = ローカル DIP × `sourcePerDip`（呼び出し毎評価。 倍率は 対象座標 / ローカル DIP。 0 以下は 1.0 扱い。 境界を省略すると無制限） |
| コンストラクタ | `CoordTransform(Func<EsPoint, EsPoint> toSource, Func<EsPoint, EsPoint> toDip, Func<EsSize?> sourceArea)` | 変換関数を直接注入（軸は利用側が定義） |

### 2.13 `Imaging.EsBitmap`（sealed class : IDisposable）

**自前のピクセルバッファ**（BGRA8888・アルファ乗算済み）を所有するピクセルコンテナ。 `NativeMemory.AlignedAlloc` で 64 バイト整列して確保し、**確保直後に全領域を 0（透明）で初期化します**（透明 = 全成分 0 が「描かれていない」の表現であるため）。 **外部パッケージへ依存しない**。

**書き込み量の最適化**: 直後に全領域を書く手順（`Create(w, h, color)` / `Clone` / `Crop` / 相互変換）は 0 初期化を省きます（画像サイズ分の書き込みを丸ごと削減する）。 `Crop` はコピーされないパディングだけを透明にし、`CreateUninitialized` は「全領域を書く」責任を呼び出し側が持つ前提で未初期化のまま確保します。

**生成・変換**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 生成 | `Create` | `static EsBitmap Create(int width, int height)` | 指定サイズの空ビットマップ（BGRA8888・Premul）を生成 |
| 生成 | `Create` | `static EsBitmap Create(EsSizeInt size)` | 指定サイズの空ビットマップを生成 |
| 生成 | `Create` | `static EsBitmap Create(int width, int height, Color color)` / `static EsBitmap Create(EsSizeInt size, Color color)` | 指定サイズのビットマップを全ピクセル指定色で生成（0 初期化を省く） |
| 生成 | `CreateUninitialized` | `static EsBitmap CreateUninitialized(int width, int height)` / `static EsBitmap CreateUninitialized(EsSizeInt size)` | 直後に全領域を書く前提で確保（ピクセルは不定値。 **書かなければ不定値が見える**） |

> `CreateUninitialized` は全行を書く相互変換（System.Drawing / Avalonia / Skia）が使うために公開しています。 書かない領域が残る用途では `Create` を使います。

**Skia との相互変換**（`ToSKBitmap` / `ToEsBitmap`）は SkiaSharp へ依存するため、Skia を使用する層の拡張メソッドが提供します（`ToSKBitmap` は非所有ビュー、`ToEsBitmap` は複製。 所有権はコア側に残ります）。

**プロパティ・ピクセル操作・その他**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| プロパティ | `Width` / `Height` | `int` | 幅 / 高さ（ピクセル） |
| プロパティ | `Size` | `EsSizeInt` | 幅・高さ（ピクセル。 コアの整数サイズ型） |
| プロパティ | `Stride` | `int` | 1 ラインあたりバイト数（**常に 幅 × 4**。 パディングなし） |
| プロパティ | `BytesPerPixel` | `const int` | 1 ピクセルあたりのバイト数（4。 ストライドを計算する相互変換が共用する） |
| プロパティ | `Address` | `nint` | ピクセルデータ先頭アドレス（BGRA8888） |
| プロパティ | `IsDisposed` | `bool` | 破棄済みか |
| ピクセル | `GetPixel` | `(byte B, byte G, byte R, byte A) GetPixel(int x, int y)` | 指定座標の BGRA（アルファ乗算済み）を取得。 範囲外は `IndexOutOfRangeException` |
| ピクセル | `GetPixel` | `Color GetPixel(EsPointInt point)` | 指定座標の BGRA を `System.Drawing.Color` で取得 |
| ピクセル | `SetPixel` | `void SetPixel(int x, int y, byte b, byte g, byte r, byte a)` | 指定座標の BGRA（アルファ乗算済み）を設定 |
| ピクセル | `SetPixel` | `void SetPixel(EsPointInt point, Color color)` | 指定座標へ `System.Drawing.Color` を設定 |
| ピクセル | `TrySetPixel` | `bool TrySetPixel(int x, int y, Color color)` | 指定座標へ色を設定（範囲外は `false`） |
| ピクセル | `Clear` | `void Clear(byte b, byte g, byte r, byte a)` / `void Clear(Color color)` | 全ピクセルを指定色で塗りつぶし |
| その他 | `Clone` | `EsBitmap Clone()` | 独立した複製を生成（所有権は呼び出し側） |
| その他 | `Crop` | `EsBitmap Crop(EsRectInt rect)` / `EsBitmap Crop(int x, int y, int width, int height)` | 指定矩形領域を切り出す（ソース範囲外は透明パディング。 位置関係は保たれる。 0 初期化は行わず、コピーされないパディングだけを透明にする） |
| その他 | `Dispose` | `void Dispose()` | ピクセルバッファを解放（二重呼び出しは無害） |
| その他 | `ThrowIfDisposed` | `void ThrowIfDisposed()` | 破棄済みなら `ObjectDisposedException` を送出 |

> `Stride` は常に `幅 × 4`（パディングなし）です。 `Clear(byte,byte,byte,byte)` は与えられた BGRA 成分をそのまま書き込みます（乗算済みへの変換はしません）。

`System.Drawing.Color`（`System.Drawing.Primitives`）のみに依存するため、**外部パッケージなしで Windows 以外でも動作します**。 Skia との変換は Skia を使用する層、System.Drawing.Bitmap との変換を含む Windows 専用の拡張は `Native.Imaging.EsBitmapDrawingExtensions` が提供します。

## 3. 使用例

### 3.1 幾何と座標変換

```csharp
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

// 幾何（DIP 相当の値型。UI の座標型は境界で 1 行変換する）
var rect = EsRect.FromLTRB(10, 20, 110, 120);
var center = rect.Center;                             // EsPoint
var clamped = rect.ClampToArea(new EsSize(200, 100)); // 領域内へクランプ

// 座標系の変換ポート（対象座標 = ローカル DIP × 倍率）
var port = CoordTransform.ClientToSource(() => 2.0);
var source = port.ToSource(new EsPoint(100, 100));
```

### 3.2 OS サービスの注入（上位のアプリ）

```csharp
// 実行時のプラットフォーム分岐を持たないため、ホストが具象を生成して抽象型として注入する
services.AddSingleton<IGlobalHotKeyService<CaptureType>>(_ => new WinGlobalHotKeyService<CaptureType>());
```

### 3.3 ロガーの null 吸収

```csharp
private readonly ILogger _log = logger.OrNullLogger();   // logger が null でも安全
```

### 3.4 ピクセルコンテナ

```csharp
using var bitmap = EsBitmap.Create(1920, 1080);          // 全ピクセル透明
bitmap.Clear(b: 0, g: 0, r: 255, a: 255);                 // 赤で塗りつぶし
using var region = bitmap.Crop(new EsRectInt(10, 20, 800, 600));
```

## 4. 制限事項

- 本プロジェクトは UI フレームワークへ依存しません。 UI 型との相互変換は、各 UI 実装の Interop 層を用意して境界で行います。
- 実行時のプラットフォーム分岐（ファクトリ）を持ちません。 OS サービスの既定実装はホストが生成して抽象型として注入します（テストはモックを注入できます）。
- `Native/` 配下（Win32 P/Invoke・`System.Drawing.Common`）は Windows 専用です。
- 描画は本プロジェクトの対象外です（`EsBitmap` と配置計算だけを提供し、描画 API は各 UI が呼びます）。
