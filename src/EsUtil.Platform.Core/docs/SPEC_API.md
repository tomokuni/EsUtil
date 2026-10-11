# EsUtil.Platform.Core API リファレンス

UI フレームワーク非依存の共有ライブラリが提供する公開 API のリファレンスです。 使い方（概要・クイックスタート・カスタマイズ）は [`../README.md`](../README.md)、アーキテクチャと設計判断は [`SPEC_System.md`](SPEC_System.md) を参照してください。

## 目次 <!-- omit from toc -->

- [1. 名前空間と型](#1-名前空間と型)
- [2. 公開 API](#2-公開-api)
  - [2.1 `EsKey`（enum）](#21-eskeyenum)
  - [2.2 `EnumCanonical`（static class）](#22-enumcanonicalstatic-class)
  - [2.3 `EsKeyExtensions`（static class）](#23-eskeyextensionsstatic-class)
  - [2.4 `EsWindowState`（enum）](#24-eswindowstateenum)
  - [2.5 `CaptureType`（enum）](#25-capturetypeenum)
  - [2.6 `HotKeyModifiers`（`[Flags]` enum）](#26-hotkeymodifiersflags-enum)
  - [2.7 `HotKeySetting`（record）](#27-hotkeysettingrecord)
  - [2.8 `Coord.ScrollMath`（static class）](#28-coordscrollmathstatic-class)
  - [2.9 `Coord.DpiScaler`（static class）](#29-coorddpiscalerstatic-class)
  - [2.10 `Geometry.EsAxis`（enum）](#210-geometryesaxisenum)
  - [2.11 `Geometry.EsRounding`（enum）](#211-geometryesroundingenum)
  - [2.12 `Geometry.EsRectInterval`（enum）](#212-geometryesrectintervalenum)
  - [2.13 `Imaging.DrawingColorExtensions`（static class）](#213-imagingdrawingcolorextensionsstatic-class)
  - [2.14 `Geometry.EsPoint`（readonly record struct）](#214-geometryespointreadonly-record-struct)
  - [2.15 `Geometry.EsSize` / `Geometry.EsVector` / `Geometry.EsRect`（readonly record struct）](#215-geometryessize--geometryesvector--geometryesrectreadonly-record-struct)
  - [2.16 `Geometry.EsPointInt` / `Geometry.EsSizeInt` / `Geometry.EsRectInt`（readonly record struct）](#216-geometryespointint--geometryessizeint--geometryesrectintreadonly-record-struct)
  - [2.17 `Geometry.EsThickness`（readonly record struct）](#217-geometryesthicknessreadonly-record-struct)
  - [2.18 `Geometry.EsRectEdges`（readonly struct）](#218-geometryesrectedgesreadonly-struct)
  - [2.19 各フレームワークの操作との対照](#219-各フレームワークの操作との対照)
  - [2.20 `EsPlatformHandle`（readonly record struct）](#220-esplatformhandlereadonly-record-struct)
  - [2.21 `Native.ICaptureService`（interface）](#221-nativeicaptureserviceinterface)
  - [2.22 `Native.ICursorService`（interface）](#222-nativeicursorserviceinterface)
  - [2.23 `Native.IWindowService`（interface）](#223-nativeiwindowserviceinterface)
  - [2.24 `Native.IElementFinder`（interface）](#224-nativeielementfinderinterface)
  - [2.25 `Native.ICursorOverrideGuard`（interface : IDisposable）](#225-nativeicursoroverrideguardinterface--idisposable)
  - [2.26 `Native.IGlobalHotKeyService<T>`（interface : IDisposable）](#226-nativeiglobalhotkeyservicetinterface--idisposable)
  - [2.27 `Native.ElementFinderResults`（readonly record struct）](#227-nativeelementfinderresultsreadonly-record-struct)
  - [2.28 `Native.Win.*`（Windows 実装）](#228-nativewinwindows-実装)
  - [2.29 `Coord.ICoordTransform`（interface）](#229-coordicoordtransforminterface)
  - [2.30 `Coord.CoordTransform`（sealed class : ICoordTransform）](#230-coordcoordtransformsealed-class--icoordtransform)
  - [2.31 `Imaging.EsReadOnlyBitmap`（sealed class : IDisposable） / `Imaging.EsBitmap`（sealed class : IDisposable） / `Imaging.EsPixmap`（sealed class : IDisposable）](#231-imagingesreadonlybitmapsealed-class--idisposable--imagingesbitmapsealed-class--idisposable--imagingespixmapsealed-class--idisposable)
  - [2.32 画像のファイル入出力（`Save` / `Load`）](#232-画像のファイル入出力save--load)
  - [2.33 画像型の設計上の判断（兄弟と借用を選んだ理由）](#233-画像型の設計上の判断兄弟と借用を選んだ理由)
  - [2.34 画像操作の他ライブラリとの比較（実測）](#234-画像操作の他ライブラリとの比較実測)
- [3. 使用例](#3-使用例)
  - [3.1 幾何と座標変換](#31-幾何と座標変換)
  - [3.2 OS サービスの注入（上位のアプリ）](#32-os-サービスの注入上位のアプリ)
  - [3.3 ログの取得と記録](#33-ログの取得と記録)
  - [3.4 ピクセルコンテナ](#34-ピクセルコンテナ)
- [4. 制限事項](#4-制限事項)

## 1. 名前空間と型

| 名前空間 | 型 | 用途 |
| --- | --- | --- |
| `EsUtil.Platform.Core` | `EsKey` / `EsKeyExtensions` / `EnumCanonical` / `EsWindowState` / `HotKeyModifiers` / `HotKeySetting` / `CaptureType` / `EsPlatformHandle` / `BoundaryCatch` | キーと入力（正規化含む）・ウィンドウ状態・ホットキー・境界の判定 |
| `.Geometry` | `EsPoint` / `EsSize` / `EsVector` / `EsRect` とその整数版、`EsThickness` / `EsRounding` / `EsRectInterval` / `EsAxis` | DIP 相当の幾何値型と領域の規則 |
| `.Coord` | `ICoordTransform` / `CoordTransform` / `ScrollMath` / `DpiScaler` / `CoordTransformExtensions` | 座標系の変換ポートとスクロール・DPI の純粋計算 |
| `.Imaging` | `EsReadOnlyBitmap` / `EsBitmap` / `EsPixmap` / `DrawingColorExtensions` / `EsImageSaveOptions` / `EsImageFileFormat` / `ImageQuality` / `DitherMode` | 読み取り専用・書き換え可能な画像と 0 コピーのビュー、保存の指定、縮小と減色の品質、描画色の判定 |
| `.Native` | `ICaptureService` / `ICursorService` / `IWindowService` / `IElementFinder` / `ICursorOverrideGuard` / `IGlobalHotKeyService<T>` / `ElementFinderResults` | OS サービスの抽象（UI 非依存） |
| `.Native.Win` | `WinCaptureService` / `WinCursorService` / `WinWindowService` / `WinElementFinder` / `WinCursorOverrideGuard` / `WinGlobalHotKeyService<T>` | 上記抽象の Windows 実装（ホストが生成して注入する） |

可視性は public のみを記載します（`internal` は [`SPEC_System.md`](SPEC_System.md) の可視性ポリシーを参照）。

## 2. 公開 API

公開型を 1 型 1 節で記載します。 型名の接頭辞は名前空間を表します（`Coord.` / `Geometry.` / `Imaging.` / `Native.` を省略した型はルート名前空間）。

### 2.1 `EsKey`（enum）

UI フレームワーク非依存のキー識別子。 **メンバー集合と名前は `Avalonia.Input.Key` と完全に一致**し（実測: 両者とも 223 メンバー・差分 0）、ホットキー設定の文字列表現・JSON シリアライズ（`JsonStringEnumConverter` 使用時）の互換性を保ちます。

- **他のキー体系とは上位互換の関係**です（実測 2026-10-05）。 `System.Windows.Input.Key`（.NET / WPF。 202 メンバー）と `Aprillz.MewUI.Key`（91 メンバー）は本型の**部分集合**です。
- `Windows.System.VirtualKey`（WinUI 3 が使う WinRT 射影。 172 メンバー）は**名前が別体系**（88 件が不一致）で、記号キー（`Oem*`）とメディア キー（`Volume*` / `Media*` / `Launch*`）を持たず、キー以外（マウス ボタン・ゲームパッド）が混在するため、本型の基盤にはしません。
- **Avalonia 側には同じ値を持つ別名が 29 組あります**（`Return` / `Enter`、`CapsLock` / `Capital`、`OemTilde` / `Oem3` 等。 実測: 223 名に対し異なる値は 194）。 本型は値の指定が無い（連番）ため**重複を持ちません**。 別名の同一性は保てないため、**変換の契約は「キーの値が保たれる」**とします（**代表名へ正規化**され、どの名前が代表かは .NET が保証しません。 実測: 29 組中 10 組が宣言順の先頭と異なる）。 なお順方向（本型 → Avalonia）では 2 つの別名が**同じ値へ衝突**するため、Avalonia 側から元の名前は復元できません。
- **各 UI 実装は自身のキー型との相互変換を Interop 層で提供**します（名前ベース。 未定義値は `None`）。 Win32 の仮想キーコードへの変換は `Native.Win.WinNative.KeyToWin32VirtualKey` が単一所有します（未対応のキーは `0` = 登録できない値）。
- **`Avalonia.Input.Key` への追随は往復テストが強制**します（実装側の Interop テスト。 双方向の名前集合一致・往復・未定義値）。

### 2.2 `EnumCanonical`（static class）

同じ値を持つ別名（重複値）を許す列挙型の「代表名」による解決を提供します。 列挙型が同じ値を持つ複数の名前を持つ場合、.NET は「値から名前を引くときにどれを返すか」を保証しないため（`Avalonia.Input.Key` には 29 組の別名がある）、フレームワークをまたぐ写像は**名前で解決して正規化された結果を使う**方針をここへ単一所有します。

| メンバー | 説明 |
| --- | --- |
| `TryParse<TSource, TTarget>(TSource source, out TTarget target)` → `bool` | 別名を持つ可能性のある列挙値を、**代表名**で別の列挙型へ解決する（未定義値と名前が一致しない値は false。 同じ値の別名は同じ結果へ正規化される） |

- **未定義値の検出は `Enum.IsDefined` が担う**（`Enum.TryParse` は数値文字列を受理するため、名前解決の成否だけでは検出できない）。
- 各 UI 実装の Interop（`EsKeyAvaloniaInterop` / `EsKeyMewUIInterop`）は本クラスを使う（同じ手順を各所に書かない）。

### 2.3 `EsKeyExtensions`（static class）

`EsKey` の正規化を提供します。

| メンバー | 説明 |
| --- | --- |
| `Normalize(this EsKey key)` → `EsKey` | キーを正規化する（**未定義値を `EsKey.None` へ寄せる**。 定義済みの値はそのまま返す。 冪等） |

- **文字列から解釈した直後に通す**ことを意図します（`Enum.TryParse` は数値文字列を受理するため、列挙に無い値がキーとして通り、登録時の仮想キーコード変換で失敗する）。
- 「値の有無」と「`None` キー」の区別が必要な場合は `Enum.IsDefined` を併用します。

### 2.4 `EsWindowState`（enum）

UI フレームワーク非依存のウィンドウ表示状態。 `Avalonia.Controls.WindowState` と同一のメンバー名（`Normal` / `Minimized` / `Maximized` / `FullScreen`）を持ち、設定ファイルの JSON シリアライズ（`JsonStringEnumConverter` 使用時）の互換性を保ちます。

### 2.5 `CaptureType`（enum）

キャプチャの開始方法を表します。

| メンバー | 説明 |
| --- | --- |
| `ScreenSelect` | 全画面（カーソル位置のスクリーンを対象） |
| `WindowSelect` | ウィンドウ選択（カーソル位置の最前面ウィンドウを対象） |
| `AreaSelect` | 範囲選択（ドラッグで任意矩形） |

UI フレームワーク非依存のキャプチャ種別。 上位のライブラリとホストが共有します。 ホストではグローバルホットキーのアクション識別子（`IGlobalHotKeyService<CaptureType>`）としても使用します。

### 2.6 `HotKeyModifiers`（`[Flags]` enum）

| メンバー | 値 | 説明 |
| --- | --- | --- |
| `None` | `0x0` | 修飾キーなし |
| `Alt` | `0x1` | Alt キー |
| `Control` | `0x2` | Ctrl キー |
| `Shift` | `0x4` | Shift キー |
| `Windows` | `0x8` | Windows キー |

値は Win32 `RegisterHotKey` の `MOD_*` 定数と一致します。

### 2.7 `HotKeySetting`（record）

```csharp
public sealed record HotKeySetting(HotKeyModifiers Modifiers, EsKey Key)
{
    public override string ToString();
}
```

- `ToString()`: "Ctrl + Shift + Q" のようなユーザー可読な文字列を返す。 数字キー（`D0`〜`D9`）・記号キー（`OemMinus` 等）は対応する記号で表示する。
- レコード型のため、`Modifiers` と `Key` が一致すれば等価。

### 2.8 `Coord.ScrollMath`（static class）

- `MaxOffset(double sourceSize, double viewportSize, double scale)`: スクロール範囲の最大オフセットを返す。
- `ViewportSize(double viewportSize, double scale)`: コンテンツ座標での表示範囲を返す。
- スクロールバー計算で共用する（スクロール位置の上限を 1 箇所へ集約するため）。

### 2.9 `Coord.DpiScaler`（static class）

- `StandardDpi`: DIP（デバイス非依存ピクセル）の基準 DPI（`96.0` = 1 ピクセル = 1 DIP）。 スケーリング係数の算出と、DIP を 1:1 で描く描画先の DPI が共用する。
- `StandardScaling`: スケーリング係数の基準値（`1.0` = DIP と物理ピクセルが 1:1）。 係数が未確定のときに使う値。
- `ToScaling(double dpi)`: DPI をスケーリング係数（表示 DIP → 物理ピクセル）へ換算する（`StandardDpi` を 1.0 とする倍率）。
- `IsValidScaling(double scaling)`: スケーリング係数が有効（描画・変換に使える）かどうかを判定する。 **有限の正の値のみ有効**とし、0 以下と非数（NaN / 無限大）は無効とする（乗算で座標が整数の上限へ溢れてウィンドウが壊れるのを防ぐ）。
- `NormalizeScaling(double scaling)`: スケーリング係数を有効値へ正規化する（無効な値は `StandardScaling`）。
- `ScaleToPixel(int/double dipValue, double scaling)`: DIP 値を物理ピクセルへ変換（切り上げ・最低 1。 浮動小数誤差を吸収）。
- `ToDip(int/double physical, double scaling)`: 物理ピクセルを DIP へ変換（`ScaleToPixel` の逆変換。 **丸め・最低値の規則は適用しない**）。 ウィンドウの位置・サイズを物理ピクセルから DIP へ戻す場面（全モニタの包含矩形をウィンドウへ適用する等）で共用する。
- `ToDip(EsSize physicalSize, double scaling)`: 物理ピクセルのサイズを DIP のサイズへ変換する（成分ごとの換算を上記 `ToDip` へ委譲する）。 コア型同士の換算を UI 側で組み立てないための入口。
- 表示サイズ・市松模様のサイズ・外枠の太さの物理ピクセル換算と、ウィンドウの位置・サイズの換算で共用する。

### 2.10 `Geometry.EsAxis`（enum）

レイアウト計算で扱う軸（`X` = 幅 / `Y` = 高さ）です。 軸を `bool` で渡すと `true` が横か縦か読めなくなるため、列挙型で指定します。

### 2.11 `Geometry.EsRounding`（enum）

小数を整数へ丸める方法（`Truncate` = 0 の方向へ切り捨て / `Round` = 最も近い整数（0.5 は 0 から遠い方）/ `Ceiling` = 正の方向へ切り上げ）です。 `bool` で「切り捨てか四捨五入か」を指定すると呼び出し側で意味が読めないため列挙型にし、丸め規則は整数型の `FromEsXxx(..., EsRounding)` へ集約します。

### 2.12 `Geometry.EsRectInterval`（enum）

矩形の幅・高さが**終点を含むかどうか**（区間の流儀）を表します。 同じ「幅 10」でも、始点のピクセルから 10 個を含むのか（`HalfOpen`）、終点のピクセルを含めて 11 個になるのか（`Closed`）で実際のピクセル数が変わります。 `bool` で受け渡すと意味が読めないため列挙型にし、ピクセル数へ変換するときの `+1` の判断を `EsRect.Interval` が単一所有します（下流の層は再判定しません）。

| メンバー | 意味 |
| --- | --- |
| `HalfOpen` | 始点を含み終点を含まない（幅 = ピクセル数。 既定） |
| `Closed` | 始点と終点の両方を含む（幅 = ピクセル数 − 1） |

**使い分け**: ドラッグで作る矩形は「始点から終点まで（両端を含む）」を意図するため `Closed`、OS の検出結果（`RECT` 等）は終点を含まないピクセル数のため `HalfOpen` として扱います。

### 2.13 `Imaging.DrawingColorExtensions`（static class）

描画色の判定を提供する拡張メソッドです。

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 拡張メソッド | `IsVisible` | `static bool IsVisible(this DrawingColor color)` | アルファが 0 より大きい場合に true（**アルファ 0 は「描かない」**を意味する） |

```csharp
// ホストの色設定をコアの明示フラグへ写す（色を指定しない = 描かない）
ShowOverlay = settings.OverlayColor!.Value.IsVisible(),
```

描画の可否は**描画にだけ必要な設定を所有する上位層の外観の明示フラグ**が単一所有します。 本メソッドは「ホストの色設定（省略可能な色）をフラグへ写す」場面で使います（`color.A > 0` を各所に書くと、同じ意味の判定が散らばる）。

### 2.14 `Geometry.EsPoint`（readonly record struct）

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

### 2.15 `Geometry.EsSize` / `Geometry.EsVector` / `Geometry.EsRect`（readonly record struct）

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

### 2.16 `Geometry.EsPointInt` / `Geometry.EsSizeInt` / `Geometry.EsRectInt`（readonly record struct）

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

### 2.17 `Geometry.EsThickness`（readonly record struct）

```csharp
public readonly record struct EsThickness(double Left, double Top, double Right, double Bottom);
```

UI フレームワーク非依存の 4 辺の厚さ（Avalonia / MewUI の `Thickness` に対応）。 `new EsThickness(double uniform)`（全辺同値）・`Horizontal`（左 + 右）・`Vertical`（上 + 下）・`FromTuple` / `ToTuple` を提供し、`EsSize` / `EsRect` の `Inflate` / `Deflate` の引数になります。

### 2.18 `Geometry.EsRectEdges`（readonly struct）

4 辺（上・下・左・右）の矩形を保持する固定長コンテナ（128 バイト）です。 `EsRect.ResolveInnerEdges` の戻り値であり、1 回あたり 4 要素の配列（約 160 バイト）を確保する `IReadOnlyList<EsRect>` の代替として、毎フレーム呼ばれる描画計画の解決で使います。 `Count`（= 4）・プロパティ（`Top` / `Bottom` / `Left` / `Right`）・インデクサ（0〜3、範囲外は `ArgumentOutOfRangeException`）・`ToArray` を提供します。 **`IReadOnlyList<T>` を実装しません**（値型をインターフェース経由で扱うとボックス化するため）。

### 2.19 各フレームワークの操作との対照

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

### 2.20 `EsPlatformHandle`（readonly record struct）

```csharp
public readonly record struct EsPlatformHandle(nint Handle, string? Descriptor = null);
```

ネイティブウィンドウ識別子（Win32: HWND 等）。 `IsValid`（ハンドル値が 0 以外か）を提供します。 座標・寸法ではないためルート名前空間（`EsUtil.Platform.Core`）にあります。

### 2.21 `Native.ICaptureService`（interface）

画面領域のキャプチャを抽象化します。 公開 API は UI フレームワーク非依存の型のみを使用します。 **ネイティブ実装を指す抽象は `EsUtil.Platform.Core.Native` 名前空間（`Native/`）に置きます**（値型・幾何はルート〜`Geometry` / `Imaging` / `Coord`）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `CaptureArea` | `EsBitmap CaptureArea(EsRectInt bounds)` | 指定画面領域（画面物理座標）をキャプチャ。 幅・高さが 0 以下なら `ArgumentOutOfRangeException`、取得に失敗したら `Win32Exception`（**生成した画像を所有するのは呼び出し側**） |
| `CaptureVirtualScreen` | `EsBitmap? CaptureVirtualScreen(out EsPointInt origin)` | 仮想スクリーン全体を 1 回でキャプチャ。 **`null` は「仮想スクリーンの境界が取得できない」の 1 つの意味だけを表す** |

**`CaptureArea` が null を返さない理由**: 取得できないと撮影を成立させられないためです。 失敗は例外にして**対象領域**と Win32 のエラー コードを残します（記録は呼び出し側が行います）。

### 2.22 `Native.ICursorService`（interface）

OS 依存のカーソル操作を抽象化します（UI フレームワーク標準で代替可能な機能は含めません）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `TryGetCursorScreenPoint` | `bool TryGetCursorScreenPoint(out EsPointInt point)` | 現在のカーソル位置を画面物理座標で取得。 失敗したら **`false`**（`point` は既定値。 **原点を入れない**） |
| `TrySetCursorScreenPoint` | `bool TrySetCursorScreenPoint(EsPointInt point)` | カーソルを画面物理座標へ移動。 失敗したら `false` |
| `ShowCursor` | `void ShowCursor(bool show)` | マウスポインターの表示/非表示を切替（Win32 は成否を返さないため戻り値なし） |

**`GetCursorScreenPoint` が原点を返さない理由**: 原点は画面の左上という**有効な座標**であるため、失敗を原点で表すと呼び出し側が「左上へ移動する」という誤った操作を行います（失敗が座標として意味を持ってしまいます）。

### 2.23 `Native.IWindowService`（interface）

ネイティブウィンドウ操作を抽象化します。 ウィンドウは `EsPlatformHandle` で識別します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `HideFromTaskSwitcher` | `void HideFromTaskSwitcher(EsPlatformHandle handle)` | タスクバー / Alt+Tab 非表示。 取得または設定に失敗したら `Win32Exception` |
| `SetCaptureExclusion` | `bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude)` | キャプチャ除外 / 解除。 **能力の有無**（Windows 10 2004 未満は `false`） |
| `SetBounds` | `void SetBounds(EsPlatformHandle handle, EsRectInt bounds)` | 位置・サイズを画面物理ピクセルで指定（全モニターを跨ぐ配置に使う）。 設定できなければ `Win32Exception` |
| `EvacuateWindow` | `void EvacuateWindow(EsPlatformHandle handle)` | 最前面解除・非表示・クローズ（UI ハング時の退避）。 **ベストエフォート**（1 つ失敗しても残りを実行し、失敗を報告しない） |

**`SetCaptureExclusion` が例外にしない理由**: Windows 10 2004 未満では OS が対応せず**写り込むだけで撮影は成立する**ためです（1 件の非対応で撮影を中止させません）。

### 2.24 `Native.IElementFinder`（interface）

指定位置の要素（ウィンドウ・スクリーン）検出を抽象化します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `FindWindowUnderCursor` | `ElementFinderResults FindWindowUnderCursor(EsPointInt screenPoint)` | 指定位置の最前面ウィンドウを検出 |
| `FindScreenUnderCursor` | `ElementFinderResults FindScreenUnderCursor(EsPointInt screenPoint)` | 指定位置が含まれるスクリーンを検出 |
| `ListScreens` | `IReadOnlyList<ElementFinderResults> ListScreens()` | 全スクリーン一覧 |
| `ListWindows` | `IReadOnlyList<ElementFinderResults> ListWindows()` | 全ウィンドウ一覧 |

### 2.25 `Native.ICursorOverrideGuard`（interface : IDisposable）

選択操作中のカーソル上書き抑止を抽象化します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `IsAttached` | `bool IsAttached { get; }` | 抑止がアタッチ済みか（**`Attach` の成否もこの値で判別する**） |
| `Attach` | `void Attach(EsPlatformHandle handle)` | 指定ウィンドウに抑止をアタッチ（差し替えに失敗した場合は `IsAttached` が false のまま。 例外にしない — 抑止できないだけで選択操作は成立する） |
| `Dispose` | `void Dispose()` | 抑止を解除 |

### 2.26 `Native.IGlobalHotKeyService<T>`（interface : IDisposable）

グローバルホットキーの登録・解除・アクション配信を抽象化します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Initialize` | `void Initialize(IntPtr hWnd)` | ウィンドウハンドルを設定しフックを設定。 差し替えに失敗したら `Win32Exception`（押下を検出できないため） |
| `RegisterAll` | `IReadOnlyList<(T Action, HotKeySetting Setting)> RegisterAll(IReadOnlyList<(T Action, HotKeySetting Setting)> registrations)` | ホットキーとアクションの対応を登録し、**登録できなかった組を返す**（他のアプリとの競合は正常な入力の変種。 1 件の競合でアプリを止めない） |
| `UnregisterAll` | `void UnregisterAll()` | 全ホットキーを解除（後始末であるため失敗を報告しない） |
| `Subscribe` | `IDisposable Subscribe(Action<T> onPressed)` | ホットキー押下の購読（値型の通知は `event` を使わない）。 解除は戻り値の `Dispose` |

### 2.27 `Native.ElementFinderResults`（readonly record struct）

```csharp
public readonly record struct ElementFinderResults(
    EsRectInt Bounds,
    bool IsDetected,
    string? Title = null,
    EsPlatformHandle? Handle = null,
    string? ClassName = null);
```

要素検出の結果。 座標は入力と同じ画面物理座標で統一されます。

### 2.28 `Native.Win.*`（Windows 実装）

上記抽象の Windows 実装です（`EsUtil.Platform.Core.Native.Win`）。 **実行時のプラットフォーム分岐（ファクトリ）は持たず、ホストが生成して抽象型として注入します**。 テストは同じ抽象を実装したモックを注入するため、Win32 環境を必要としません。

| クラス | 実装する抽象 |
| --- | --- |
| `WinCaptureService` | `ICaptureService`（ロガーを持たない） |
| `WinCursorService` | `ICursorService`（ロガーを持たない） |
| `WinWindowService` | `IWindowService`（ロガーを持たない） |
| `WinElementFinder` | `IElementFinder`（ロガーを持たない） |
| `WinCursorOverrideGuard` | `ICursorOverrideGuard`（ロガーを持たない） |
| `WinGlobalHotKeyService<T>` | `IGlobalHotKeyService<T>`（`T : notnull`。 ロガーを持たない） |

`Native.Win.WinNative`（internal）が OS の API（`user32` / `gdi32` / `dwmapi` / `kernel32` / `gdiplus`）の P/Invoke の宣言を集約します。 **本プロジェクトはログを出力しません**（`ILogger` を扱わないため。 失敗は例外または戻り値で返し、記録は呼び出し側が行います）。

### 2.29 `Coord.ICoordTransform`（interface）

ローカル DIP と対象座標の双方向変換ポート。 座標はコア幾何型で扱います。 軸はこの 1 つだけであり、DIP とデバイスピクセルの換算は `DpiScaler` が単一所有します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `ToSource` | `EsPoint ToSource(EsPoint dip)` | ローカル DIP 座標 → 対象座標 |
| `ToDip` | `EsPoint ToDip(EsPoint source)` | 対象座標 → ローカル DIP 座標 |
| `SourceArea` | `EsSize? SourceArea { get; }` | 対象座標の境界（null = 無制限） |

### 2.30 `Coord.CoordTransform`（sealed class : ICoordTransform）

`Func` 委譲による変換ポートを提供します。 倍率の向きは 1 通り（対象座標 / ローカル DIP）だけです。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Identity` | `static ICoordTransform Identity { get; }` | 等価変換（ローカル DIP = 対象座標・境界なし）の既定ポート |
| `ClientToSource` | `static ICoordTransform ClientToSource(Func<double> sourcePerDip, Func<EsSize?>? sourceArea = null)` | 対象 = ローカル DIP × `sourcePerDip`（呼び出し毎評価。 倍率は 対象座標 / ローカル DIP。 0 以下は 1.0 扱い。 境界を省略すると無制限） |
| コンストラクタ | `CoordTransform(Func<EsPoint, EsPoint> toSource, Func<EsPoint, EsPoint> toDip, Func<EsSize?> sourceArea)` | 変換関数を直接注入（軸は利用側が定義） |

### 2.31 `Imaging.EsReadOnlyBitmap`（sealed class : IDisposable） / `Imaging.EsBitmap`（sealed class : IDisposable） / `Imaging.EsPixmap`（sealed class : IDisposable）

読み取り専用の画像・書き換え可能な画像・**0 コピーの読み取りビュー**の 3 型です。 **読み取り専用と書き換え可能は兄弟**（互いに派生せず、**暗黙の変換もキャストもできません**）。 読み取り専用の型は画素を書き換える入口を持たないため、**受け取った側が誤って書き換えることがコンパイル時に決まります**（実行時の判定に頼りません）。 **外部パッケージへ依存しません**。

| 型 | 役割 | 書き込みの入口 |
| --- | --- | --- |
| `EsReadOnlyBitmap` | **読み取り専用**の画像（画像を**受け取る・保持する**側） | **無し**（持たないことが型で決まる） |
| `EsBitmap` | **書き換え可能**な画像（画像を**作る・書き込む**側） | `Pixels` / `SetPixel` / `TrySetPixel` / `Clear` / `SetReadOnly` |
| `EsPixmap` | 画像の一部を指す **0 コピーの読み取りビュー**（切り出しの複製を避ける） | **無し**（読み取り専用のビュー） |

**兄弟にする理由（継承にしない）**: 継承にすると**未凍結の画素を読み取り専用として暗黙に渡せてしまいます**（upcast は無料のため）。 受け取った側は「内容が変わらない」ことを前提にしますが、渡した側が書き続ければ画素は変わり、**約束が静かに破れます**。 兄弟ならその経路が型で消え、読み取り専用として渡すには **`FromBitmap`（凍結済みなら 0 コピー・未凍結なら複製）** を通すしかありません。 これは実測した Skia と同じ構造です（`SKImage` と `SKBitmap` は兄弟で、共有は `SKImage.FromBitmap` が凍結状態で判断します）。 代償は**無料の upcast が無いこと**であり、`FromBitmap` を 1 回呼ぶ必要があります。

**型が保証すること／保証しないこと（重要）**: 読み取り専用の型は「**この型では書けない**」ことを保証し、「**内容が変わらない**」ことは保証しません。 この 2 つは別の保証であり、**型で保証できるのは前者だけ**です。 後者を得る入口が `EsReadOnlyBitmap.FromBitmap` です。

| 保証 | 何を守るか | 誰が守るか | 手段 |
| --- | --- | --- | --- |
| **この型では書けない** | 受け取った側（呼び出し先）が画素を書き換えない | **コンパイラ** | 読み取り専用の型に書き込みの入口が無い（`EsReadOnlyBitmap` に `SetPixel` 等が存在しない） |
| **内容が変わらない** | 受け取った側が読んだ画素が後から変わらない | **実行時** | `EsReadOnlyBitmap.FromBitmap` を通す（**凍結済みなら 0 コピーで共有・未凍結なら複製**） |

- **前者は 1 つの型では得られません**: 型が 1 つだけだと、受け取った側が「書かない」ことを**コンパイラは何も見ていない**ため、守っているかどうかは実装を読むまで分かりません（実行時のフラグで止めても、凍結前に書かれた分は静かに通ります）。 受け取る公開 API が約 100 箇所あるため、**入口で保証できる**ことの価値が大きいと判断しました。
- **後者は型では得られません**（C# は upcast を検出できないため）: 書き換え可能な参照を手放していない限り、別の箇所から書けます。 したがって**相手へ渡す入口は `FromBitmap` です**。

**相手へ渡す入口は `EsReadOnlyBitmap.FromBitmap`（Skia の `SKImage.FromBitmap` と同じ規則）**: 元の凍結状態で動作を分けます（実測: 凍結していない `SKBitmap` からは**複製**し、凍結済みなら**共有**します）。

| 元の状態 | 動作 | 費用（実測 4K） | 何が保証されるか |
| --- | --- | --- | --- |
| 凍結済み（`IsReadOnly`） | **0 コピーで共有** | ほぼ 0 | 誰も書けないため内容が変わらない |
| 未凍結 | **複製** | 31 MiB / 5.7 ms | 元の書き込みが届かない（元は書き換え可能なまま凍結されない) |

- **「その場で読むだけ」なら複製は不要です**: 保存・切り出し・縮小のように**読んでいる間に書き手がいない**用途は、書き換え可能な画像をそのまま渡せます（`Save` / `Crop` / `Resize` / `ReduceColors` は両方の型のオーバーロードを持つため、書き換え可能な画像でも複製も凍結も不要です。 §2.31）。 `FromBitmap` が要るのは**保持する・後で読む**場合です。
- **凍結してから渡せば 0 コピーで内容の安定も得られます**: 書き終えた時点で書き換え可能な型の `SetReadOnly` を呼ぶと、`FromBitmap` は共有だけを行います（凍結は画素領域の共有状態に効くため、同じ領域を指す実体はどれも書けなくなります）。
- **無条件に凍結する入口（読み取り専用として渡すと元が書けなくなる方法）は採用しません**: 渡した先で元の画像が書けなくなると、その後の編集（オーバーレイの追加等）ができません。 凍結は**書き終えた時点**で明示的に行います。

**画素ビューの名前（無印が書き込み可能・`ReadOnly` 付きが読み取り専用）**: `EsBitmap.Pixels` は `Span<byte>`（書き込み可能）、`EsReadOnlyBitmap.ReadOnlyPixels` は `ReadOnlySpan<byte>`（読み取り専用）です。 無印を書き込み可能なビューにするのは、**書き換え可能な型では書き込みが本題**であるためです（`Memory<T>` に対する `Span<T>` と同じ関係）。 読み取り専用のビューは**どちらの型にも同じ名前で置く**ため、**名前と返る型が一致します**（同じ名前で型だけが変わる取り違えが起きません）。

**読み取り専用であって不変ではない**: 本型が保証するのは「**この実体からは書けない**」ことであり、「内容が決して変わらない」ことではありません（上記「型が保証すること／保証しないこと」）。 書き換え可能な画像を**暗黙に**読み取り専用として渡すことはできません（兄弟のため型が違います）が、**借用した画素**（`InstallPixels`）は所有者が書き換えれば変わります。 **内容を固定したい場合は `EsReadOnlyBitmap.FromBitmap` / `ToPixmap` を使います**（`FromBitmap` は凍結済みなら 0 コピーで共有、未凍結なら複製）。

**凍結（`SetReadOnly` / `IsReadOnly`）が覆う範囲（実測）**: 凍結は「**その実体の画素領域**」に効きます。 覆う範囲と覆わない範囲を分けて整理します（覆わない範囲は**呼び出し側の責務**です）。

| 操作 | 凍結後の可否 | 備考 |
| --- | --- | --- |
| `Pixels`（書き込み可能なビュー）の**取得** | **例外**（`InvalidOperationException`） | 取得の時点で拒否する |
| `SetPixel` / `TrySetPixel` / `Clear` | **例外** | 更新系の入口はすべて拒否する |
| `ReadOnlyPixels` / `GetPixel` / `Width` 等の読み取り | 可能 | 凍結は読み取りを妨げない |
| 凍結の**前に取得したビュー**（`Span`） | 書ける | 取得時にしか判定できない（`Span` は raw なビューであり、取得後に無効化できない）。 Skia の `GetPixels` / `GetPixelSpan` も同じ |
| `Copy` / `Create` / `CreateFilled` / `Load` / `InstallPixels` / `FromPixelCopy` / `ToPixmap` | 可能 | **新しい実体（またはビュー）を作る入口**であり、凍結した実体の画素は変わらない |
| **同じメモリを `InstallPixels` で借用した別の実体** | 書ける | **凍結を迂回できる**（凍結は実体の画素領域ごとであり、生のメモリは呼び出し側のもの。 実測: 借用した実体の書き込みで凍結した実体の画素が変わった） |

- **「同じメモリの別実体」を作らないのが規則です**: 同じ画素を書き換えたい場合は `EsBitmap.FromReadOnlyBitmap`（複製）を使います。 借用（`InstallPixels`）は**外部が確保したメモリ**（他フレームワークのビットマップ・アンマネージドの領域）を渡すための入口であり、**本ライブラリの画像のアドレスを渡し直す用途には使いません**（凍結も読み取り専用の約束も迂回できてしまうため）。

**凍結（`SetReadOnly` / `IsReadOnly`）**: `SetReadOnly` を呼ぶと、以後の書き込みの入口（`Pixels` / `SetPixel` / `TrySetPixel` / `Clear`）が `InvalidOperationException` を投げます。 読み取り専用の型の `IsReadOnly` は常に true（書き込みの入口が無いため）、書き換え可能な型は凍結の状態を返します。

- **凍結後の書き込みを例外にする理由（実測に基づく）**: Skia の `SetImmutable` は**宣言のみで強制しません**。 実測では、凍結した `SKBitmap` から 0 コピーで共有した `SKImage` があるのに `SetPixel` が例外なく書けて**共有先の画素が変わり**、`GetPixelSpan` 経由の書き込みも届き、`InstallPixels` は**フラグ自体を false に戻しました**（不変の約束が守られなければ、不変を前提にした画像が静かに壊れます）。 本ライブラリは型（書き込みの入口が無い）と実行時（凍結後の**取得**を拒否）の二重で守ります。
- **「読み取り専用として渡す」と「凍結する」を分ける理由**: 渡すだけで凍結すると、生成した画像へ切り出し・保存を呼んだ後に書き続けられなくなります。 凍結は**書き終えた時点**で明示的に行います。

**共有と寿命（参照を数える）**: 同じ画素を指す複数の実体は、バッファの `SafeHandle` に参照を 1 つ取って解放します。 したがって**先に元を破棄しても画素は有効なまま**であり、最後の 1 つが解放します（二重 `Dispose` でも 2 回目は解放しません）。

- **`Copy` はどちらの型でも独立した画素を作ります**: 複製の費用は実測で 4K 1 枚あたり 31 MiB と 5.7 ms です。 名前を `Clone` にしない理由は、`Clone` が「安い複製」を期待させるためです（旧設計では読み取り専用側の `Clone` が 0 コピーの共有、書き換え可能側が 31 MiB の複製であり、**同じ名前が逆の費用**を意味していました。 Skia も `SKBitmap.Copy` が複製で、共有は `SKImage.FromBitmap` と名前が別です）。
- **0 コピーの共有は名前を分けます**: 書き換え可能な画像を凍結して共有する場合は `EsReadOnlyBitmap.FromBitmap`（凍結済みなら 0 コピー）、切り出しを複製せずに読む場合は `EsReadOnlyBitmap.ToPixmap` を使います。

**借用と複製を名前で分けます**: 画素を既に持っている場合（他フレームワークのビットマップ・アンマネージドの領域・共有メモリ）は、複製の有無を名前で選べます。

| 入口 | 複製 | 用途 |
| --- | --- | --- |
| `EsReadOnlyBitmap.InstallPixels(w, h, pixels, release = null)` | **しない**（借用） | 既にある画素を読むだけの場面 |
| `EsReadOnlyBitmap.FromPixelCopy(w, h, pixels)` | **する** | 元の画素が後で変わってよい場面 |
| `EsBitmap.InstallPixels(w, h, pixels, release = null)` | **しない**（借用） | 既にある画素を書き換える場面 |

- **`release` は解放のコールバック**です（借用した領域を破棄時に解放したい場合に渡します。 渡さない場合は呼び出し側が自分のタイミングで解放します）。 **解放の実装は `SafeBorrowedBuffer` が単一所有する**ため、`Dispose` でも GC でも 1 回だけ呼ばれます。
- **行パディングがある領域は借用できません**: 画素は連続した 1 本の領域として公開するため、ストライドは `幅 × 4` に限ります（それ以外は `ArgumentException`）。
- **借用中は内容の不変を保証しません**: 借用は「その時点の画素を読む（書く）」入口であり、所有者が書き換えれば内容は変わります（読み取り専用の約束は「この実体からは書けない」ことだけです）。

**変換は向きごとに 1 つです**: 読み取り専用 → 書き換え可能 は `EsBitmap.FromReadOnlyBitmap`（**常に複製**。 共有したまま書き換えると元の画素が変わるため）、書き換え可能 → 読み取り専用 は `EsReadOnlyBitmap.FromBitmap`（**凍結済みなら 0 コピー・未凍結なら複製**）です。 **兄弟であるため無料の upcast はありません**（未凍結の画素が「変わらない」ものとして渡ることを型で防ぐための代償です）。

**型の比較表（対称。 書き込みに関するものだけが書き換え可能な型にあります）**:

| 種別 | メンバー | `EsReadOnlyBitmap` | `EsBitmap` | 説明 |
| --- | --- | --- | --- | --- |
| 定数 | `BytesPerPixel` | あり | あり | 1 ピクセルあたりのバイト数（4。 定義は内部のバッファ型が単一所有） |
| プロパティ | `Width` / `Height` | あり | あり | 幅 / 高さ（ピクセル） |
| プロパティ | `Size` | あり | あり | 幅・高さ（`EsSizeInt`） |
| プロパティ | `Stride` | あり | あり | 1 ラインあたりバイト数（常に 幅 × 4。 パディングなし） |
| プロパティ | `ReadOnlyPixels` | あり | あり | 読み取り専用ビュー（`ReadOnlySpan<byte>`。 コピーしない。 破棄後は `ObjectDisposedException`） |
| プロパティ | `IsDisposed` | あり | あり | 破棄済みか |
| プロパティ | `IsReadOnly` | あり（**常に true**） | あり（凍結の状態） | この実体から書けないか |
| プロパティ | **`Pixels`** | **なし** | **あり** | **書き込み可能なビュー**（`Span<byte>`。 凍結後は `InvalidOperationException`） |
| ピクセル | `GetPixel` | あり | あり | 画素の取得（`(byte B, byte G, byte R, byte A)` / `Color`）。 範囲外は `ArgumentOutOfRangeException` |
| ピクセル | **`SetPixel`** | **なし** | **あり** | **画素の設定**（範囲外は `ArgumentOutOfRangeException`） |
| ピクセル | **`TrySetPixel`** | **なし** | **あり** | **画素の設定**（**範囲外は `false`**。 端をはみ出す矩形を描く用途） |
| ピクセル | **`Clear`** | **なし** | **あり** | **全ピクセルを指定色で塗りつぶす** |
| その他 | **`SetReadOnly`** | **なし** | **あり** | **凍結する**（以後の書き込みの入口が `InvalidOperationException`。 画素領域の共有状態に効く） |
| その他 | `ToSizeText` | あり | あり | 寸法を `幅x高さ` 形式で返す（書式は本面が単一所有） |
| その他 | `ThrowIfDisposed` | あり | あり | 破棄済みなら `ObjectDisposedException` を送出 |
| その他 | `Dispose` | あり | あり | 画素領域の解放（冪等） |
| 複製 | `Copy` | あり（`EsReadOnlyBitmap`） | あり（`EsBitmap`） | **独立した画素**の複製（実測: 4K で 31 MiB / 5.7 ms） |
| 操作 | `Crop`（2 版） | あり（`EsBitmap`） | あり（`EsBitmap`） | 指定矩形を切り出す（**ソース範囲外は透明で埋める**。 実装は内部の 1 か所） |
| 操作 | `Resize` | あり（`EsBitmap`） | あり（`EsBitmap`） | 長辺を上限へ収める（**拡大は未サポート**。 品質 `ImageQuality` で速度と画質を選ぶ） |
| 操作 | `ReduceColors` | あり（`EsBitmap`） | あり（`EsBitmap`） | 指定の色数へ減色する（パレット化。 **不可逆**。 品質 `ImageQuality` と `DitherMode` を選ぶ） |
| ビュー | `ToPixmap` | あり（`EsPixmap`） | あり（`EsPixmap`） | **0 コピーの読み取りビュー**（行幅は元のストライドのまま。 矩形は画像の内側であること） |
| ファイル | `Save`（2 版） | あり | あり | ファイルへ保存する（**形式は拡張子が決める**。 2 型とも同じ手順。 Windows 専用） |
| static | `SupportedFileExtensions` | あり | あり | 保存に使える拡張子（Windows 専用） |
| static | `Load` | あり（`EsReadOnlyBitmap`） | あり（`EsBitmap`） | ファイルから読み込む（中間バッファを作らない） |
| static | `Create`（4 版） | あり（`EsReadOnlyBitmap`） | あり（`EsBitmap`） | 指定サイズの画像（全ピクセル透明 / 指定色。 0 埋めする） |
| static | `CreateFilled` | あり（`EsReadOnlyBitmap`） | あり（`EsBitmap`） | 確保と**充填**を 1 回で行う（相互変換が使う） |
| static | `InstallPixels` | あり（`EsReadOnlyBitmap`） | あり（`EsBitmap`） | 呼び出し側の画素を**借用**する（複製しない） |
| static | `FromPixelCopy` | あり（`EsReadOnlyBitmap`） | あり（`EsBitmap`） | `ReadOnlySpan<byte>` から**複製**する |
| static | `FromBitmap` | あり（`EsReadOnlyBitmap`） | なし | 書き換え可能 → 読み取り専用（**凍結済みなら 0 コピー・未凍結なら複製**） |
| static | `FromReadOnlyBitmap` | なし | あり（`EsBitmap`） | 読み取り専用 → 書き換え可能（**常に複製**） |
| static | `FromPixmap` | あり（`EsReadOnlyBitmap`） | あり（`EsBitmap`） | ビュー → 画像（**常に複製**。 ビューの行幅を考慮して行ごとに写す） |

- **対称の規則**: 2 つの型は**同じ名前・同じ意味のメンバー**を持ちます（`なし` は書き込み不可能であることを表します）。 **書き込みに関するものだけ**（`Pixels` / `SetPixel` / `TrySetPixel` / `Clear` / `SetReadOnly`）が書き換え可能な型にあります。 **メンバーの並び順も同一**であり、書き込みのメンバーを足す位置だけが違います（並び順の規則は [`.github/コーディング規約.md`](../../../.github/コーディング規約.md) §5 の「メンバーの並び順」が単一所有します）。
- **変換の向きだけは型ごとに 1 つです**: `FromBitmap`（読み取り専用の型）と `FromReadOnlyBitmap`（書き換え可能な型）は、それぞれ「もう一方の型から自分を作る」入口です（実測: Skia も `SKImage.FromBitmap` / `SKBitmap.FromImage` と同じ形です）。 **役割は同じではありません**（前者は凍結状態で 0 コピーと複製を選び、後者は常に複製します）。
- **読み取り専用の型にも `Create` を置く理由**: 相互変換とテストが**どちらの権利の画像も作る**ためです（片方だけ別の書き方になると、変換を読むたびに「なぜ違うのか」を調べることになります）。 透明の画像は読み取り専用では書き込めないため用途は限られますが、`Create(w, h, color)` と `CreateFilled` はそのまま使えます。
- **`CreateFilled` と `InstallPixels` は引数以外も違います**（どちらも「画素を持つ画像を作る」入口ですが、役割が別です）:

| 観点 | `CreateFilled` | `InstallPixels` |
| --- | --- | --- |
| 所有権 | **自分が確保して所有する**（`Dispose` で解放。 GC でも解放される） | **借りるだけ**（解放しない。 `release` は破棄時に 1 回だけ呼ぶ通知） |
| メモリ | 内部で確保する（64 バイト整列） | 呼び出し側が確保済みの領域を指す |
| 初期化 | **充填コールバックで全領域を書く**（0 埋めを省く） | 既にある内容を**そのまま使う** |
| 失敗時 | 充填が例外なら確保した領域を解放する | 引数の検証のみ（外部の領域に触れない） |
| 費用 | 確保 + 充填 | ほぼ 0（**ゼロコピー**） |
| 用途 | 相互変換（他ライブラリの画素をコピーして取り込む） | 既に確保された画素を複製せずに扱う（Skia の `SKBitmap.InstallPixels` と同じ） |

**`EsPixmap`（0 コピーの読み取りビュー）のメンバー**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| プロパティ | `Width` / `Height` | `int` | ビューの幅 / 高さ（ピクセル） |
| プロパティ | `Size` | `EsSizeInt` | ビューの幅・高さ（ピクセル） |
| プロパティ | `Stride` | `int` | **元画像のストライドのまま**（`幅 × 4` より大きい場合がある。 名前を画像と同じ `Stride` にするのは「1 ラインあたりのバイト数」が同じ概念であるため） |
| プロパティ | `ReadOnlyPixels` | `ReadOnlySpan<byte>` | 先頭から `Stride × Height` バイトのビュー（**行の余白を含む**） |
| ピクセル | `GetPixel` | `(byte B, byte G, byte R, byte A) GetPixel(int x, int y)` / `Color GetPixel(EsPointInt point)` | 画素の取得（**行幅を考慮する**。 ビューの左上が 0, 0）。 範囲外は `ArgumentOutOfRangeException` |
| 操作 | `Resize` | `EsBitmap Resize(int maxSize, ImageQuality quality = Mid)` | ビューを縮小した画像を作る（**切り出しの実体化を挟まない**。 実測: 1000x800 の切り出し → 長辺 320 の `ImageQuality.High` で **3.3 倍**） |
| 操作 | `ReduceColors` | `EsBitmap ReduceColors(int colorCount, ImageQuality quality = Mid, DitherMode dither = None)` | ビューを減色した画像を作る（**切り出しの実体化を挟まない**。 実測: 切り出しの 0.85 ms / 4 MB が不要 ＝ 約 12%） |
| ビュー | `ToPixmap` | `EsPixmap ToPixmap(EsRectInt rect)` | ビューの一部をさらに 0 コピーで切り出す |
| その他 | `ToSizeText` | `string ToSizeText()` | 寸法を `幅x高さ` 形式で返す |
| その他 | `IsDisposed` / `ThrowIfDisposed` / `Dispose` | `bool` / `void` / `void` | 破棄状態の判定と、元の画素への参照の返却（**画素は破棄しない**。 冪等） |

- **生成しない（入口は `EsReadOnlyBitmap.ToPixmap` / `EsBitmap.ToPixmap`）**: ビューは画像の一部を指すため、画素を持つ型から作ります。
- **書き込みの入口を持ちません**: 元の画素を書き換える場合は `EsBitmap.FromReadOnlyBitmap` で複製するか、元の書き換え可能な画像を使います。
- **操作（縮小・減色）を持つ理由**: ビューは**画素の読み取りだけ**を要する操作の対象になれます。 先に画像から切り出すと**実体化の複製**（行コピーと透明パディング）を払いますが、ビューをそのまま渡せばその分が要りません（縮小は 3.3 倍、減色は約 12% の差）。 新しい画像を作る操作であり、ビュー自体は変更しません。
- **保存は持ちません**: 保存は画素をエンコードへ渡すため、行の余白を扱える符号化が必要です（現在のエンコーダーは密な領域を前提とするため、`FromPixmap` で画像にしてから `Save` を呼びます）。
- **寿命**: ビューは元の画素の参照を 1 つ持つため、**先に元を破棄しても読めます**（ただし元の内容の変化は見えます）。
- **画像にする場合**: `EsBitmap.FromPixmap` / `EsReadOnlyBitmap.FromPixmap`（**常に複製**）を使います。 `FromPixelCopy` に `ReadOnlyPixels` をそのまま渡すと**長さが合わず** `ArgumentException` になります（行の余白を含むため）。
- **ビューを渡せる相手**: コアの操作（`Crop` / `Resize` / `ReduceColors`）は**行幅を受け取る**ため、**行の余白を含むビューをそのまま渡せます**（実装は行単位に読み書きし、余白の画素を結果へ混ぜません。 実際に余白付きのソースで結果が変わらないことをテストで固定しています）。 外部の走査へ渡す場合は**行幅を一緒に渡してください**（密であることを前提にした走査は余白を画素として扱います）。
**画像を変更せずに新しい画像を作る操作（`Crop` / `Resize` / `ReduceColors`）と保存（`Save`）**:

**2 型が同じ名前のメンバー**として持ちます（実装は内部の 1 か所であり、型の違いは書き込みの入口だけです）。

| メンバー | シグネチャ（`EsReadOnlyBitmap` と `EsBitmap` の同じ形） | 説明 |
| --- | --- | --- |
| `Crop` | `EsBitmap Crop(int x, int y, int width, int height)` / `EsBitmap Crop(EsRectInt rect)` | 指定矩形を切り出す（**ソース範囲外は透明で埋める**。 位置関係は保たれる。 0 初期化は行わない） |
| `Resize` | `EsBitmap Resize(int maxSize, ImageQuality quality = Mid)` | 長辺を `maxSize` へ収める（**拡大は未サポート**。 速度と画質は `ImageQuality`（`Low` / `Mid` / `High`）で選ぶ） |
| `ReduceColors` | `EsBitmap ReduceColors(int colorCount, ImageQuality quality = Mid, DitherMode dither = None)` | 指定の色数へ減色する（パレット化。 **不可逆**。 品質とディザを選べる） |
| `Save` | `void Save(string filePath)` / `void Save(string filePath, EsImageSaveOptions options)` | ファイルへ保存する（[2.32](#232-画像のファイル入出力save--load)） |

- **同じ実装を 2 型のメンバーとして公開します（拡張メソッドにしません）**: 実装は内部の 1 か所（`EsBitmapBuffer` / `EsBitmapOperations`）に置き、公開の入口は**両方の型のメンバー**にします。 拡張メソッドにすると、**同じ操作が型の外へ 2 組並び**、型自身の能力（`Copy` / `GetPixel` / `Load` 等）と置き場所が分かれます（IntelliSense でも 1 か所にまとまりません）。 **共通の面（interface）は作りません**（復活させると、書き換え可能な画像を読み取り専用の面として**保持・後読みできる経路**ができ、兄弟にした理由（[2.33](#233-画像型の設計上の判断兄弟と借用を選んだ理由)）が崩れます）。
  - **代償**: 宣言が型ごとに必要です（**実装は 1 つのまま**）。 それぞれの型の `Crop` / `Resize` / `ReduceColors` / `Save` は内部の実装へ転送するだけであり、規則の記述（備考）は読み取り専用側（`EsReadOnlyBitmap`）が単一所有します。
- **書き換え可能な画像を渡しても凍結しません**: 画素を読むだけの操作であるためです（`Save` / `Crop` / `Resize` / `ReduceColors` の後も書き続けられます）。 0 コピーで安定した内容を渡したい場合は、書き終えた時点で `SetReadOnly` を呼びます。
- **戻り値は書き換え可能な画像**です（生成した側が所有し、破棄します）。 読み取り専用で足りる場合は `Copy`（複製）または `ToPixmap`（0 コピーのビュー）を使います。
- **切り出しは 2 通りあります**: 画像の外側を含み**透明で埋めたい**場合は `Crop`（複製）、**内側だけ**を複製せずに読みたい場合は `ToPixmap`（0 コピーのビュー）です。

**他フレームワークの画像型との比較（一次資料と実測。 速度の比較は [2.34](#234-画像操作の他ライブラリとの比較実測) が単一所有します）**:

| 観点 | 本ライブラリ | Skia（`SKImage` / `SKBitmap`） | Avalonia | WinUI 3（WinAppSDK） | Win2D（`CanvasBitmap`） | System.Drawing |
| --- | --- | --- | --- | --- | --- | --- |
| 読み取り専用の表し方 | **兄弟**（互いに派生しない） | **兄弟型**（共通の親は `SKObject` のみ） | **継承**（`WriteableBitmap : Bitmap`） | **継承**（`WriteableBitmap : BitmapSource`）＋**単一型**（`SoftwareBitmap` はロックモードで切替） | 単一型（読み書き両方） | **継承**（`Bitmap : Image`） |
| 不変の表明 | `SetReadOnly()` / `IsReadOnly`（**凍結後の「取得」は例外**） | `SetImmutable()` / `IsImmutable`（**宣言のみで強制しない**。 実測: 凍結後も `SetPixel` が書けて共有先の画素が変わり、`GetPixelSpan` の書き込みも届き、`InstallPixels` はフラグを false に戻す） | 無し（`Bitmap` に書き込みの入口は無いが「不変」の表明ではない） | 型（`BitmapSource`） | 無し | 無し |
| ピクセルへの書き込み | `Pixels`（`Span<byte>`） | `SKBitmap.GetPixelSpan()` | `Lock()` → `ILockedFramebuffer`（**借用のオブジェクト**） | `WriteableBitmap.PixelBuffer`（`IBuffer`。 直接は書けない） | `SetPixelBytes`（**常に複製**） | `LockBits` |
| 外部メモリの借用の入口 | `InstallPixels`（両方の型に同じ名前）。 複製は `FromPixelCopy` と**名前で区別** | `InstallPixels`（複製の `FromPixelCopy` と**名前で区別**。 実測: `PeekPixels` が元アドレスを指す） | `WriteableBitmap(PixelFormat, AlphaFormat, IntPtr, …)` | `SoftwareBitmap.LockBuffer()` → `BitmapBuffer` → `CreateReference()`（`IDisposable` の連鎖） | 無し | `Bitmap(IntPtr, …)` |
| 読み取り専用 → 書き換え可能 | **複製**（実測: 4K で 31 MiB / 5.7 ms） | 複製（`SKBitmap.FromImage` は実測で別アドレス。 凍結済みなら逆向きの共有が 0 コピー） | 複製（静的変換も無く、`WriteableBitmap` を作って `CopyPixels`） | **即時の複製**（`CreateCopyFromBuffer` / `Convert`。 メソッド名が複製を明示） | 複製 | 複製（`Clone`） |
| 生ポインタの公開 | 無し（`ReadOnlyPixels` / `Pixels` を `fixed` で取る） | 有り（`GetPixels()`） | 有り（`ILockedFramebuffer.Address`） | 無し | 無し（`byte[]` のみ） | 有り（`Scan0`） |
| 0 コピーの切り出し | `ToPixmap`（**0 コピー**。 行幅は元のストライドのまま。 縮小・減色の対象にもなる） | `SKBitmap.ExtractSubset`（実測 **0.000 ms** のビュー。 縮小の `SKBitmap.ScalePixels` / `SKPixmap.ScalePixels` も併存する）/ `SKImage.Subset`（実測 0.755 ms の複製） | `CroppedBitmap`（実測 0.001 ms のビューだが**元の破棄で無効**） | 無し | 無し | `Clone(部分矩形)` は複製 / `Clone(全域)` はビュー（7.79 ms で実体化） |
| ピクセル形式の自由度 | **連続 BGRA8888・Premul に固定**（借用も連続のみ） | 任意の `RowBytes`・多数の `ColorType` / `AlphaType` | 数種 | 多数 | 多数 | 多数 |
| 破棄 | `IDisposable`（両方の型） | `SKObject : IDisposable` | `IDisposable`（`Bitmap` が宣言） | `SoftwareBitmap : IClosable`（表示型は GC） | `IDisposable` | `Image : IDisposable` |

- **出典**: SkiaSharp / Avalonia / System.Drawing は各アセンブリで実測（リフレクション＋アドレス比較）。 WinUI 3 / Win2D は Microsoft Learn のクラスリファレンス（`WriteableBitmap` / `SoftwareBitmap` / `CanvasBitmap` / `CanvasRenderTarget`）と Windows App SDK のイメージング解説です。
- **借用と複製を名前で分ける点は Skia と同じです（優劣ではなく一致）**: `InstallPixels`（借用）/ `FromPixelCopy`（複製）という名前の付け方は Skia に合わせました（**両方の型で同じ名前**にしています）。 本アプリの描画は MewUI（Skia）であり、MewUI 用の表示ビューは読み取り専用の画像を `ToSKBitmap()`（**非所有ビュー。 複製なし**）で `SKCanvas.DrawBitmap` へ渡します。
- **読み取り専用の表し方は Skia と同じです（兄弟型）**: 本ライブラリも互いに派生しない 2 型にし、変換は `FromBitmap` / `FromReadOnlyBitmap` の 2 つだけにしました（実測: Skia は `SKImage.FromBitmap` / `SKBitmap.FromImage`）。 **兄弟にした理由**は §2.33 を参照（継承だと未凍結の画素を読み取り専用として暗黙に渡せてしまうため）。
- **遅延コピー（読み取り専用の画像と画素を共有し、最初の書き込みで複製する仕組み）は持ちません**: 調査したどのフレームワークも持たず、本ライブラリでも**本番の呼び出しがありませんでした**（画像から書き換え可能な画像を作る `EsBitmap.FromReadOnlyBitmap` を渡すのはテストだけであり、本番は寸法と色で生成していました）。 0 コピーの共有は凍結と `FromBitmap`（凍結済みなら共有・未凍結なら複製）で得られるため、書き込み可能なビューを渡す入口（`Pixels`）と組み合わせると**いつ複製されたか分からない隠れた確保**になるだけでした（[2.33](#233-画像型の設計上の判断兄弟と借用を選んだ理由)）。
- **凍結は Skia の `SetImmutable` と同じ役割ですが、実行時に強制します**: Skia の凍結は呼び出し側の**宣言**であり（実測: 凍結後も `SetPixel` が例外なく書けて共有先の画素が変わり、`GetPixelSpan` の書き込みも届き、`InstallPixels` はフラグを false に戻す）、守られなければ不変を前提にした画像が静かに壊れます。 本ライブラリは凍結後の**取得**を拒否します（凍結の前に取得したビューは残ります。 これは Skia と同じ原理的な限界です）。
- **借用の入口を公開する点は WinUI 3 / Win2D と違います**: 借用は**借用先の寿命に依存する契約**を増やします（実測: Avalonia の表示用のビューは元の破棄で無効化されます）が、複製の費用（実測: 4K で 31 MiB / 5.7 ms）を避ける手段として有効なため、**名前で複製と区別したうえで公開します**。
- **書き込みの期間は型で表しません**: Avalonia の `Lock()` は借用オブジェクトの寿命で「借用している間」を表しますが、本ライブラリは `Pixels`（`Span<byte>`）を返します。 `Span` は `ref struct` のため**フィールドに保持できません**が、ローカルに置いたまま破棄すれば不正な書き込みになる点は Avalonia と同じです（借用中の破棄を型で防ぐ手段は、調査したどのフレームワークにもありません）。
- **Skia の `SKImage` が持つ能力（遅延デコード・GPU テクスチャ・ゼロコピーの部分ビュー）は持ちません**: 実測では `SKImage.FromEncodedData` は寸法だけなら**画素を 1 バイトも持たず**（`PeekPixels` が null、ワーキングセットの増分 0 B）、`SKBitmap.Decode` は 48 MB を要します（4000x3000）。 本ライブラリの画像は**常に CPU 上のフル バッファ**であり、2 型の違いは権利の表明だけです（メモリと速度は同一）。 遅延デコードが要る場面は、読み込みを遅らせる側（呼び出し側）で行います。

### 2.32 画像のファイル入出力（`Save` / `Load`）

コアの画像のファイル保存・読み込みです。 **公開面は画像型のメンバー（`Load` は静的・`Save` はインスタンス。 2 型が同じ名前を持ちます）**であり、GDI+（`gdiplus.dll`）を呼ぶ実装は `src/EsUtil.Platform.Core/src/Native/Win/WinGdiPlusImageCodec.cs` の `internal` クラスが単一所有します（入口は内部の `EsBitmapBuffer.LoadFromFile` / `SaveToFile`）。 **Windows 専用**です。

| 型 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| `EsReadOnlyBitmap` / `EsBitmap` | `Save` | `void Save(string filePath)` | 画像をファイルへ保存する（**形式は拡張子が決める**。 2 型が同じ手順で保存する） |
| `EsReadOnlyBitmap` / `EsBitmap` | `Save` | `void Save(string filePath, EsImageSaveOptions options)` | 保存の形式とオプションを指定して保存する（**形式の指定は拡張子より優先され、拡張子が無ければ補完する**。 下記） |
| `EsReadOnlyBitmap` / `EsBitmap` | `SupportedFileExtensions` | `static IReadOnlyList<string> SupportedFileExtensions { get; }` | 保存に使える拡張子（`.bmp` / `.png` / `.jpg` / `.jpeg` / `.gif` / `.tif` / `.tiff`） |
| `EsReadOnlyBitmap` | `Load` | `static EsReadOnlyBitmap Load(string filePath)` | ファイルから**読み取り専用の**画像を読み込む（形式は内容から判別する。 所有権は呼び出し側へ移る） |
| `EsBitmap` | `Load` | `static EsBitmap Load(string filePath)` | ファイルから**書き換え可能な**画像として読み込む（所有権は呼び出し側へ移る） |
| `EsImageFileFormat` | （enum） | `Bmp` / `Png` / `Jpeg` / `Gif` / `Tiff` | 保存する形式（GDI+ のエンコーダーと 1 対 1。 通常は拡張子で足りるため、拡張子が無い場合と拡張子と違う形式で書きたい場合だけ使う） |
| `EsImageSaveOptions` | `Format` | `EsImageFileFormat? Format { get; init; }` | 保存する形式（`null` は拡張子が決める。 指定した場合は**拡張子より優先**し、拡張子が無ければ補完する） |
| `EsImageSaveOptions` | `IgnoreFormatMismatch` | `bool IgnoreFormatMismatch { get; init; }` | 拡張子と `Format` の食い違いを無視するか（既定 false = 例外）。 不変の `readonly record struct` であり、部分更新は `with` 式で行う |
| `EsImageSaveOptions` | `JpegQuality` | `int? JpegQuality { get; init; }` | JPEG の品質（0〜100。 `null` は GDI+ の既定） |
| `EsImageSaveOptions` | `ColorCount` | `int? ColorCount { get; init; }` | 減色（パレット化）して残す色数（2〜256。 `null` は減色しない。 **BMP / PNG / GIF だけ**） |
| `EsImageSaveOptions` | `ColorQuality` | `ImageQuality? ColorQuality { get; init; }` | 減色の品質（`null` は `ImageQuality.Mid`。 **減色する場合だけ指定できる**） |
| `EsImageSaveOptions` | `Dither` | `DitherMode? Dither { get; init; }` | 減色のディザ（`null` は `DitherMode.None`。 **減色する場合だけ指定できる**） |
| `ImageQuality` | （enum） | `Low` / `Mid` / `High` | 縮小と減色の速度と画質の釣り合い（`Low` = 最速・最低画質、`Mid` = 既定、`High` = 最高画質。 段階の意味は操作ごとに決まる） |
| `DitherMode` | （enum） | `None` / `FloydSteinberg` | 減色のディザ（誤差拡散。 既定は `None` = ファイルが最も小さい） |

- **`Save` は 2 型のメンバーです（拡張メソッドにしません）**: 保存は**ピクセルを読むだけ**であり、書き換えの権利も要らないため実装は内部の 1 か所に置きますが、公開の入口は**両方の型のメンバー**にします（`Load` と同じ型にファイル入出力が並び、型自身の能力として IntelliSense に現れます。 置き場所の規則は §2.31）。 **保存は凍結しません**（書き換え可能な画像を保存した後も書き続けられます）。
- **保存に使える形式の一覧は画像型が公開します**: 形式を決める規則の単一所有は `EsReadOnlyBitmap.SupportedFileExtensions`（`EsBitmap` も同じ値を公開）であり、保存の実装がそこを参照します（一覧は型の能力であり、特定の実体を要さないため）。

- **`Load` は具体型の静的メソッドに置きます**: 戻したい型（読み取り専用 / 書き換え可能）を**呼び出し側が名指す**必要があるためです（静的メソッドは戻り値の型を共変にできないため、`EsReadOnlyBitmap.Load` と `EsBitmap.Load` の 2 つが要ります）。 書き換えが要る場合は `EsBitmap.Load` を使ってください（`EsReadOnlyBitmap.Load` の結果を `EsBitmap.FromReadOnlyBitmap` で複製すると、2 回目の複製が無駄になります）。 つまりファイル入出力の見え方は「**読むときは型を名指す（`Load`）／書くときはどちらの型でも同じ（`Save`）**」です。
- **`Save` は再び画像型のメンバーになりました**: いったん拡張メソッドへ移しましたが、その理由（読み取り専用の面を実装する外部の画像からもキャストせずに呼べること）は**面（interface）の廃止で消えました**。 いまは実装を内部の 1 か所（`EsBitmapBuffer.SaveToFile`）に置き、公開の入口を両方の型のメンバーにしています（`Load` と同じ型にファイル入出力が並びます）。
- **`Crop` / `Resize` / `ReduceColors` も同じ形にしました**: 実装は内部の 1 か所（`EsBitmapOperations`）に置き、公開の入口は両方の型のメンバーです（規則と理由は §2.31）。 これにより型の外に並ぶ操作は無くなり、`Copy` / `ToPixmap` と同じ「型自身の能力」として読めます。
- **実装は `internal` に隠します**: 形式の判別（拡張子 → エンコーダー）・GDI+ の呼び出し・状態値の例外化は 1 か所（「`WinGdiPlusImageCodec`」）が単一所有し、公開面（`Save` / `Load` / `SupportedFileExtensions`）はそこへ委譲するだけです。
- **実装の入出力は画像型を知りません**: 符号化は受け取ったピクセル バイト列を書き（`EncodeToFile`）、復号は**画像の生成処理を受け取ってデコード結果をその領域へ直接書きます**（`DecodeFromFile<T>`）。 コーデックが画像型（`EsBitmap` 等）を名指ししないため、画像型が要るのは公開面だけになります。 **中間バッファも追加のコピーも作りません**（実測: 4K PNG の読み込みで 29.9 ms。 中間バッファを挟む方式は 32.6 ms ＝ **+2.7 ms / +8%** と +31 MiB。 保存は両方式とも 66.5 ms で差なし）。
- **形式は既定では拡張子が決めます**: `Save` は拡張子から形式を選びます（`.bmp` = Windows Bitmap / `.png` / `.jpg` / `.jpeg` / `.gif` / `.tif` / `.tiff`）。 大文字・小文字は区別しません。
- **形式を明示する入口を持ちます（拡張子の補完を含む）**: `EsImageSaveOptions.Format` を指定すると形式が優先されます。 指定しなかった場合は従来どおり拡張子が形式を決めます（**既存の呼び出しの挙動は変わりません**）。

  | `Format` | 拡張子 | `IgnoreFormatMismatch` | 結果 |
  | --- | --- | --- | --- |
  | 未指定 | 対応形式 | — | 拡張子の形式で保存する（従来どおり） |
  | 未指定 | 無い・未対応 | — | `NotSupportedException`（従来どおり） |
  | 指定 | 無い | — | **形式の標準拡張子を補完して保存**する（`shot` + `Png` → `shot.png`） |
  | 指定 | 一致 | — | そのまま保存する |
  | 指定 | 不一致 | false | `ArgumentException`（**ファイルは作りません**） |
  | 指定 | 不一致 | true | **指定形式で**与えられたファイル名へ保存する（拡張子と中身が食い違う） |
  | 指定 | 未対応 | false | `NotSupportedException`（拡張子が対応形式でない場合と同じ） |
  | 指定 | 未対応 | true | 指定形式で与えられたファイル名へ保存する |

  - **補完に使う拡張子は 1 つに固定します**（`.bmp` / `.png` / `.jpg` / `.gif` / `.tif`）。 別名（`.jpeg` / `.tiff`）は受け付けますが補完には使いません（同じ形式に 2 つの候補があると、呼び出し側が保存先を予測できないため）。
  - **食い違いを既定で例外にする理由**: どちらが呼び出し側の意図か読めないためです。 例外にしないと、拡張子を直し忘れたまま別形式で保存され、**拡張子と中身が食い違ったファイルが黙ってできます**。 `IgnoreFormatMismatch` は「承知の上で食い違わせる」ことを明示する入口です。
  - **どちらの値も GDI+ の初期化より前に判定します**（拒否される呼び出しはプロセス全体の副作用を起こしません）。
- **未対応の拡張子は例外にします**: PNG 等へ黙って差し替えると、呼び出し側が意図した形式と違うファイルができて気付かないためです（`NotSupportedException`。 対応形式の単一所有は `EsReadOnlyBitmap.SupportedFileExtensions`）。
- **保存のオプションを指定できます（実測）**: `Save(string, EsImageSaveOptions)` は指定した値だけを GDI+ のエンコーダー パラメーター（減色は自前の計算）として渡します。 1280x720 の画像（一様 / なだらか / 不規則）を各値で保存し、ファイル サイズと復号した画素を比較した結果です。

  | オプション | 内容 | 一様（単色） | なだらか | 不規則（疑似乱数） |
  | --- | --- | --- | --- | --- |
  | `JpegQuality` = 0 | 最低品質 | 15,026 B | 15,259 B | 42,597 B |
  | `JpegQuality` = 50 | | 15,028 B | 17,867 B | 378,312 B |
  | `JpegQuality` = 75 | | 15,028 B | 30,947 B | 554,267 B |
  | `JpegQuality` = 90 | | 15,029 B | 39,433 B | 791,497 B |
  | `JpegQuality` = 100 | 最高品質 | 15,031 B | 195,783 B | **1,821,653 B** |
  | （オプションなし） | GDI+ の既定 | 15,029 B | 44,273 B | 826,483 B |

  - **既定は明示した品質と一致しません**: 既定は不規則な画像で 826,483 B（品質 90 の 791,497 B と 100 の 1,821,653 B の間）であり、**品質 75 とは 1.5 倍違います**。 したがって「既定のまま」と「品質 75 を明示」は別物です。 調整しない場合は `Save(string)` を使ってください。
  - **単色の画像では効きません**: 変化の乏しい画像はどの品質でも同じように圧縮されます（15,026 B 〜 15,031 B）。
- **GDI+ のエンコーダー パラメーターを全通り試した結果（実測。 640x360 の不規則な画像）**: 「JPEG 以外にも品質・圧縮率のオプションがあるのでは」という疑問に答えるため、**`Encoder` の全 13 パラメーター × 全形式**を総当たりし（`EncoderParameters` を自前で組み立てて `GdipSaveImageToFile` へ直接渡す。 型 `Long` / `Short[64]` を明示）、出力のバイト列のハッシュで判定しました。

  | パラメーター（試した値） | BMP | PNG | GIF | JPEG | TIFF |
  | --- | --- | --- | --- | --- | --- |
  | `Quality`（0〜200） | 無効 | 無効 | 無効 | **有効**（0 = 11,436 B 〜 100 = 458,991 B。 101 以上は 100 と同じ） | 無効 |
  | `LuminanceTable` / `ChrominanceTable`（short[64]） | 無効 | 無効 | 無効 | **有効**（208,287 → 148,295 / 192,531 B） | 無効 |
  | `Compression`（0〜10 / 32773） | 無効 | 無効 | 無効 | 全部**失敗**（状態 2） | **一部有効**（2 = LZW / 3 = CCITT3 / 4 = CCITT4 / 5 = RLE / 6 = 無圧縮。 他は状態 2 で失敗） |
  | `ColorDepth`（1〜64） | 無効 | 無効 | 無効 | 無効 | **一部有効**（1 / 4 / 8 / 24 のみ。 12 / 16 / 48 / 64 は失敗） |
  | `ScanMethod`（0 / 7 / 8） | 無効 | 無効 | **無効** | 無効 | 無効 |
  | `Version`（0 / 9 / 10） | 無効 | 無効 | **無効** | 無効 | 無効 |
  | `Transformation`（0〜17） | 無効 | 無効 | 無効 | 一部失敗・残りは**無効** | 無効 |
  | `SaveFlag`（0 / 18 / 19 / 20） | 無効 | 無効 | **18 で保存後にファイルが開いたまま** | 無効 | **18 で保存後にファイルが開いたまま** |
  | `ColorSpace`（0〜5） | 無効 | 無効 | 無効 | 0 のみ**有効**（CMYK 化で 364,484 B）。 他は失敗 | 無効 |
  | `SaveAsCmyk`（0 / 1） | 無効 | 無効 | 無効 | 無効 | 1 で**有効**（1,195,850 B） |
  | `RenderMethod` / `ImageItems` | 無効 | 無効 | 無効 | 無効（`ImageItems` は失敗） | 無効 |

  - **公開するのは JPEG の品質だけにします**: 効果のある残りは、(1) 品質と**同じつまみ**（`LuminanceTable` / `ChrominanceTable`。 品質を併せて指定すると**テーブルは黙って無視される**ことを実測で確認したため、2 つ公開すると片方が効かない）、(2) **情報を失う**（TIFF の CCITT3 / CCITT4 は 1 ビットへ落として画素を壊す。 `ColorDepth` の 1 / 4 / 8 は減色）、(3) **既定より必ず大きくなる**（TIFF の圧縮方式。 下記）、(4) **ファイルを開いたまま返す**（`SaveFlag` = 18。 保存後にファイル ハンドルが残り、続けて読むと `IOException` になることを実測で確認）、のいずれかです。 使うと分かっている値だけを公開する方針（[`.github/コーディング規約.md`](../../../.github/コーディング規約.md) の YAGNI）に従います（**減色は GDI+ のパラメーターではなく自前の実装であり、この表の対象外**です）。
  - **TIFF の圧縮方式を公開しない理由（実測）**: 指定はできますが（無圧縮 / LZW / RLE）、**既定より小さい値が選べません**（実測: なだらかな画像で既定 173,766 B に対し LZW 2,158,648 B / 無圧縮 3,686,618 B / RLE 3,715,416 B。 一様な画像では既定 5,958 B 対 LZW 11,238 B）。 生成したファイルの TIFF タグを読むと、**既定も LZW 指定も圧縮タグ（259）は 5 = LZW で同じ**であり、違うのは予測子タグ（317）だけでした（既定 = 2 = 水平差分 / 指定 = 1 = なし）。 GDI+ は予測子を指定する手段を持たないため、**指定すると既定より悪くなる**ことになります。
- **形式ごとの名前付きメソッドを作らない理由**: `EncodeToJpgFile` / `EncodeToPngFile` のような形式を名前に持つ入口を足すと、**形式を表す引数と拡張子が二重管理になり**、食い違えば拡張子と実体がずれたファイルができます。 GDI+ のパラメーターで調整できる値を持つ形式は JPEG だけであり（減色は GDI+ のパラメーターではなく自前の実装）、その JPEG の 1 つも「オプション付きの `Save(string, EsImageSaveOptions)` 1 つ（形式は `Format` が決める）」で表せるため、公開面は `Save(string)` / `Save(string, EsImageSaveOptions)` の 2 つに留めます（減色の 3 項目も同じ入口に載ります）。
- **オプションの値は形式ごとに検証します**: 使えない形式へ設定すると `NotSupportedException`、JPEG の品質が 0〜100 の範囲外なら `ArgumentOutOfRangeException`、**減色しない保存（`ColorCount` が `null` かつ GIF 以外）へ `ColorQuality` / `Dither` を設定すると `InvalidOperationException`** です（黙って無視すると「設定したのに効かない」ことに気付かないため。 また上限へ丸めると、呼び出し側が指定した値と実際の出力が食い違います）。 検証は **GDI+ の初期化より前**に行うため、拒否された呼び出しはプロセス全体の副作用（バックグラウンド スレッド +1 等）を起こしません。
- **WebP / HEIF を対応形式へ入れない理由（実測）**: GDI+ のエンコーダーは 5 件だけです（`GdipGetImageEncoders` が返すのは BMP / JPEG / GIF / TIFF / PNG。 デコーダーはこれに EMF / WMF / ICO を加えた 8 件）。 WebP / HEIF のエンコーダーは無く、**存在しない形式の CLSID を渡すと例外を出さずに PNG を書き出します**（実測: PNG の CLSID と `.webp` のパスで 2831 バイトの PNG が書かれ、先頭バイトは PNG のシグネチャ）。 拡張子と中身が食い違うファイルが**黙って**できるため、対応形式へ追加しません（食い違いを承知で作る場合は `Format` + `IgnoreFormatMismatch` を使います）。
- **減色（パレット化）を指定できます（実測）**: `EsImageSaveOptions.ColorCount` を指定すると、保存の前に
  `EsReadOnlyBitmap.ReduceColors`（`ReduceColors(int, ImageQuality, DitherMode)`。 両方の型が同じ名前のメンバーを持ちます）と同じ計算で減色し、**8bpp のインデックス カラー（パレット付き）として**書きます。 **`ColorQuality` と `Dither` はこの計算へそのまま渡します**（段階の意味と実測は `ImageQuality` / `DitherMode` の備考が単一所有します。 減色しない保存へ指定すると `InvalidOperationException` です）。

  | 形式 | 32bpp のまま | 256 色へ減色 | 削減 | `Load` |
  | --- | --- | --- | --- | --- |
  | BMP | 8,294,454 B | **2,074,678 B** | 25% | 可（画素は**完全一致**） |
  | PNG（スクリーンショット風） | 4,305,898 B | **595,082 B** | 14% | 可 |
  | PNG（写真風） | 2,301,470 B | **173,684 B** | 8% | 可 |
  | GIF（スクリーンショット風） | 922,095 B | 691,564 B | 75% | 可 |

  - **32bpp のまま減色しても意味がありません（実測）**: GDI+ の PNG エンコーダーは 32bpp の入力をパレット PNG にしません
    （減色の効果は圧縮率の向上だけであり、BMP では 1 バイトも減りません）。 **8bpp のインデックス カラーとして渡すと
    パレット画像になります**（実測で確認した 3 形式。 BMP / PNG / GIF）。
  - **BMP の RLE 圧縮は併用できません**（GDI+ の BMP エンコーダーは圧縮の指定を無視します）。 減色しても無圧縮の 2,074,678 B のままです。
  - **GIF は指定が無くても常に量子化して書きます（実測に基づく）**: GDI+ の GIF エンコーダーは透明色の番号を書きますが、**その番号をどの画素にも使わせません**（実測: GDI+・`System.Drawing`・`SkiaSharp` のいずれで読み戻しても全画素が不透明になった）。 そのため 32bpp のまま渡すと透過が失われます。 これは GDI+ の不具合であり GIF の仕様の制約ではありません（GIF の透過はパレットの 1 色を透明とする仕組みであり、量子化の有無とは無関係です）。 自前の量子化は透過を保ち、**ファイルも小さく画質も良くなります**（実測上の詳細は下表）。
  - **JPEG と TIFF へ指定すると例外になります**（`NotSupportedException`）。 JPEG はパレットを持てず、TIFF は**未検証**であるため受け付けません（未検証の組み合わせを黙って通さない方針。 [`.github/コーディング規約.md`](../../../.github/コーディング規約.md) の YAGNI と対になる）。
  - **透過の扱い（実測）**: **形式ごとに保存できるアルファが違います**。 減色はアルファも色の一部として扱うため（4 段階に量子化）、**透明な画素は透明なパレットの色へ寄ります**。

    | 形式 | 減色しない場合 | 減色した場合 | 理由 |
    | --- | --- | --- | --- |
    | PNG | **8 ビットのアルファを完全に保存**（色の種類 6 = RGBA。 実測: アルファの一致 100%） | **パレットのアルファを保存**（`tRNS` を書く。 実測: 3 段階のアルファ 0 / 128 / 255 がそのまま残った） | PNG はパレットごとのアルファ（`tRNS`）を持てる |
    | GIF | **完全透明を保存**（GIF は常に自前の量子化で書く。 下記） | **完全透明を保存**（透明の色番号を書く。 実測: 完全透明は残り、半透明 128 は不透明になった） | GIF は 1 ビットの透過しか持てない |
    | BMP | アルファを**保存しない** | 同左（パレットのアルファも無視される） | BMP はアルファを持てない |
    | JPEG | アルファを**保存しない** | 減色を指定できない | JPEG はアルファを持てない |

    - **減色しない PNG は透過を保ちます**（実測: アルファの一致 100%、完全透明の保持 1,408/1,408）。 減色した場合も `tRNS` により**保たれます**（実測: 3 段階すべてが一致）。
    - **GIF は指定が無くても自前の量子化で書きます（実測に基づく）**: **GDI+ の GIF エンコーダーは透明色の番号を書きますが、その番号をどの画素にも使わせません**（実測: 透明色の指定は番号 252 を指すが、GDI+・`System.Drawing`・**SkiaSharp のいずれで読み戻しても全画素が不透明**になった）。 つまり 32bpp のまま渡すと**透過が失われます**（これは GDI+ のエンコーダーの不具合であり、GIF の仕様の制約ではありません。 GIF の透過は「パレットの 1 色を透明とする」仕組みであり、減色の有無とは関係ありません）。 自前の量子化は透明な画素を透明なパレットの色へ割り当てるため透過が保たれます（実測: SkiaSharp でも円の外のアルファが 0）。 **副次的にファイルが小さく画質も良くなります**（実測: 640x360 の写真で 91,294 B / 色差 平均 16.40 に対し **34,130 B / 2.89**。 スクリーンショット風で 106,170 B / 14.07 に対し **86,659 B / 5.15**）。
    - **半透明は減色で 4 段階へ量子化されます**（0〜63 / 64〜127 / 128〜191 / 192〜255。 実測: アルファ 128 は 128 のまま、中間の値は段階の平均になる）。 **RGBA を平均するため、半透明の色は元の値とわずかに違います**（透明と不透明は完全に一致します）。
    - **切り出しのパディング（透明）を持つ画像も減色できます**（実測: 32x32 の画像から 48x48 を切り出して 8 色へ減色し、PNG / GIF とも透明の 1,280 画素がそのまま残った）。
  - **ディザは既定では行いません（実測）**: ディザはパレットの色を交互に置くため保存のファイルが**大きくなります**。 本ライブラリはファイル サイズを下げる目的で減色するため、`DitherMode.FloydSteinberg` を明示された場合だけ行います（実測: 1920x1080 の 16 色グラデーションの PNG で 17,830 B → **319,481 B**。 写真風 16 色で 495,932 B → 603,078 B。 **平坦な色の画像は変化しません**。 縞が消えるかどうかの判断は `DitherMode` の備考が単一所有します）。
  - **外部ライブラリ・外部ツールとの比較（実測。 1920x1080・256 色。 所要は 1 枚あたり）**:

    | 手段 | スクリーンショット風 | 写真風 | 色差（平均） | 前提 |
    | --- | --- | --- | --- | --- |
    | OptiPNG -o1（自前の 256 色 PNG を再圧縮） | **580,508 B** / 514 ms | 122,973 B / 107 ms | 3.95 / 3.83 | 外部実行ファイル |
    | **自前の減色 + 8bpp で保存（本実装）** | 595,082 B / **61 ms** | 173,684 B / **47 ms** | 3.95 / 3.83 | **無し（OS のみ）** |
    | pngquant 256 色 | 643,635 B / 763 ms | **105,307 B** / 700 ms | **2.61 / 3.09** | 外部実行ファイル |
    | Magick.NET 256 色 PNG | 645,647 B / 520 ms | 117,156 B / 553 ms | 3.61 / 3.54 | NuGet（ネイティブ同梱） |
    | ImageSharp 256 色 PNG（Wu） | 661,381 B / 283 ms | 111,764 B / 219 ms | 2.74 / 3.49 | NuGet（下記の制約） |
    | OptiPNG -o1（コアの PNG を減色せず再圧縮） | 2,062,074 B / 816 ms | 724,196 B / 7,338 ms | 0.00 / 0.00 | 外部実行ファイル |
    | コア（GDI+ の既定。 減色なし） | 4,305,898 B / 106 ms | 2,301,470 B / 79 ms | 0.00 / 0.00 | 無し |

    - **本実装が最速です**（61 ms。 次が ImageSharp の 283 ms）。 サイズは OptiPNG が最小（自前より 2.5% 小さい）で、
      減色の品質は pngquant が最も良い（色差 2.61 対 3.95）ものの 1 枚 700 ms と**実行ファイルの同梱**を要します。
    - **本実装の行の所要は減色のパレット作成を改善する前の実測です**（同じ画像では現在これより短くなります。 減色だけの所要は
      `EsReadOnlyBitmap.ReduceColors` の備考が所有します。 外部ツールの値は同じ画像で測った当時のままです）。
    - **外部ライブラリの候補と制約（実測と公式情報）**: **SixLabors.ImageSharp** は減色・ディザ・PNG 圧縮レベル・
      **BMP 8bpp パレット出力**まで備えますが、**v4 はライセンス キーが無いとビルドが失敗し**（実測:
      `error : No Six Labors license found`）、ビルドが通る v3 系は**既知の脆弱性（高 4 件・中 2 件）**を持ちます。
      **Magick.NET-Q8** は Apache 2.0 系でライセンスと脆弱性に問題がありませんが**ネイティブを同梱**し、本実装より遅く大きい。
      **SkiaSharp（導入済み）は減色の API を持ちません**（実測: `SKPngEncoderOptions`（`FilterFlags` / `ZLibLevel`）・
      `SKJpegEncoderOptions`（`Quality` / `Downsample` / `AlphaOption`）・`SKWebpEncoderOptions`（`Compression` / `Quality`）だけ）。
      **NetVips** は pngquant と同じ libimagequant を積みますが**ネイティブが LGPL** です。
  - **減色を既定にしない理由**: 減色は**不可逆**であり、既定の `Save` の「見た目を変えずに書き出す」期待と衝突します。
    また効果は内容に強く依存します（実測: スクリーンショット風で 14%、写真風で 8%）。 明示した場合だけ行います。
- **エンコード・デコードは `gdiplus.dll` のフラット API を直接呼びます**（宣言は `src/EsUtil.Platform.Core/src/Native/Win/WinNative.GdiPlus.cs` が集約。 `WinNative` は DLL ごとの `partial` ファイルに分かれます）。 **外部パッケージを参照しません**（Windows に標準で入る OS コンポーネントだけを使います）。
- **本機能は Windows 専用です**: `gdiplus.dll` は Windows にのみ存在します（非 Windows では `DllNotFoundException`）。 公開メンバー（`Save` / `Load` / `SupportedFileExtensions`）は `SupportedOSPlatform("windows")` で限定します。 本ソリューションは Windows デスクトップ専用アプリのため実害はありません（コアは可搬な層ですが、`src/EsUtil.Platform.Core/src/Native` だけが Windows に依存します）。
- **GDI+ の初期化は `WinGdiPlusLifetime` が単一所有します**（プロセス単位で 1 回だけ `GdiplusStartup` を呼びます）。 終了処理（`GdiplusShutdown`）は**呼びません**（プロセス全体の GDI+ を終了させるため、ホストが別の目的で GDI+ を使っている場合に影響します。 GDI+ はプロセスの終了時に OS が解放します）。
- **初期化を前倒しする公開 API は持ちません**: 初期化は `WinGdiPlusLifetime`（`internal`）が単一所有するため、ホストからは呼べません。 初期化のコストは最初の `Save` / `Load` が負担します。 公開しない理由は**利得が小さいこと**です（実測: 初期化は初回 4.89 ms であり、4K PNG の保存 66.5 ms の 7% に満たない。 しかも 1 プロセスで 1 回だけ）。 加えて、公開すると**利用者が起動時に呼び、実際に必要になる前にプロセス全体の副作用（バックグラウンド スレッド +1・解放されない参照カウント）を起こす**誘導になります。
- **本機能はプロセス全体に及ぶ副作用を引き受けます（実測）**: 初回の `Save` / `Load` で次が起きます。
  - **GDI+ が有効になる**（初期化しないと状態 18 = `GdiplusNotInitialized` で全 API が失敗する）。
  - **バックグラウンド スレッドが 1 本増える**（起動直後 8 → 9。 GDI+ が表示設定の通知を受け取るためで、抑止すると初期化自体が `InvalidParameter` になる）。
  - **解放されない参照カウントを 1 つ取る**（`GdiplusShutdown` を呼ばない。 プロセス終了時に OS が解放する）。
  - **初回だけ遅延が乗る**（初回 4.89 ms / 2 回目 0.53 ms）。

  **この副作用は本ライブラリ固有ではなく、`System.Drawing.Common.Bitmap` を使っても同じです**（実測: スレッド 8 → 9、初回 4.87 ms / 2 回目 0.06 ms。 `System.Drawing.Common` の実装（`System.Private.Windows.Core`）も `GdiplusStartup` だけを宣言し、`GdiplusShutdown` を宣言していません）。 したがって **影響を受けるのは GDI+ を他に使っていないプロセスだけです**。 副作用を避けるには WIC へ移す必要があります（下記）。
- **代替（採用していない）**: **マネージド .NET には他に標準の画像コーデックがありません**。 Windows ネイティブにはあります（下記）。 いずれも実装量または依存の代償があります。
  - **WIC / WinRT（Windows ネイティブ。 実測で存在を確認）**: `Windows.Graphics.Imaging`（`BitmapEncoder` / `BitmapDecoder`。 WIC の .NET 投影）が**追加の NuGet なしで使えます**（Windows TFM `net10.0-windows10.0.x` が Windows SDK の投影を自動参照する。 実測: このマシンに 17763〜26100 の投影が存在）。 実測したエンコーダーは BMP / GIF / JPEG / PNG / TIFF / WMPhoto（JPEG XR）/ DDS / **HEIF（AVIF を含む）/ JPEG XL** で、**7 種すべてで保存に成功**しました。 デコーダーはさらに ICO / CUR / DNG / RAW（約 40 形式）/ **WebP（デコードのみ）** を含みます。 ただし (1) 本コアは `net10.0`（可搬）のため WinRT を直接参照できず、**TFM を Windows 固有にするか常駐アセンブリを分ける**必要があります（COM 相互運用を自前で宣言すれば `net10.0` のまま `Native/` に置けますが、`IWICImagingFactory` 等の宣言に数百行を要します）、(2) HEIF / JPEG XL / WebP は**OS のコーデック拡張（Store アプリ）で提供**されるため、拡張の無い環境では使えません（このマシンには入っています）。
  - **WPF の `System.Windows.Media.Imaging`（`BitmapEncoder` / `BitmapDecoder`）**: Microsoft 公式ですが `UseWPF` が必要で、UI 非依存のコアへ WPF を持ち込みます。
  - **SkiaSharp**: WebP を含む多くの形式を扱えますが、**外部パッケージを参照しない**という本プロジェクトの依存規則に反します（Skia に依存する変換は Skia を使う上位層が単一所有する）。
- **WebP の保存は Windows 標準のどちらでもできません（実測）**: GDI+ は**例外を出さずに PNG を書く**（危険）ので本機能は `NotSupportedException` で拒否し、Windows ネイティブも `BitmapEncoder` に WebP の識別子を持ちません（`BitmapDecoder.WebpDecoderId` があり**デコードのみ**）。 保存が必要なら SkiaSharp などの外部ライブラリが要ります。
- **`gdiplus.dll` のフラット API を選ぶ理由**: 用途（BMP / PNG / JPEG / GIF / TIFF）に対して OS 標準のコーデックで足り、**外部パッケージも相互運用の宣言も要りません**（宣言は `WinNative` の 8 関数）。 下記の実測のとおり **GDI+ が最も速く、可逆形式の圧縮効率も同等以上**です。
- **Native AOT（実測）**: 本機能は `LibraryImport`（ソース生成の相互運用）だけを使うため、**Native AOT でも動作します**（実行時マーシャリングを使いません）。 参考として、代案の `Windows.Graphics.Imaging` は **WinRT（COM ベース）の .NET 投影**であり、エンコード・デコードの実体は **OS 側（WIC）にあります**:
  - **COM の登録や DLL の同梱は不要です**（自分で `regsvr32` する COM ではなく、OS が提供するランタイム クラスを起動するだけ）。 実測でも配布物に追加ファイルは増えませんでした。
  - **Native AOT で動作します**（実測: 本アプリの AOT 配布物で `BitmapEncoder.GetEncoderInformationEnumerator()` が 9 個のエンコーダーを列挙できた）。 CsWinRT（投影と `WinRT.Runtime.dll`）は**マネージド コードとして exe へリンクされる**ため、**単一 exe の構成を保てます**（実測: exe 1 個 + ネイティブ DLL 4 個の構成は変わらず、exe は 24.8 → 24.9 MB で **+0.1 MB**）。
  - **制限**: Windows 専用であること、HEIF / JPEG XL / WebP（デコード）は **OS のコーデック拡張に依存**すること（拡張の無い環境では列挙から消えます）、そして**コア（`net10.0`）からは直接参照できない**こと（`net10.0-windows10.0.x` の TFM が Windows SDK の投影を自動参照する。 COM 相互運用を自前で宣言すれば `net10.0` のまま `Native/` に置けますが数百行）。

- **速度は GDI+ の方が速い（実測・4K 3840x2160・変換込み・5 形式すべて）**:

  | 形式 | 保存 GDI+ | 保存 WIC | 読込 GDI+ | 読込 WIC |
  | --- | --- | --- | --- | --- |
  | BMP | **29 ms** | 198 ms | **35 ms** | 127 ms |
  | PNG | **280 ms** | 353 ms | **76 ms** | 167 ms |
  | JPEG（品質 90） | **33 ms** | 39 ms | **44 ms** | 72 ms |
  | GIF | **196 ms** | 681 ms | **34 ms** | 68 ms |
  | TIFF | **263 ms** | 358 ms | **142 ms** | 212 ms |

  **すべての形式・両方向で GDI+ が速い**（差は PNG で 1.3 倍、GIF 3.5 倍、**BMP 6.8 倍**）。 WIC は WinRT の境界越しにサーフェスを渡すため中間コピーが入り、**BMP のような「memcpy で済む」形式ほど差が開きます**。 コアの画像との相互変換も GDI+ が速い（コア → `Drawing.Bitmap` 10.5 ms / コア → `SoftwareBitmap` 12.3 ms、`Drawing.Bitmap` → コア 6.8 ms / `SoftwareBitmap` → コア 11.9 ms。 WinRT はバイト配列を 1 回経由するため）。

- **同じ品質に揃えたときのサイズ（実測。 JPEG は両者へ同じ値を指定: GDI+ は 0〜100 / WIC は 0.0〜1.0。 可逆形式は品質の指定を持たないため既定設定で比較）**:

  | 形式 | GDI+ | WIC | 往復後の差（最大 / 平均。 0〜255） | 判定 |
  | --- | --- | --- | --- | --- |
  | BMP | 32,400 KB | 32,400 KB | GDI+ 0 / 0.00、WIC 0 / 0.00 | 同一（両方とも無損失） |
  | PNG | **7,553 KB** | 8,780 KB | 両方 0 / 0.00 | **GDI+ が 14% 小さい**（品質は同一 = 無損失） |
  | JPEG 100 | 5,646 KB | 5,646 KB | 同一（最大 3 / 平均 0.09） | 同一 |
  | JPEG 90 | **2,088 KB** | 2,251 KB | GDI+ 54 / 2.58、WIC 51 / 2.46 | **GDI+ が 8% 小さい**（品質はほぼ同等で WIC がわずかに良い） |
  | JPEG 75 | 1,190 KB | 1,190 KB | 同一（最大 117 / 平均 3.32） | 同一 |
  | GIF | 2,315 KB | **1,943 KB** | GDI+ 138 / 14.40、**WIC 72 / 5.09** | **WIC が 17% 小さく、品質も大幅に良い** |
  | TIFF | 11,099 KB | 11,144 KB | 両方 0 / 0.00 | GDI+ が 1% 小さい（品質は同一 = 無損失） |

  - **可逆形式（BMP / PNG / TIFF）は往復の誤差が 0** です（画素は完全に一致。 「品質」の差は無く、差は圧縮効率だけです）。 PNG は **GDI+ の圧縮が 14% 優れ**、TIFF はほぼ同じでした。
  - **JPEG は品質を揃えると同程度**でした（100 と 75 は同サイズ、90 は GDI+ が 8% 小さいが、往復誤差は WIC がわずかに小さい）。
  - **GIF だけは WIC が明確に優位**です（17% 小さく、往復誤差も **平均 5.09 対 14.40** と 3 分の 1）。 GDI+ の GIF エンコーダーはパレット量子化の誤差が大きいためです（速度は WIC が 3.5 倍遅いので、**GIF だけは速度と品質の交換**になります）。
  - まとめると **通常の用途（BMP / PNG / JPEG / TIFF）は GDI+ が速くサイズも同等以上**であり、**GIF を配布物として小さく・きれいに作りたい場合だけ WIC に分がある**という結果です。
  - ※ 計測用の画像は「グラデーション + ノイズ + 細線」（スクリーンショットに近い傾向）です。 形式ごとの既定の圧縮設定・パレット生成は API 側の実装に依存するため、**実画像での数値は画像の内容で変わります**。

- **制限**: JPEG / BMP はアルファ （透明度）を保存できません（形式の仕様。 透過を保つ場合は PNG を選ぶこと）。 `Load` は**縮小読み込みをしません**（元の寸法でデコードする。 小さく使う場合は `Downscale*` で縮小する）。
- **例外**: `Load` は存在しない・画像として解釈できないファイルで `ArgumentException` を投げます（GDI+ はどちらも同じ状態値を返すため区別しません）。 エンコード・デコードの失敗は `Win32Exception`（`ExternalException` の派生。 GDI+ の状態値を保持）で通知します。

### 2.33 画像型の設計上の判断（兄弟と借用を選んだ理由）

現在の画像型（[2.31](#231-imagingesreadonlybitmapsealed-class--idisposable--imagingesbitmapsealed-class--idisposable--imagingespixmapsealed-class--idisposable)）に至る判断と、その理由です。 **実装を変えるときに同じ検討を繰り返さないため**に残します。

**1. 面（interface）を廃止しました**: 以前は `EsReadOnlyBitmap` / `EsBitmap` の 2 つのインターフェイスと抽象基底（`EsBitmapBase`）で権利を表していましたが、**一般的なインターフェイスの意味と役割が違う**（権利の表明であり、実装の差し替え点ではない）ためです。

- 実装していた外部の型は表示アダプタ 1 種類だけで、その中身は**コアの画像への転送**でした（画素を自前で持つ実装は存在しませんでした）。 したがってインターフェイスは「境界の受け口を 1 つに見せる」ためだけに使われており、**型の数を増やす利益より、使い分けを覚える費用の方が大きい**状態でした。
- 受け取る型を具象型にしても「受け取った側は書けない」は保たれます（読み取り専用の型で受けるため）。 キャストを避ける必要がある場面（保存・切り出し・縮小・減色）は、**両方の型が同じ名前のメンバーを持つ**形で解決しています（実装は内部の 1 か所。 §2.31）。
- 廃止の副産物として、破棄状態（`IsDisposed`）の判定が**型で揃いました**（以前は「面から判定する」ためにインターフェイスが要る、という理由付けがありましたが、表示アダプタはコアの画像を内包して転送するだけになったため、コアの実体そのものを見れば足ります）。

**2. 継承から兄弟に変えました（`EsBitmap` と `EsReadOnlyBitmap` は互いに派生しない）**: 継承にすると**未凍結の画素を読み取り専用として暗黙に渡せます**（upcast が無料のため）。 受け取った側は「内容が変わらない」ことを前提にしますが、渡した側が書き続ければ画素は変わり、**約束が静かに破れます**。 兄弟ならその経路が型で消え、渡すには `FromBitmap`（凍結状態で 0 コピー / 複製を選ぶ）を通すしかありません。 **継承を選んだ当初の理由（切り出し・縮小・保存へ渡すたびに型が合わない）は、この安全性に劣ります**（Skia 自身が兄弟型で成立しているため。 実測: `SKImage` と `SKBitmap` は兄弟で、共有は `SKImage.FromBitmap` が凍結状態で判断します）。 代償は無料の upcast が無いことです（`FromBitmap` を 1 回呼ぶ）。

- **同じ種類の誤りを繰り返さないための規則**: API を変える前に、**参照実装（Skia）の規則を先に測る**（実装の摩擦だけを測って規則の一致を確かめないと、今回のように「摩擦が少ないが保証が弱い」構造を選んでしまいます）。

- **兄弟型の利点（同じ実体を 2 つの型で見せない）は失われません**: 派生した書き換え可能な画像を読み取り専用として渡すと、相手は書き込みの入口を持ちません。 同じ実体を書き換え可能な側が変える可能性は残りますが、それは**凍結**（`SetReadOnly`）で明示的に止められます（兄弟型では「書き換え可能な側の書き込みが読み取り専用側の画素を変える」ことすら型では防げず、実体の共有を避けるしかありませんでした）。

**3. 遅延コピーを廃止しました**: 以前は読み取り専用の画像から書き換え可能な画像を作るときに**バッファを共有し、最初の書き込みでだけ複製する**仕組み（copy on write）を持っていました。

- **本番の呼び出しがありませんでした**: `EsBitmap.Create(画像)` を渡していたのはテストだけであり、本番はすべて寸法と色で生成していました。
- **調査したどのフレームワークも遅延コピーを持ちません**（Skia / Avalonia / System.Drawing / WinUI 3）。 遅延複製が実務で成功しているのは「値のように頻繁にコピーされ、生のバッファを外へ渡さない型」であり、書き込み可能なビューを渡す API と組み合わせると、**いつ複製されたか分からない隠れた確保**になります（`Pixels` を取った瞬間に全領域コピーが走りうる）。
- **0 コピーの共有は凍結で得られます**: 読み取り専用の型（書き込みの入口が無い）＋ `SetReadOnly` で、共有しても内容が変わらないことを保証できます。

**4. 借用（`InstallPixels`）を公開しました**: 他フレームワークのビットマップ・アンマネージドの領域・共有メモリを複製せずに扱う入口です。

- **複製（`FromPixelCopy`）と名前で区別します**（Skia と同じ付け方）。 複製の費用は実測で 4K 1 枚あたり 31 MiB と 5.7 ms であり、既に画素がある場合に払う理由がない場面があります。
- **借用先の寿命に依存する契約が増える**という代償は受け入れています（実測: Avalonia の表示用のビューは元の破棄で無効化されます）。 代償を減らすため、解放のコールバック（`release`）を任意で受け取り、**解放の経路は `SafeBorrowedBuffer` の 1 か所**に保っています。 WinUI 3 / Win2D は借用の入口を公開しませんが、本ライブラリは汎用のライブラリであり、**複製を避ける手段が要る場面が想定される**ため公開します。

**5. 凍結（`SetReadOnly`）で書き込みを止めます**: Skia の `SetImmutable` と同じ役割ですが、**凍結後の取得を例外にします**（Skia の凍結は宣言のみであり、実測では凍結後も `SetPixel` / `GetPixelSpan` / `Erase` で書けて、`InstallPixels` はフラグを false に戻しました）。 **限界は 1 つだけ**です: 凍結の前に取得したビュー（`Span`）は残ります（取得時にしか判定できません。 Skia も同じ）。 **凍結は画素領域の共有状態に効かせます**（実体ごとに持つと、共有した実体を通した凍結が他の実体へ届かず「凍結したのに書ける」状態が残るため）。 型で保証できるのは「この型では書けない」ことだけであり、「内容が変わらない」ことは凍結状態に応じた入口（`EsReadOnlyBitmap.FromBitmap`）で得る形にしました（実測: Skia も凍結していない `SKBitmap` からは複製し、凍結済みなら共有する）。

- **「読み取り専用として渡す」と「凍結する」を分けています**: 渡すだけで凍結すると、生成した画像へ切り出し・保存を呼んだ後に書き続けられません。 凍結は書き終えた時点で明示的に行います。

**6. 画素ビューの名前を `Memory<T>` に合わせました**: 書き換え可能な型の無印（`Pixels`）を `Span<byte>`（書き込み可能）、読み取り専用のビューを `ReadOnlyPixels` にしました。

- 以前は書き込み可能なビューを `WritablePixels`、読み取り専用を `Pixels` としていましたが、**書き換え可能な型では書き込みが本題**であり、無印が書き込み可能である方が名前と役割が一致します（`Memory<T>` に対する `Span<T>` と同じ関係）。
- 読み取り専用のビューは**どちらの型にも同じ名前で置く**ため、**名前と返る型が一致します**（同じ名前で型だけが変わる取り違えが起きません）。

**7. `Clone` を廃止し、複製は `Copy`・共有は別名にしました**: `Clone` は「安い複製」を期待させる名前であるにもかかわらず、費用が型で反転していました（旧: 読み取り専用側の `Clone` は 0 コピーの共有、書き換え可能側は 31 MiB の複製であり、**同じ名前が逆の費用**）。 複製は費用が名前から読める **`Copy`**（どちらの型でも独立した画素）、共有は **`FromBitmap`**（凍結済みの画像）と **`ToPixmap`**（読み取りビュー）に分けました（Skia も `SKBitmap.Copy` が複製で、共有は `SKImage.FromBitmap` と名前が別です）。

**8. 0 コピーの切り出しビュー（`EsPixmap`）を追加しました**: 実測で Skia は**ビューと複製の両方**を持っていました（`SKBitmap.ExtractSubset` = **0.000 ms**（元と双方向に書き込みが伝わるビュー。 行幅は元の 15360 バイトのまま）/ `SKImage.Subset` = **0.755 ms**（複製））。 本ライブラリは切り出しが複製（`Crop` で 0.72 ms / 4 MB）だけだったため、**ビューを追加**しました（`EsReadOnlyBitmap.ToPixmap(rect)`）。 ビューは行幅を持つため（元のストライドを保つ）、`ReadOnlyPixels` は行の余白を含み、画素は `GetPixel` で読みます。 **範囲は元画像の内側に限ります**（ビューは透明で埋められないため。 外側を含む切り出しは `Crop`）。 元の寿命に依存せず（参照を 1 つ持つ）、**先に元を破棄しても読めます**。

**9. 任意のストライド・ピクセル形式は受け入れません（実測に基づく設計上の差）**: Skia の画像は任意の `RowBytes` と多数の `ColorType` / `AlphaType` を表せますが、本ライブラリは**連続した BGRA8888（アルファ乗算済み）に固定**しています。 理由は (1) 相互変換と画素処理（切り出し・縮小・減色・保存）が**ストライドを計算せずに 1 本の連続した領域として扱える**こと（実測した最適化はこの前提で成立しています）、(2) アプリが扱う画像（画面キャプチャ・GDI+ のデコード結果）がどれもこの形式に揃うことです。 **借用（`InstallPixels`）も連続した領域に限ります**（行パディングがある領域は `ArgumentException`）。 任意形式が要る場面は、**取り込む時点で変換**します。

**10. 座標が範囲外の場合は `ArgumentOutOfRangeException`** にしました（旧 `IndexOutOfRangeException`）。 `IndexOutOfRangeException` はランタイムが予約した例外であり、送出を禁じるアナライザー（`CA2201` / `MA0012`）があるためです（`System.Drawing.Bitmap.GetPixel` も同じ型を投げます）。 **判定を入れた理由**: 境界を跨いだ座標はバッファの内側に落ちるため、判定しないと隣の行の画素が静かに変わります。

### 2.34 画像操作の他ライブラリとの比較（実測）

同じ画素を持つ 4K（3840x2160・BGRA8888）の画像を入力に、**切り出し（Crop）と縮小（Resize）を各ライブラリで行った実測**です。 入力は 3 者で共有する非所有ビュー（コアのバッファを指す）で、出力の生成と画素の書き込みを含めて測っています。 Release・20 コア・中央値（20 回）。

**切り出し（Crop。 4K → 1000x800 @ 500,400 = 4 MB）**:

| 方法 | 時間 | 1 枚のメモリ | 画素 | 実体 |
| --- | --- | --- | --- | --- |
| コア `Crop` | **0.72 ms** | 4.1 MB | 複製（独立） | 0 初期化を省き、行を直接コピーする |
| コア `Crop`（出力の確保だけ） | 0.01 ms | 4.1 MB | — | 確保のみ（初回タッチは 0.66 ms） |
| コア（行コピーだけ。 確保済みの領域へ） | 0.70 ms | — | 複製 | 切り出しのコピー部分だけを取り出した値 |
| GDI+ `Bitmap.Clone(rect, format)` | 0.67 ms | 4.1 MB | 複製（独立） | **部分矩形は実際に複製する**（行幅は切り出しの幅に詰められる ＝ 4000 バイト） |
| GDI+ `Clone` + 画素を配列へ | 1.48 ms | 4.1 MB + 4.1 MB | 複製 | **同じ成果物（バイト列）にするまでの比較**（コアは 1.44 ms） |
| GDI+ `Graphics.DrawImage(rect)` | 0.79 ms | 4.1 MB | 複製 | 描画経路（`Graphics` の生成を含む） |
| Skia `ExtractSubset` | 0.011 ms | 0.0 MB | **共有（複製しない）** | **非所有ビュー**。 元を破棄・書き換えると壊れる（実測で確認） |
| Skia `ScalePixels(Nearest, 同寸)` | 0.59 ms | 4.1 MB | 複製 | 複製する切り出しの実装 |
| Skia `Canvas.DrawImage(rect)` | 0.89 ms | 4.1 MB | 複製 | 不変イメージ（`SKImage`）を経由する |
| Avalonia `CroppedBitmap` | 0.001 ms | 0.0 MB | **共有（遅延）** | 表示用の射影。 **`Dispose` が元画像まで解放する**（実測で確認） |
| Avalonia `RenderTargetBitmap` + `DrawImage` | 1.44 ms | 4.1 MB | 複製 | 描画経路（描画面のクリアを含む） |

- **コアと GDI+ は同じ仕事をほぼ同じ時間で行います**（コア 0.72 ms / GDI+ 0.67 ms。 差は 8%）。 差の理由は**行ごとのコピーの回し方**だけです（GDI+ はネイティブの `memcpy` を行数分、コアはマネージドの `Span.CopyTo` を行数分呼びます）。 **同じ成果物（バイト列）まで含めると 1.44 ms 対 1.48 ms で並びます**（GDI+ はネイティブ メモリをマネージド配列へ写す追加のコピーが要るため）。
- **GDI+ の `Clone` は部分矩形では複製し、全域では複製しません**（実測: 1000x800 で 0.67 ms / 2000x1600 で 2.77 ms ＝ 面積に比例、3840x2160 では **0.00 ms** で `LockBits` まで触らず 7.79 ms）。 全域の `Clone` は**元の行幅（15360 バイト）のままのビュー**です。 切り出しの結果を書き換える・元を破棄する場合は部分矩形と同じく複製が要ります。
- **Skia の `ExtractSubset` と Avalonia の `CroppedBitmap` は画素を複製しません**（最速なのは複製しないからです）。 Skia のビューは**元の行幅（15360 バイト）を保ったまま**、行ごとに別の領域を指します（実測で確認）。 切り出しの結果を書き換える・元を破棄する場合は複製が要ります（Skia は `ScalePixels` 0.59 ms、Avalonia は `RenderTargetBitmap` 1.44 ms）。

**切り出し（Crop。 4K 全域 = 33 MB の複製）**:

| 方法 | 時間 | 1 枚のメモリ |
| --- | --- | --- |
| コア `Crop(0, 0, 3840, 2160)` | **5.4 ms** | 33.3 MB（出力そのもの） |
| GDI+ `Bitmap.Clone(全域)` | 0.00 ms（**複製しない**）→ **7.79 ms**（`LockBits` で実体化） | 0.0 MB（ビュー） → 33.2 MB（実体化） |
| Skia `ExtractSubset(全域)` | 0.010 ms | 0.0 MB（**共有**） |
| Skia `ScalePixels(Nearest, 全域)` | 5.3 ms | 33.3 MB |
| Skia `CopyTo` | 24.7 ms | **66.5 MB（出力の 2 倍。 中間バッファを確保する）** |
| Avalonia `RenderTargetBitmap` + `DrawImage(全域)` | 11.8 ms | 33.3 MB |

**縮小（Resize。 4K → 320x180 = 12 倍縮小）**:

| 方法 | 時間 | 1 枚のメモリ | 40 倍縮小後の細線の輝度 | 面積平均の厳密値との差（平均 / 最大） |
| --- | --- | --- | --- | --- |
| コア `Nearest` | **0.073 ms** | 0.25 MB | 255（位置により残る） | 25.4 / 225 |
| コア `Bilinear`（既定） | **0.27 ms** | 0.25 MB | 0 | 10.6 / 189 |
| コア `Box`（面積平均） | 1.7 ms | 0.27 MB | 6 | **0.37 / 1** |
| GDI+ `NearestNeighbor` | 0.13 ms | 0.27 MB | 0 | — |
| GDI+ `Bilinear` | 4.1 ms | 0.27 MB | 6 | **0.20 / 0.9** |
| GDI+ `HighQualityBilinear` | 13.1 ms | 0.27 MB | 4 | 1.8 / 62 |
| GDI+ `HighQualityBicubic` | 24.6 ms | 0.27 MB | 5 | 1.6 / 33 |
| Skia `Resize(Nearest + None)` | 0.14 ms | 0.25 MB | 0 | — |
| Skia `Resize(Linear + None)` | 6.5 ms | 0.25 MB | 0 | 10.5 / 189 |
| Skia `Resize(Linear + ミップ)` | 12.5 ms | **1.9 MB（ミップマップを保持）** | 6 | 1.6 / 24 |
| Skia `Resize(Cubic Mitchell)` | 7.7 ms | 0.25 MB | 0 | 12.1 / 191 |
| Skia `Canvas.DrawImage(Linear + ミップ)` | 1.1 ms | 0.25 MB | — | — |
| Avalonia `CreateScaledBitmap(None)` | 0.15 ms | 0.25 MB | 0 | — |
| Avalonia `CreateScaledBitmap(LowQuality)` | 0.47 ms | 0.25 MB | 0 | — |
| Avalonia `CreateScaledBitmap(HighQuality)` | 5.2 ms | 0.25 MB | 6 | — |
| Avalonia `RenderTargetBitmap` + `DrawImage(High)` | 1.1 ms | 0.25 MB | 6 | — |

**縮小（Resize。 4K → 1920x1080 = 2 倍縮小。 出力 8 MB）**:

| 方法 | 時間 | 面積平均の厳密値との差（平均 / 最大） |
| --- | --- | --- |
| コア `Nearest` | **1.1 ms** | 18.3 / 191 |
| コア `Bilinear` | 3.4 ms | 0.28 / 0.8 |
| コア `Box` | 3.1 ms | **0.28 / 0.8** |
| GDI+ `NearestNeighbor` | 4.6 ms | — |
| GDI+ `Bilinear` | 16.7 ms | 0.38 / 1.0 |
| GDI+ `HighQualityBicubic` | 44.3 ms | 5.3 / 113 |
| Skia `Resize(Nearest + None)` | 5.5 ms | — |
| Skia `Resize(Linear + None)` | 21.4 ms | 0.19 / 0.5 |
| Skia `Resize(Linear + ミップ)` | 46.5 ms | 0.28 / 0.8 |
| Avalonia `CreateScaledBitmap(None)` | 5.5 ms | — |
| Avalonia `CreateScaledBitmap(HighQuality)` | 45.9 ms | — |

**立方畳み込み（Bicubic 相当）をコアへ足す場合（検討した結果。 実測）**:

「コアで Bicubic 相当を実装すると、GDI+ の `HighQualityBicubic` と コアの `Box` に対して速度・画質がどうなるか」を試作して確かめました。 試作は 3 通りで、いずれも**縮小先の行を範囲へ分けて並列実行**し、出力は未初期化の配列として確保します（コアと同じ方針）。

| 実装 | 12 倍（320x180） | 2 倍（1920x1080） | 備考 |
| --- | --- | --- | --- |
| コア `Box`（面積平均） | **1.6 ms** | **3.6 ms** | 出力の画素数ではなく元画像の画素数に比例 |
| コア `Bilinear` | 0.23 ms | 3.7 ms | 元の 2x2 のみ |
| 試作: 固定 4 タップ（1 パス。 Keys） | 0.72 ms | 13.0 ms | 縮小後の画素ごとに 4x4 を畳み込む（コストは縮小後の画素数に比例） |
| 試作: 固定 4 タップ（分離 2 パス。 Keys） | 2.3 ms | 12.1 ms | 中間画像（縮小後の幅 × 元の高さ）を確保する |
| 試作: 支持幅を縮小率に広げる（Keys。 2 パス） | 12.9 ms | 21.7 ms | 12 倍で 48 タップ / 40 倍で 160 タップ |
| 試作: 支持幅を縮小率に広げる（Mitchell。 2 パス） | 11.2 ms | 20.5 ms | 12 倍で同タップ数。 リンギングが最も少ない |
| GDI+ `HighQualityBicubic` | 24.7 ms | 44.7 ms | `Graphics.DrawImage` の経路（`Graphics` の生成を含む） |

| 実装 | 厳密な面積平均との差（12 倍） | 差（2 倍） | GDI+ `HighQualityBicubic` との差（12 倍） | 鮮鋭さ（12 倍 / 2 倍） |
| --- | --- | --- | --- | --- |
| コア `Box` | **0.37 / 最大 1** | **0.28 / 最大 0.8** | — | 5.97 / **21.25** |
| コア `Bilinear` | 10.57 / 最大 189 | 0.28 / 最大 0.8 | — | — |
| 試作: 固定 4 タップ（1 パス） | 13.39 / 最大 197 | 3.36 / 最大 52 | 13.43 / 最大 200 | 23.73 / 27.91 |
| 試作: 支持幅（Keys） | 1.30 / 最大 22 | 4.23 / 最大 86 | **0.61 / 最大 33** | — |
| 試作: 支持幅（Mitchell） | 1.47 / 最大 22 | 5.28 / 最大 98 | 0.87 / 最大 33 | 4.13 / 13.13 |
| GDI+ `HighQualityBicubic` | 1.62 / 最大 33 | 5.31 / 最大 113 | —（基準） | 5.29 / 17.04 |

- **GDI+ の `HighQualityBicubic` は固定 4 タップではありません**: 支持幅を縮小率に広げる実装（試作の 3 番目）だけが GDI+ の出力と一致し（差 0.61 / 1.36）、固定 4 タップ版は大きく外れます（差 13.43 / 8.38）。 画質も、GDI+ は `Box` の近く（12 倍で 1.62）にあり、固定 4 タップ版（13.39）は `Bilinear`（10.57）と同程度です。 **したがって GDI+ の「高画質」の実体は支持幅の拡大であり、遅さ（24.7 ms）もそこから来ています。**
- **コアへ足すと GDI+ よりは速くなりますが、`Box` には負けます**: 同じ系統の実装では GDI+ の 1.9〜2.2 倍速く（12.9 対 24.7 / 21.7 対 44.7）、画質もわずかに上です（1.30 対 1.62 / 4.23 対 5.31）。 しかし **`Box` と比べると 6〜8 倍遅く、正しさで負けます**（12 倍で 1.30 対 0.37、2 倍で 4.23 対 0.28）。
- **縮小で `Box` より「きれい」になることはありません**: 2 倍縮小の鮮鋭さ（横隣接の差の平均）は `Box` 21.25 に対し Mitchell 13.13 で、**立方畳み込みの方が柔らかくなります**（`Box` はこの縮小率で情報を失わない理想的なフィルタであるため）。 固定 4 タップ版の 27.91 は**折り返し（間引き）による偽の輪郭**であり、面積平均との差は 10.6（12 倍）と最も不正確です。
- **40 倍縮小でも同じ**です（面積平均との差: `Box` 0.37 / GDI+ 1.58 / 支持幅 Mitchell 1.00 / `Bilinear` 10.32 / 固定 4 タップ 12.98）。
- **結論**: Bicubic 相当を足す利点がありません。 立方畳み込みが正しい選択になるのは**拡大**ですが、`Resize` は拡大を行いません（`NotSupportedException`）。 将来拡大を実装する場合は、**拡大では支持幅を広げる必要がない**ため固定 4 タップの 1 パス（試作の 1 番目。 最も安い実装）が正しい選択になります。

**この実測から言えること**:

- **コアが最速か、最速に並びます**（12 倍縮小の `Bilinear` は 0.27 ms で GDI+ の 15 倍速、Skia の `Linear + ミップ` の 47 倍速。 2 倍縮小の `Bilinear` は 4.0 ms で Skia の `Linear + None` の 5.4 倍速）。
- **面積平均と同じ画質を出せる他の実装は、コアより遅い**（12 倍縮小で面積平均に最も近いのは **GDI+ `Bilinear`**（厳密値との差 0.20）であり 4.1 ms ＝ コア `Box` の 2.4 倍。 Skia は **ミップマップ**（12.5 ms、差 1.6）を伴い、メモリも 1.9 MB を余分に保持します。 Avalonia の `HighQuality`（5.2 ms）は Skia のミップマップ経路と同じです）。
- **コアが速い理由（同一のサンプリング定義で測って分離した。 4K → 320x180 = 12 倍縮小）**:

  | 段階 | 時間 | 何が効いているか |
  | --- | --- | --- |
  | 素朴な双線形（逐次・列の表なし・`double`） | 1.90 ms | 画素ごとに除算・床・クランプを行い、1 スレッドで回す |
  | ＋列の対応表（除算を内側のループから除く）と `int` 化 | 1.90 → 0.41 ms | **除算の事前計算で 4.6 倍**（並列化と合算） |
  | ＋行の範囲ごとに並列化（20 コア） | **0.27 ms** | **並列化で 4.6 倍** |
  | コア `Bilinear`（上記すべて + 範囲で受けるループ） | **0.27 ms** | 素朴な逐次実装の **7 倍速** |

  - したがって速度の主因は **並列化（4.6 倍）と、除算・床・クランプの事前計算（1.5 倍）**であり、次に**中間画像を作らないこと**（Skia のミップマップは +1.9 倍と +1.9 MB、`Canvas.DrawImage` は不変イメージを経由）、**色形式を変換しないこと**（Skia を同じアルゴリズムのまま色形式だけ Rgba8888 にすると 12.5 → 18.5 ms ＝ +48%）が続きます。
- **面積平均（`Box`）と GDI+ の `HighQualityBicubic` は別のアルゴリズムです**: 厳密な面積平均との差は 12 倍縮小で `Box` が **0.37**（最大 1）、`HighQualityBicubic` が **1.6**（最大 33）であり、両者の画素の最大差は **33**（2 倍縮小では 113）です。 `Box` は元の矩形の**等重み平均**（負の重みを持たない）、`HighQualityBicubic` は**立方畳み込み**（負の重みを持つ復元フィルタ）であり、縮小では**名前を揃えられません**。 なお `HighQualityBicubic` は**支持幅を縮小率に広げる**実装であり（固定 4 タップではありません）、その点が `Box` と近い画質になる理由です。 縮小で `Box` に相当するのは GDI+ では `Bilinear`（差 0.20）、Skia では `Linear + ミップ`（差 1.6）です。
- **リンギング（元の画素の範囲を外れる値）**: 12 倍縮小で `Box` 0 / コア `Bilinear` 0 / GDI+ `HighQualityBicubic` 0 / GDI+ `HighQualityBilinear` 1 / **Skia `Cubic Mitchell` 193 成分**です。 `Box` が負の重みを持たないことの現れです。
- **Skia の `ExtractSubset` と Avalonia の `CroppedBitmap` は画素を複製しません**（最速なのは複製しないからです）。 Skia のビューは**元の行幅を保ったまま**行ごとに別の領域を指します。 切り出しの結果を書き換える・元を破棄する場合は複製が要ります（Skia は `ScalePixels` 0.59 ms、Avalonia は `RenderTargetBitmap` 1.44 ms）。
- **GDI+ の `Clone` は部分矩形では複製し、全域では複製しません**（1000x800 で 0.67 ms、2000x1600 で 2.77 ms ＝ 面積に比例。 全域は 0.00 ms で `LockBits` まで触らず 7.79 ms）。 全域の結果も書き換えるなら実体化が要ります。
- **Avalonia の `CroppedBitmap` は `Dispose` で元画像まで解放します**（元の画像を使い続けると `ObjectDisposedException`。 射影を使い終えたら元より先に破棄する必要があります）。
- **画素の一致**: GDI+ の切り出しはコアと完全一致しますが、**Skia の縮小は一致しません**（サンプリング位置と重みの定義が違うため。 細線の輝度もコア `Nearest` 255 に対し Skia `Nearest` 0 で、位置の選び方が異なります）。 経路を混ぜると同じ画像でも結果が変わるため、**本ライブラリは変換をコアへ集約します**。

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

### 3.3 ログの取得と記録

本プロジェクトはログを出力しません（外部パッケージを参照しないため `ILogger` を扱えません）。 **失敗は例外または戻り値で返る**ため、記録は呼び出し側の層が行います。

```csharp
// 失敗は値で返る（呼び出し側が判断して記録する）
if (!cursor.TryGetCursorScreenPoint(out var point))
{
    log.LogCursorPositionQueryFailed();   // 記録は呼び出し側の定義が担う
}

// OS 操作の失敗は例外として返る（対象と Win32 のエラー コードをメッセージに持つ）
try
{
    var image = captureService.CaptureArea(new EsRectInt(0, 0, 1920, 1080));
    // ... 画像を使う（破棄は呼び出し側）
}
catch (Win32Exception ex) when (BoundaryCatch.IsRecoverable(ex))
{
    log.LogOperationError(ex, nameof(CaptureArea));   // 境界で記録して継続する
}
```

「ロガー未設定（null）は出力なしとして扱う」規則は [`.github/ログ規約.md`](../../../.github/ログ規約.md) §5 が所有し、`ILogger` を保持する各層の入口で `?? NullLogger.Instance` により適用します。

### 3.4 ピクセルコンテナ

```csharp
using var bitmap = EsBitmap.Create(1920, 1080);      // 全ピクセル透明（書き換え可能）
bitmap.Clear(b: 0, g: 0, r: 255, a: 255);            // 赤で塗りつぶし（BGRA の順）
bitmap.SetReadOnly();                                 // 書き終えたら凍結する（共有の準備）

using var shared = EsReadOnlyBitmap.FromBitmap(bitmap);      // 凍結済みなので 0 コピーで共有
using var region = shared.Crop(new EsRectInt(10, 20, 800, 600));   // 切り出し（複製。 外側も透明で埋める）
using var view = shared.ToPixmap(new EsRectInt(10, 20, 800, 600)); // 切り出し（0 コピーのビュー。 内側のみ）
using var copy = bitmap.Copy();                       // 凍結前に戻したい場合の複製（独立した画素）
```

## 4. 制限事項

- 本プロジェクトは UI フレームワークへ依存しません。 UI 型との相互変換は、各 UI 実装の Interop 層を用意して境界で行います。
- 実行時のプラットフォーム分岐（ファクトリ）を持ちません。 OS サービスの既定実装はホストが生成して抽象型として注入します（テストはモックを注入できます）。
- `Native/` 配下（Win32 P/Invoke・OS 標準のコーデック）は Windows 専用です。
- 描画は本プロジェクトの対象外です（画像の型と配置計算だけを提供し、描画 API は各 UI が呼びます）。
