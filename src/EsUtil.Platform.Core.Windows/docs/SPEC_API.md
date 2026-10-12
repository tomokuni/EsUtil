# EsUtil.Platform.Core.Windows API リファレンス

`EsUtil.Platform.Core` の画像型と `System.Drawing.Bitmap` を相互変換する公開 API を記載します。

## 目次 <!-- omit from toc -->

- [1. 名前空間と型](#1-名前空間と型)
- [2. 公開 API](#2-公開-api)
  - [2.1 `Imaging.EsBitmapDrawingExtensions`（static class）](#21-imagingesbitmapdrawingextensionsstatic-class)
- [3. 使用例](#3-使用例)
- [4. 制限事項](#4-制限事項)
- [5. 関連ドキュメント](#5-関連ドキュメント)

## 1. 名前空間と型

| 名前空間 | 型 | 用途 |
| --- | --- | --- |
| `EsUtil.Platform.Core.Windows.Imaging` | `EsBitmapDrawingExtensions` | コアの画像と `System.Drawing.Bitmap` の相互変換（Windows 専用） |

`EsReadOnlyBitmap` / `EsBitmap` は `EsUtil.Platform.Core` が提供します（[`EsUtil.Platform.Core` の API リファレンス](../../EsUtil.Platform.Core/docs/SPEC_API.md)）。

## 2. 公開 API

### 2.1 `Imaging.EsBitmapDrawingExtensions`（static class）

コアの画像と `System.Drawing.Bitmap` の相互変換です（`System.Drawing.Common` を使う Windows 専用）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `ToEsReadOnlyBitmap` | `static EsReadOnlyBitmap ToEsReadOnlyBitmap(this Bitmap bitmap)` | `System.Drawing.Bitmap` から**読み取り専用の**画像を作る（複製。 元の Bitmap は変更しない） |
| `ToEsBitmap` | `static EsBitmap ToEsBitmap(this Bitmap bitmap)` | `System.Drawing.Bitmap` から**書き換え可能な**画像を作る（複製。 元の Bitmap は変更しない） |
| `ToDrawingBitmap` | `static Bitmap ToDrawingBitmap(this EsReadOnlyBitmap bitmap)` | コアの画像から `System.Drawing.Bitmap`（`Format32bppPArgb`）を作る（複製） |

- **転送は「1 枚につき 1 回のコピー」を守ります**。 `ToEsReadOnlyBitmap` が書き換え可能な画像を
  経由すると**バッファを 2 つ作って全領域を 2 回コピー**する（確保量と所要がそこで倍になる）ため、読み取り専用の画像へ直接 1 回だけ書き込みます
  （`EsReadOnlyBitmap` の内部ファクトリ `CreateFilled` が確保と充填を 1 回で行います）。
- **行幅が一致する場合は連続領域として 1 回でコピーします**（`LockBits` のストライドが `幅 × 4` のとき）。
  一致しない場合は行単位でコピーし、覆われない右端は 0 にします（元の Bitmap の仕様上ありえます）。
- **ピクセルの形式は `Format32bppPArgb`（アルファ乗算済みの BGRA8888）です**（コアの画像と同じ並び）。 部分的なアルファも保持します。
- **`System.Drawing.Common` は Windows 専用です**（非 Windows では動作しません）。 本クラスの公開メンバーは `SupportedOSPlatform("windows")` で限定します。

## 3. 使用例

```csharp
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Windows.Imaging;
using System.Drawing;

using var source = new Bitmap(640, 480, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

// コアの画像へ（複製。 読み取り専用）
using EsReadOnlyBitmap image = source.ToEsReadOnlyBitmap();

// コアの画像から（複製）
using Bitmap back = image.ToDrawingBitmap();
```

**書き換えが要る場合は `ToEsBitmap` を使います**（読み取り専用の画像は書き込みの入口を公開しません）。

## 4. 制限事項

- **Windows 専用です**（`System.Drawing.Common` の制約。 非 Windows では動作しません）。
- **変換は必ず複製を作ります**（結果の破棄は呼び出し側が行います。 引数の所有権は移動しません）。
- **`System.Drawing.Bitmap` の寿命を越えて使えません**: 返される画像は複製であるため元の Bitmap を破棄しても有効ですが、逆方向の変換（`ToDrawingBitmap`）は返された `Bitmap` を呼び出し側が破棄します。

## 5. 関連ドキュメント

- 変換元の画像型（`EsReadOnlyBitmap` / `EsBitmap`） … [`EsUtil.Platform.Core` の API リファレンス](../../EsUtil.Platform.Core/docs/SPEC_API.md)
- 参照の許可範囲 … [`.github/依存関係.md`](../../../.github/依存関係.md)
