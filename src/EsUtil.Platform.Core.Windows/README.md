# EsUtil.Platform.Core.Windows

`EsUtil.Platform.Core` の画像型（`EsBitmap` / `EsBitmap`）と **`System.Drawing.Bitmap` を相互変換する Windows 専用の拡張メソッド**を提供するライブラリです。

## 1. 目的

- **コアから `System.Drawing.Common` を切り離す**ため、Windows 専用の画像型との相互変換を本プロジェクトが単一所有します（`EsUtil.Platform.Core` は外部パッケージを参照しません）。
- 変換は **`LockBits` によるネイティブ コピー**で行い、中間の `byte[]` を介しません。
- **借用と所有を型で表します**: `System.Drawing.Bitmap` を受け取る変換は複製を作り（元は変更しない）、コアの画像から作る変換は複製を返します（引数の所有権は呼び出し側に残ります）。

## 2. 収録する型

| 型 | 内容 |
| --- | --- |
| `EsBitmapDrawingExtensions` | `System.Drawing.Bitmap` ⇔ コアの画像の相互変換（`ToEsBitmap` / `ToEsBitmap` / `ToDrawingBitmap`） |

## 3. 依存関係

| 種別 | 依存 |
| --- | --- |
| 参照するプロジェクト | `EsUtil.Platform.Core` |
| 参照するパッケージ | `System.Drawing.Common`（Microsoft 公式。 **Windows 専用**） |
| 参照される側 | 本リポジトリ内では `System.Drawing.Bitmap` を使う利用者が明示的に参照します（コアとコントロール層は参照しません） |

参照の許可範囲は [`.github/依存関係.md`](../../.github/依存関係.md) が所有します。

## 4. 主な API

```csharp
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Windows.Imaging;
using System.Drawing;

using var drawing = new Bitmap("capture.png");

// System.Drawing.Bitmap → コアの画像（複製。 読み取り専用）
using EsBitmap readOnlyImage = drawing.ToEsBitmap();

// System.Drawing.Bitmap → コアの画像（複製。 書き換え可能）
using EsBitmap editableImage = drawing.ToEsBitmap();

// コアの画像 → System.Drawing.Bitmap（複製）
using Bitmap back = editableImage.ToDrawingBitmap();
```

| メソッド | 説明 |
| --- | --- |
| `ToEsBitmap(this Bitmap)` | 読み取り専用の画像を作る（複製。 **書き換えが要る場合は `ToEsBitmap`**） |
| `ToEsBitmap(this Bitmap)` | 書き換え可能な画像を作る（複製） |
| `ToDrawingBitmap(this EsReadOnlyBitmap)` | `System.Drawing.Bitmap`（`Format32bppPArgb`）を作る（複製） |

- **アルファは乗算済みの BGRA8888 で扱います**（コアの画像と同じ並び）。 部分的なアルファ（半透明）も保持します。
- **`System.Drawing.Common` は Windows 専用です**（非 Windows では動作しません）。 公開メンバーは `SupportedOSPlatform("windows")` で限定します。

## 5. システム要件

- **Windows のみ**（`System.Drawing.Common` は .NET 7 以降、非 Windows では動作しません）
- .NET 10 以降

## 6. テスト

```powershell
dotnet test src/EsUtil.Platform.Core.Windows/tests/EsUtil.Platform.Core.Windows.Tests.csproj
```

変換（ピクセルの一致・部分的なアルファの保持・複製であること）を検証します。

## 7. ドキュメント

- [API リファレンス](docs/SPEC_API.md)
- [システム仕様書](docs/SPEC_System.md)
- 変換元の画像型 … [`EsUtil.Platform.Core`](../EsUtil.Platform.Core/README.md)

## 8. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
