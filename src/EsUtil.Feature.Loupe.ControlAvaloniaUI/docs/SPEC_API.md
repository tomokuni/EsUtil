# EsUtil.Feature.Loupe.ControlAvaloniaUI API リファレンス

## 目次 <!-- omit from toc -->

- [1. 文書概要](#1-文書概要)
- [2. 目的](#2-目的)
- [3. 外部利用条件](#3-外部利用条件)
  - [3.1 プロジェクト参照](#31-プロジェクト参照)
  - [3.2 必要な名前空間](#32-必要な名前空間)
  - [3.3 Visual Treeへの配置](#33-visual-treeへの配置)
- [4. 基本的な使用方法](#4-基本的な使用方法)
  - [4.1 AXAMLでの使用例](#41-axamlでの使用例)
  - [4.2 コードからの使用例](#42-コードからの使用例)
  - [4.3 Binding用ViewModel例](#43-binding用viewmodel例)
- [5. 公開API](#5-公開api)
  - [5.1 プロパティ](#51-プロパティ)
    - [5.1.1 Source](#511-source)
    - [5.1.2 CenterPoint](#512-centerpoint)
    - [5.1.3 Options（設定の単一値）](#513-options設定の単一値)
    - [5.1.4 ClientToPixelScaling](#514-clienttopixelscaling)
    - [5.1.5 寸法と外観（Appearance）](#515-寸法と外観appearance)
    - [5.1.6 IsVisible](#516-isvisible)
    - [5.1.7 Log](#517-log)
    - [5.1.8 LogPrefix](#518-logprefix)
    - [5.1.9 CenterPointProvider（描画の直前の拡大中心の取り直し）](#519-centerpointprovider描画の直前の拡大中心の取り直し)
  - [5.2 メソッド](#52-メソッド)
    - [5.2.1 RequestRender](#521-requestrender)
    - [5.2.2 Clear](#522-clear)
    - [5.2.3 SetCoordTransform](#523-setcoordtransform)
    - [5.2.4 RefreshPosition](#524-refreshposition)
  - [5.3 表示位置・サイズの自己計算（座標変換ポート方式）](#53-表示位置サイズの自己計算座標変換ポート方式)
    - [5.3.1 PositionOffset](#531-positionoffset)
  - [5.4 イベント](#54-イベント)
    - [5.4.1 Rendered](#541-rendered)
  - [5.5 型](#55-型)
    - [5.5.1 LoupeControl](#551-loupecontrol)
- [6. 描画のタイミング](#6-描画のタイミング)
- [7. ライフサイクルと所有権](#7-ライフサイクルと所有権)
  - [7.1 Source の所有権](#71-source-の所有権)
  - [7.2 中間 Bitmap を持たない](#72-中間-bitmap-を持たない)
- [8. 表示位置・サイズの責務](#8-表示位置サイズの責務)
- [9. 推奨使用パターン](#9-推奨使用パターン)
- [10. 制約事項](#10-制約事項)
- [11. サンプルアプリケーション](#11-サンプルアプリケーション)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `EsUtil.Feature.Loupe.ControlAvaloniaUI`（公開クラスは `LoupeControl`） |
| プロジェクト | `src/EsUtil.Feature.Loupe.ControlAvaloniaUI/src/AvaloniaUI/EsUtil.Feature.Loupe.ControlAvaloniaUI.csproj` |
| 名前空間 | `EsUtil.Feature.Loupe.AvaloniaUI` |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要基底クラス | `Avalonia.Controls.Panel`（コードのみ。 AXAML を持たない） |
| 表示方式 | 子の描画層（`RenderLayer`）が窓の描画面へ直接描く方式（中間 Bitmap を持たない）と、中心座標ラベルのテキスト要素（`Border` + `TextBlock`） |
| 主な用途 | 画像中心周辺の拡大表示、画像端の外側（画像外）の無地の表示 |

## 2. 目的

`LoupeControl` は、指定された画像の任意の中心座標周辺を拡大し、ルーペ画像として表示するAvaloniaコントロールです。

次の機能を提供します。

- Avalonia Bindingによる画像入力
- 指定中心座標周辺の画像領域抽出
- 1〜64倍の拡大表示
- 画像範囲外への画像外の色（無地）の表示
- 十字線の表示
- 枠線の表示
- DPIスケーリングに対応した描画（描画計画は物理ピクセル、描画面は DIP）
- 窓の描画面への直接描画（フレームの描画フェーズで 1 回。 中間 Bitmap の生成も画素の複製もしない）
- 描画エラーの注入ロガーへの記録
- 描画完了通知イベント（`Rendered`・描画先の `DrawingContext` と描画計画を渡す。 中心座標ラベルの契約も含む）

## 3. 外部利用条件

### 3.1 プロジェクト参照

利用側プロジェクトから `EsUtil.Feature.Loupe.ControlAvaloniaUI.csproj` を参照します（`EsUtil.Feature.Loupe.Core` は本プロジェクトが参照するため、追加の参照は不要です）。

```xml
<ProjectReference Include="..\EsUtil.Feature.Loupe.ControlAvaloniaUI\src\AvaloniaUI\EsUtil.Feature.Loupe.ControlAvaloniaUI.csproj" />
```

### 3.2 必要な名前空間

```csharp
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Feature.Loupe.AvaloniaUI;
```

### 3.3 Visual Treeへの配置

`LoupeControl` は `Panel` であるため、表示するには親Visualの子要素として配置します。

```csharp
var loupe = new LoupeControl
{
    IsVisible = true,
    IsHitTestVisible = false,
    ZIndex = 100
};

panel.Children.Add(loupe);
```

ルーペの描画は子の描画層（`RenderLayer`）が窓の描画面へ直接行います（利用側が描画 API を呼ぶ入口は `Rendered` イベントだけです）。

## 4. 基本的な使用方法

### 4.1 AXAMLでの使用例

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:loupe="clr-namespace:EsUtil.Feature.Loupe.AvaloniaUI;assembly=EsUtil.Feature.Loupe.ControlAvaloniaUI">
    <Canvas>
        <loupe:LoupeControl
            IsVisible="{Binding IsLoupeVisible}"
            Source="{Binding SourceImage}"
            CenterPoint="{Binding CenterPoint}"
            Options="{Binding Options}"
            ClientToPixelScaling="1.0" />
    </Canvas>
</Window>
```

### 4.2 コードからの使用例

```csharp
var loupe = new LoupeControl
{
    Source = sourceBitmap,
    CenterPoint = new Point(400, 300),
    Options = LoupeOptions.Default with { RequestedSize = 240, ZoomLevel = 4 },
    ClientToPixelScaling = 1.0,
    IsHitTestVisible = false,
    IsVisible = true
};

canvas.Children.Add(loupe);
Canvas.SetLeft(loupe, 300);
Canvas.SetTop(loupe, 220);
```

### 4.3 Binding用ViewModel例

```csharp
public IImage SourceImage { get; } = sourceBitmap;

public Point? CenterPoint { get; set; } = new(400, 300);

public bool IsLoupeVisible { get; set; } = true;
```

`Source` は `Image.Source` と同じAvaloniaの `IImage?` 型です。 ピクセル抽出処理は `Bitmap`（`WriteableBitmap` / `RenderTargetBitmap` を含む）と `EsBitmapAvaloniaAdapter`（コアの画像を内包し、`GetEsBitmap()` で取り出せる `IImage` アダプタ）をサポートします。

## 5. 公開API

公開 API は「プロパティ / メソッド / イベント / 型」の 4 分類で構成されます。
座標を扱う API は、対象座標系 / DIP 座標系のどちらであるかを明記します。

### 5.1 プロパティ

#### 5.1.1 Source

| 項目 | 内容 |
| --- | --- |
| 型 | `IImage?` |
| 既定値 | `null` |
| 用途 | 拡大元画像 |
| Binding | 対応 |
| 実装上の対応型 | `Bitmap`（`WriteableBitmap` / `RenderTargetBitmap` を含む） / `EsBitmapAvaloniaAdapter` |

`Source` が `null` の場合、画像なし（画像外の色のみ）を描画します。

`Source` に `Bitmap`（派生を含む）以外の `IImage`（`EsBitmapAvaloniaAdapter` を除く）を設定した場合、`NotSupportedException` が発生します。 描画の経路では例外を `LoupeControl.Log` へ記録し、そのフレームは下地までで終えます（アプリを落としません）。

#### 5.1.2 CenterPoint

| 項目 | 内容 |
| --- | --- |
| 型 | `Point?` |
| 単位 | **対象座標系**（元画像の物理ピクセル座標） |
| 既定値 | `null` |
| 用途 | 拡大表示の中心座標 |

`CenterPoint` が `null` の場合、画像が設定されていても拡大画像を描画しません（画像外の色のみ）。

画像範囲外の座標も指定できます。 抽出領域と画像の重なり部分は画像として表示され、範囲外部分は画像外の色（無地）になります。

#### 5.1.3 Options（設定の単一値）

| 項目 | 内容 |
| --- | --- |
| 型 | `LoupeOptions`（コアの `EsUtil.Feature.Loupe`） |
| 既定値 | `LoupeOptions.Default`（表示サイズ 160 / 拡大率 4 / 寸法は `LoupeOptions` の既定値） |
| 用途 | ルーペの設定（表示サイズ・拡大率・寸法）を 1 つの値で指定する |

```csharp
loupe.Options = LoupeOptions.Default with { RequestedSize = 240, ZoomLevel = 4 };
```

設定の値・既定値・数値項目の安全域への補正は**コアの `LoupeOptions` が単一所有**します（UI 側は既定値も補正の登録も持ちません）。 値の設定経路（オブジェクト初期化子・`with` 式・バインディング・逆シリアル化）によらず、範囲内の値だけが保持されます。

| `LoupeOptions` の項目 | 型 | 既定値 | 補正範囲 | 説明 |
| --- | --- | --- | --- | --- |
| `RequestedSize` | `int` | `160` | `1`〜`4096` | ルーペの基本サイズ（DIP・一辺） |
| `ZoomLevel` | `int` | `4` | `1`〜`64` | 拡大率 |

**表示サイズ（`RequestedSize`）はコア（ファサード）がそのまま使います**: 表示サイズはコアが本値（基本サイズ）をそのまま使います（領域に合わせた縮小はしません。 ホストは基本サイズを設定するだけです）。 表示サイズは公開 API として外部へ公開しません（ホストは関与せず、コアが表示と描画で同じ値を使います）。

**設定と状態（データ）の区別**: マウス移動ごとに変わる `Source` / `CenterPoint` は個別プロパティのままです（設定に含めません）。 DPI スケーリング係数（`ClientToPixelScaling`）と表示位置のオフセット（`PositionOffset`）もフレームワーク・ホスト固有のため設定に含めません。

**描画の差し替え**: 描画は本コントロールの `LoupeContextPainter.Paint`（描画計画を描画 API へ写すだけの実装。 本プロジェクトの内部）が担います。  
見た目を変える場合は 2 つの入口を使います（**派生は不要**）。  

- 色・枠線の太さ・表示可否: `Appearance`（`LoupeAppearance`）を差し替える（`LoupeAppearance.Default with { ... }`。 表示を止めても計画は矩形を保ち、描画側（コントロールの描画実装）がフラグを見て描かない）
- 描画計画（装飾の矩形・ラベルの文言と配置）: `DrawPlanResolver`（`Func<LoupeDrawPlan, LoupeDrawPlan>`）で差し替える（配置と描画の両方に適用される）

矩形で表せない描画（円・斜線等）を加える場合は、外観の `Show*` を false にして `Rendered` のビットマップへ自前で描き、必要なら `DrawPlan` で計画どおりの描画を行います。

#### 5.1.4 ClientToPixelScaling

| 項目 | 内容 |
| --- | --- |
| 型 | `double` |
| 既定値 | `1.0` |
| 補正範囲 | `0.1`〜`8.0` |
| 用途 | DIP から描画の物理ピクセルへの変換 |

描画の物理ピクセルの一辺は概ね次の式で決定されます。

```text
PixelSize = ceil(表示サイズ × ClientToPixelScaling)
```

環境値（DPI）のためコアの `LoupeOptions` には含めず、補正（`coerce`）はプロパティが担います（安全域の定義はコアの `LoupeCore.Limits` が単一所有します）。

#### 5.1.5 寸法と外観（Appearance）

ルーペの基本サイズと拡大率は **コアの `LoupeOptions`（`EsUtil.Feature.Loupe`）が単一所有**し、`Options` として指定します（両 UI で既定値・意味が一致します）。 **枠線の太さ・十字線と枠線とラベルの表示可否・色・ラベルのフォントサイズ・角丸は描画にだけ必要な設定**であり、提示層 `EsUtil.Feature.Loupe.Presenter` の `LoupeAppearance`（`EsUtil.Feature.Loupe`） が単一所有し、本コントロールの `Appearance` プロパティとして指定します（コアはこれらの値を決定せず、描画可否の判定にも使いません）。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Options` | `LoupeOptions` | コアの既定値 | ルーペの基本サイズと拡大率。 `with` 式で部分変更する。 数値項目はコアの `LoupeOptions` が生成時に安全域へ補正する |
| `Appearance` | `LoupeAppearance`（`EsUtil.Feature.Loupe`） | `LoupeAppearance.Default` | 枠線の太さ・表示可否（十字線・枠線・ラベル）と描画に使う色（ペーストボード（画像外）・枠線・十字線・中心座標ラベルの 6 色）・ラベルのフォントサイズ・角丸。 `LoupeAppearance.Default with { ... }` で部分変更する |
| `DrawPlanResolver` | `Func<LoupeDrawPlan, LoupeDrawPlan>?` | `null` | **描画計画の差し替え点**（装飾の矩形・ラベルの文言と配置）。 設定すると保持中の計画を破棄して再解決する（配置と描画の両方に適用され、同じ入力では 1 フレーム 1 回だけ解決されるため、呼び出しの時点に依存しない純粋関数にすること） |
| `CenterPointProvider` | `Func<Point?>?` | `null` | **描画の直前に拡大中心を取り直す関数**（[5.1.9](#519-centerpointprovider描画の直前の拡大中心の取り直し)）。 null または null の戻り値は取り直さない |

`Options`（コアの `LoupeOptions`）の項目（詳細は `EsUtil.Feature.Loupe.Core` の `docs/SPEC_API.md` を参照）:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `RequestedSize` | `int` | `160` | ルーペの基本サイズ（DIP。 1〜4096 に補正） |
| `ZoomLevel` | `int` | `4` | 拡大率（1〜64 に補正） |

`Appearance`（`LoupeAppearance`。 詳細は `EsUtil.Feature.Loupe.Presenter` の `docs/SPEC_API.md` を参照）の項目:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `BorderThickness` | `int` | `2` | 枠線の太さ（DIP。 1〜64 に補正） |
| `ShowCrosshair` / `ShowBorder` | `bool` | `true` | 十字線・枠線の表示有無（透明色を「描かない」の代用にしない） |
| `ShowCoordLabel` | `bool` | `true` | 中心座標ラベルの表示有無（中心が未設定のときは描画しない） |
| `ShowPasteboard` | `bool` | `true` | 画像外（ペーストボード）の表示有無（非表示でも描画面を透明で塗る） |
| `PasteboardColor` | `System.Drawing.Color` | 中性グレー（`#FF535353`） | 画像外（ペーストボード）の色。 描画面を置き換えて前フレームの残像を残さない（非表示時は透明で塗る） |
| `BorderColor` | `System.Drawing.Color` | LightGray（`#FFD3D3D3`） | 枠線の色 |
| `CrossColor` | `System.Drawing.Color` | 赤 40%（`#66FF0000`） | 十字線の色 |
| `CoordLabelColor` | `System.Drawing.Color` | 白 | 座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `System.Drawing.Color` | 黒 60%（`#99000000`） | 座標ラベルの背景色 |
| `CoordLabelFontSize` | `double` | `12.0` | 座標ラベルのフォントサイズ（DIP。 6〜48 に補正。 非数は既定値へ補正） |
| `CoordLabelCornerRadius` | `int` | `2` | 座標ラベルの背景の角丸半径（DIP。 0〜64 に補正。 0 は角丸なし） |

座標ラベルは中心座標（対象座標）を `(x, y)` 形式でルーペの左上に表示します。 実寸 → 配置矩形の写像（内側余白 4 / 1 DIP、基準点は左上）は描画層（`EsUtil.Feature.Loupe.Presenter` の `LoupePresenter`（`LabelPlan` partial））が単一所有し、体裁の値（色・フォントサイズ・角丸）は描画の設定（`Appearance`）が単一所有します。 **表示はコントロールが行います**（`Border` + `TextBlock` の子要素として配置する。 実寸測定は `FormattedText`、描画順はコアの `LoupeDrawLayer` が単一所有するため、UI は実測と要素の配置だけを行います）。

寸法項目（`Appearance.BorderThickness`）は DIP の値であり、描画計画の解決時に**物理ピクセルへ換算されます**（`EsUtil.Platform.Core` の `DpiScaler.ScaleToPixel`。 呼び出すのは `EsUtil.Feature.Loupe.Presenter` の `LoupePresenter`。 `ClientToPixelScaling` に追従するため、DPI ≠ 100% でも DIP 基準の見た目になります）。 UI 層は換算式を持ちません。

#### 5.1.6 IsVisible

Avalonia標準の `Visual.IsVisible` を使用します。 初期値は `false`（コンストラクタで設定。 表示指示は `IsVisible = true` で行う）。

- `true`: ルーペ画像を表示する（次のフレームで描画する）
- `false`: 未実行の再描画を無効化する（解放する中間 Bitmap は無い）

非表示中に画像・座標・設定を変更しても描画されません（状態は保持され、表示時に反映されます）。

#### 5.1.7 Log

| 項目 | 内容 |
| --- | --- |
| 型 | `ILogger` |
| 既定値 | `NullLogger.Instance` |
| 用途 | 描画エラー・診断の通知先ロガー（出力先は利用側のロギング構成に依存） |

#### 5.1.8 LogPrefix

| 項目 | 内容 |
| --- | --- |
| 型 | `string?` |
| 既定値 | `null`（定義クラスの既定は `[Loupe]`） |
| 用途 | ログの接頭辞（ロガーと一緒に設定する。 null・空白で既定値へ戻す）。 提示層・コアへ伝搬する |

#### 5.1.9 CenterPointProvider（描画の直前の拡大中心の取り直し）

| 項目 | 内容 |
| --- | --- |
| 型 | `Func<Point?>?` |
| 既定値 | `null`（取り直さない） |
| 用途 | 描画の直前に拡大中心を取り直す関数。 **返した中心は `CenterPoint` へ反映する**（中心座標ラベル・表示位置・拡大画像を同じ中心で揃える） |

**ポインター移動イベントの座標は入力へ積まれた分だけ古い**（最新の移動がまだ届いていない）ため、ホストは本関数で実カーソルから中心を取り直せます。
本コントロールは再描画のジョブ（1 フレーム 1 回へ結合されたもの）の先頭で本関数を呼びます。
中心を反映しても再描画の要求は増えません（未実行の要求は 1 回へ結合済みです）。
取り直しの可否（ポインターを追従している最中かどうか）と実カーソルから対象座標への変換はホストの責務です。
null を返した場合は現在の中心を使います。

```csharp
loupe.CenterPointProvider = () => marquee.TryGetCurrentLoupePosition(out var position) ? position : null;
```

### 5.2 メソッド

#### 5.2.1 RequestRender

```csharp
void RequestRender()
```

現在の状態からルーペ画像の再生成を要求します（**プロパティ変更を伴わない再生成要求**）。

ホストが `Rendered` でビットマップへオーバーレイを描画する場合、重ねるデータの変化（一覧の選択・外部からの状態変更等）ではプロパティが変わらないため、本メソッドで再生成を要求します。

ルーペのプロパティは値が変わらない設定（同一値の再設定）では変更通知を発行しないため、オーバーレイの元データがルーペのプロパティに現れない場合は本メソッドが唯一の再生成手段です。 上位の合成コントロールは、破線の色（外観設定）・破線の表示可否（モード）・選択矩形の変更時に本メソッドを呼び出します。

描画は次のフレームで行われ、完了は `Rendered` イベントで通知されます（戻り値を待つ必要はありません）。 プロパティ変更と同じ経路を通るため、複数の要求は 1 フレームに 1 回の描画へ結合されます（呼び出し頻度を気にせず呼べます）。 非表示中は描画しません。

```csharp
// 一覧の選択が変わったとき（ルーペの中心は変わらないが、破線を描き直したい）
loupe.RequestRender();
```

#### 5.2.2 Clear

```csharp
void Clear()
```

ルーペを非表示にします（解放する中間 Bitmap は無いため、`IsVisible = false` と等価です）。

```csharp
loupe.Clear();
```

#### 5.2.3 SetCoordTransform

```csharp
void SetCoordTransform(ICoordTransform? port)
```

論理座標（対象）↔ 親パネル DIP の座標変換ポートを設定します（null = 等価変換。 対になるコントロールと同方式）。

変換関数は呼び出し毎に評価されるため、ウィンドウサイズ変更・ズーム・スクロール等のレイアウト変化に追従できます。 表示位置・サイズは本ポートから自己計算されます（イベント時の DIP キャッシュは使わない）。

#### 5.2.4 RefreshPosition

```csharp
void RefreshPosition()
```

表示位置と表示サイズを最新のレイアウトから再計算します（外部からの更新要求）。

座標変換ポートは呼び出し毎に最新の表示状態を評価するため、パン・ズーム等のレイアウト非変更なビューポート変化時にホスト（上位 AP）が呼び出すことで、再計算だけで追従できます。

### 5.3 表示位置・サイズの自己計算（座標変換ポート方式）

`CenterPoint`（対象座標）と `SetCoordTransform` で設定したポートから、ルーペの表示位置と表示サイズを自己計算します。

| 項目 | 計算方法 |
| --- | --- |
| 表示サイズ | 基本サイズ（`Options.RequestedSize`）をそのまま使う（親パネルが収められない場合は配置しない） |
| 表示位置 | 中心 →（ポート）→ 親パネル DIP → カーソル右下 + `PositionOffset` → 親パネル内へ補正（`RenderTransform` で反映） |

再評価タイミング: `CenterPoint` 変更 / ポート設定（`SetCoordTransform`）/ `PositionOffset` 変更 / `Options` 変更 / `IsVisible` 変更 / レイアウト時（`ArrangeCore` オーバーライド）/ 外部要求（`RefreshPosition`）。

#### 5.3.1 PositionOffset

| 項目 | 内容 |
| --- | --- |
| 型 | `double` |
| 既定値 | `0` |
| 単位 | DIP 座標系 |
| 用途 | ルーペ中心（カーソル位置）からの右下方向へのオフセット量。 カーソルにルーペが重ならないよう、ホストが設定する（例: 切り出しモードでは上位のアプリの設定値、領域選択モードでは `20.0`） |

### 5.4 イベント

#### 5.4.1 Rendered

```csharp
event EventHandler<LoupeRenderedEventArgs>? Rendered
```

ルーペの描画完了（表示内容の反映）を通知します。 拡大画像が実際にコントロールへ反映された時点で発火します。

| 項目 | 内容 |
| --- | --- |
| 引数型 | `LoupeRenderedEventArgs` |
| 発火タイミング | ルーペの内容を描いた直後（同じ描画コンテキスト。 フレームの描画フェーズで 1 回） |
| 用途 | ホストがルーペに重ねるオーバーレイ（線分・選択枠等）の描画位置を計算する |

**`LoupeRenderedEventArgs` のプロパティ**

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Context` | `DrawingContext` | ルーペの内容を描いた描画コンテキスト（描画中のみ有効）。 イベントハンドラ内で付加情報を描画 API で描ける |
| `Content` | `LoupeDrawPlan` | 実際に描画した描画計画（可視域の左上 `CaptureTopLeft`（実数を含む実効原点）・拡大率 `ZoomLevel`・描画面のサイズを持つ） |

オーバーレイの描画位置は `e.Content.CaptureTopLeft` と `e.Content.ZoomLevel` から `(対象座標 − CaptureTopLeft) × ZoomLevel` で求められます（`CaptureTopLeft` は**可視域の左上（実効原点。 小数を含む）**であり、拡大中心は描画面の中心へ写るため、十字線と本式の結果は一致します）。

外観で装飾を止めている場合でも、`LoupeControl.DrawPlan(DrawingContext, IImage?, in LoupeDrawPlan)`（static）で計画どおりの描画を行えます（描画計画（`e.Content`）をそのまま渡す。 止めた項目はフラグを見て描かないため描かれません。 `DrawingContext` は描画中のみ有効）。

通知するのはホストが付加情報を描くために必要な最小限（描画先の描画コンテキスト・描画計画）です。 座標変換に必要な値は描画計画が単一所有するため複製しません。 表示位置・表示サイズは、描画に必要な値をホストが既に保持しているため通知しません（必要な場合はコントロールの公開プロパティから取得します）。

```csharp
loupe.Rendered += (_, e) =>
{
    if (e.Content.CaptureTopLeft is not { } topLeft) return;
    var local = (対象座標 - topLeft) * e.Content.ZoomLevel;
    // local の位置へ線分等を e.Context へ描く（ルーペの内容の上に重なる）
};
```

> プロパティ変更による再描画（`RequestRender`）では、最新状態のスナップショットに対して 1 回だけ発火します（連続変更は 1 フレームに 1 回に結合）。

### 5.5 型

公開型は `LoupeControl` と `LoupeRenderedEventArgs` です。
描画・画像処理の実装は internal（非公開）です。 UI 非依存コア（`EsUtil.Feature.Loupe.Core`）と描画層（`EsUtil.Feature.Loupe.Presenter` の `LoupePresenter` / `LoupeAppearance` / `LoupePresenter`（`LabelPlan` partial））の型は public です（外部が UI アダプタを実装できるようにするため）。 描画の手順と色は描画層が単一所有し、本プロジェクトは描画（`DrawingContext`）とラベル要素の配置を担います。
UI 固有のログ（`LoupeLogMessage`）は本プロジェクトの internal です。

#### 5.5.1 LoupeControl

公開 API は「5.1 プロパティ」「5.2 メソッド」に記載の通りです。
StyledProperty は同ファイルに定義されます（Avalonia のバインディング・スタイル設定に必要なため public）。

## 6. 描画のタイミング

ルーペの描画は**フレームの描画フェーズで 1 回**行われます。 複数のプロパティが連続して変更された場合も、再描画の要求は 1 回のポストへ結合されます（描画そのものはフレームごとに 1 回）。

描画が行われる契機は次のとおりです。

- `CenterPoint` の変更
- `Source` の変更
- `Options`（設定・寸法）または `Appearance`（外観）の変更
- `IsVisible = true`（表示の開始）
- `RequestRender()` の実行

描画エラー（`Source` が未対応の `IImage` 等）は `Log` へ記録され、アプリは落ちません（そのフレームは下地までで終わり、例外は呼び出し元へ伝播しません）。

`Log` へ設定したロガーは**コアへも伝搬**します。 設定（`Options`）・環境値（`ClientToPixelScaling`）・拡大中心（`CenterPoint`）の変更ログはコアが出力し、本コントロールは `Source` の変更と描画の例外だけを出力します。

## 7. ライフサイクルと所有権

### 7.1 Source の所有権

`Source` に設定した元画像（`Bitmap` / `EsBitmapAvaloniaAdapter`）の所有権は利用側にあります。

`LoupeControl` は元画像を破棄しません（`EsBitmapAvaloniaAdapter` を設定した場合も、真実の画像の破棄は利用側に委ねる。 `EsBitmapAvaloniaAdapter` は `IImage` の非所有契約のため `Source` の設定では破棄されないので、**差し替える側が旧アダプタを破棄して射影を解放する**）。

### 7.2 中間 Bitmap を持たない

ルーペの描画は窓の描画面へ直接行うため、**本コントロールが所有・解放する中間 Bitmap はありません**（描画のたびの確保も画素の複製も無い）。 描画の範囲は子要素の矩形（`ClipToBounds`）が決めます。

## 8. 表示位置・サイズの責務

`LoupeControl` は描画に加え、**表示位置・表示サイズを自己計算**します
（座標変換ポート方式。 対になるコントロールと同様）。

- 表示位置: `CenterPoint`（対象座標）を `SetCoordTransform` で設定したポートで親パネル DIP へ変換し、カーソル右下 + `PositionOffset` + 補正した位置を `RenderTransform` で反映します。
- 表示サイズ: `Options.RequestedSize`（基本サイズ）をそのまま表示サイズとして内部管理します（ホストは基本サイズを設定するだけ）。
- 再評価タイミング: `CenterPoint` 変更 / ポート設定 / `PositionOffset` 変更 / `Options` 変更 / レイアウト時（`ArrangeCore`）/ 外部要求（`RefreshPosition`）。

上位APでは次の処理を行います。

- ポインター座標の取得
- 元画像座標への変換
- `SetCoordTransform` / `PositionOffset` の設定
- `LoupeControl` のVisual Treeへの追加・削除

## 9. 推奨使用パターン

```csharp
private readonly LoupeControl _loupe = new()
{
    IsHitTestVisible = false,
    // 設定（基本サイズ・拡大率）はコアの値を 1 つ設定する（with 式で部分変更する）
    Options = LoupeOptions.Default with { RequestedSize = 240, ZoomLevel = 4 },
    // 外観（色・枠線の太さ・表示可否）は提示層の外観を 1 つ設定する
    Appearance = LoupeAppearance.Default with
    {
        BorderThickness = 2,
        CrossColor = DrawingColor.FromArgb(102, 255, 0, 0),
        PasteboardColor = DrawingColor.FromArgb(255, 0x53, 0x53, 0x53),
        BorderColor = DrawingColor.LightGray,
    },
};

private void InitializeLoupe(Panel panel, Bitmap source)
{
    _loupe.Source = source;
    panel.Children.Add(_loupe);
    _loupe.IsVisible = true;
}

private void UpdateLoupe(Point imagePixelPoint, Point displayPoint)
{
    _loupe.CenterPoint = imagePixelPoint;
    Canvas.SetLeft(_loupe, displayPoint.X);
    Canvas.SetTop(_loupe, displayPoint.Y);
}

private void RefreshLoupe()
{
    // 中心は変わらないが、ビットマップへ重ねる内容が変わった場合に再生成を要求する
    _loupe.RequestRender();
}
```

## 10. 制約事項

- 領域抽出を行うため、実質的な画像入力対応型はAvalonia `Bitmap` です。
- `RenderTargetBitmap` と `DrawingContext` の合成はUIスレッドで実行します。
- 高頻度更新では、利用側でポインター更新頻度を制限することを推奨します。
- `Source` に設定した画像は利用側が有効期間を管理してください。
- 色（`Appearance`）・寸法（`Options`）の連続変更は、同一Dispatcher待機中である場合に1回へ集約されます。
- `RequestRender()` は完了待機APIではありません。 完了は `Rendered` イベントで通知されます。
- 外部 `DrawingContext` へルーペを描画するAPIは提供しません。 必要な場合も `LoupeControl` をVisual Treeへ配置してください。

## 11. サンプルアプリケーション

動作確認用サンプルは次のプロジェクトです。

```powershell
dotnet run --project src/EsUtil.Feature.Loupe.ControlAvaloniaUI/sample/EsUtil.Feature.Loupe.ControlAvaloniaUI.Sample.csproj
```

サンプルでは次を確認できます。

- `Source` のBinding
- `CenterPoint` のマウス追従
- 画像端から外れた部分の画像外の色（無地）
- `Options`（`ZoomLevel` / `RequestedSize`）と `Appearance`（色・枠線の太さ・表示可否）・`ClientToPixelScaling`
- 十字線・枠線の色変更（`Appearance`）
- 外観（`Appearance`）と描画計画（`DrawPlanResolver`）による見た目の差し替え（`sample/Presentation/`。 格子線と四隅の鉤の例）
- `IsVisible`、`Clear()`、`RequestRender()`
- `Log`（描画エラーの通知先 ILogger 設定例）
