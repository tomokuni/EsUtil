using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using Xunit;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests;

/// <summary><see cref="LoupeSourceResolver"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ルーペのソース画像（表示アダプタ / null）から切り出し元の解決ロジックを検証する。<br/>
/// <see cref="Bitmap"/> 由来の変換（<c>ToEsWritableBitmap()</c>）は <c>WriteableBitmap</c> 生成を伴うため<br/>
/// <see cref="LoupeControlTests"/>（ヘッドレス直列）が担う。<br/>
/// UI スレッドに依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class LoupeSourceResolverTests
{
    [Fact]
    internal void Resolve_nullは市松模様のみにフォールバックする()
    {
        var resolved = LoupeSourceResolver.Resolve(source: null, out var bitmap);

        Assert.Null(bitmap);
        Assert.False(resolved.IsSourceBitmap);
    }

    [Fact]
    internal void Resolve_EsBitmapAvaloniaAdapterは借用を返す()
    {
        using var bmp = EsWritableBitmap.Create(10, 10);
        using var image = new EsBitmapAvaloniaAdapter(bmp);

        var resolved = LoupeSourceResolver.Resolve(image, out var bitmap);

        // 借用（渡した表示アダプタをそのまま読み取りの面として返す）で、破棄は呼び出し側で行わない
        Assert.Same(image, bitmap);
        Assert.False(resolved.IsSourceBitmap);
    }

    // ====================================================================
    // IsSourceSupported（対応可能型の判定）
    // ====================================================================

    [Fact]
    internal void IsSourceSupported_nullは対応可能である()
    {
        // null は「画像なし」を表すため対応可能（市松模様のみで描画する）
        Assert.True(LoupeSourceResolver.IsSourceSupported(source: null));
    }

    [Fact]
    internal void IsSourceSupported_EsBitmapAvaloniaAdapterは対応可能である()
    {
        using var bmp = EsWritableBitmap.Create(10, 10);
        using var image = new EsBitmapAvaloniaAdapter(bmp);

        Assert.True(LoupeSourceResolver.IsSourceSupported(image));
    }
}
