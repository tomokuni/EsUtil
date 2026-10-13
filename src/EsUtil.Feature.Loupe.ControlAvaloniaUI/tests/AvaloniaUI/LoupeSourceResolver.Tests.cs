using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Feature.Loupe.AvaloniaUI.Internal;
using Xunit;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests;

/// <summary><see cref="LoupeSourceResolver"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ルーペのソース画像（表示アダプタ / 他のアセンブリの表示アダプタ / null）から切り出し元の解決ロジックを検証する。<br/>
/// <see cref="Bitmap"/> 由来の変換（<c>ToEsBitmap()</c>）は <c>WriteableBitmap</c> 生成を伴うため<br/>
/// <see cref="LoupeControlTests"/>（ヘッドレス直列）が担う。<br/>
/// UI スレッドに依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class LoupeSourceResolverTests
{
    [Fact]
    internal void Resolve_nullは下地のみにフォールバックする()
    {
        var resolved = LoupeSourceResolver.Resolve(source: null, out var bitmap);

        Assert.Null(bitmap);
        Assert.False(resolved.IsSourceBitmap);
    }

    [Fact]
    internal void Resolve_EsBitmapAvaloniaAdapterは借用を返す()
    {
        using var bmp = EsBitmap.Create(10, 10);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var image = new EsBitmapAvaloniaAdapter(read);

        var resolved = LoupeSourceResolver.Resolve(image, out var bitmap);

        // 借用（アダプタが内包するコアの画像を取り出す）で、破棄は呼び出し側で行わない
        Assert.Same(read, bitmap);
        Assert.False(resolved.IsSourceBitmap);
    }

    [Fact]
    internal void Resolve_他のアセンブリの表示アダプタも借用を返す()
    {
        // Platform 層のアダプタのようにルーペが型を参照できないアダプタでも、公開契約（GetEsBitmap）で解決する
        using var bmp = EsBitmap.Create(10, 10);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        var image = new ForeignAdapter(read);

        var resolved = LoupeSourceResolver.Resolve(image, out var bitmap);

        Assert.Same(read, bitmap);
        Assert.False(resolved.IsSourceBitmap);
    }

    [Fact]
    internal void Resolve_破棄済みの真実を内包する表示アダプタは下地のみにフォールバックする()
    {
        var bmp = EsBitmap.Create(10, 10);
        bmp.SetReadOnly();
        var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var image = new EsBitmapAvaloniaAdapter(read);
        read.Dispose();

        var resolved = LoupeSourceResolver.Resolve(image, out var bitmap);

        // 破棄済みの真実は渡さない（無効な画像を切り出し元にしない）
        Assert.Null(bitmap);
        Assert.False(resolved.IsSourceBitmap);
    }

    // ====================================================================
    // IsSourceSupported（対応可能型の判定）
    // ====================================================================

    [Fact]
    internal void IsSourceSupported_nullは対応可能である()
    {
        // null は「画像なし」を表すため対応可能（下地のみで描画する）
        Assert.True(LoupeSourceResolver.IsSourceSupported(source: null));
    }

    [Fact]
    internal void IsSourceSupported_EsBitmapAvaloniaAdapterは対応可能である()
    {
        using var bmp = EsBitmap.Create(10, 10);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);
        using var image = new EsBitmapAvaloniaAdapter(read);

        Assert.True(LoupeSourceResolver.IsSourceSupported(image));
    }

    [Fact]
    internal void IsSourceSupported_他のアセンブリの表示アダプタも対応可能である()
    {
        using var bmp = EsBitmap.Create(10, 10);
        bmp.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(bmp);

        Assert.True(LoupeSourceResolver.IsSourceSupported(new ForeignAdapter(read)));
    }

    /// <summary>ルーペが型を参照できないアセンブリの表示アダプタを模した画像（公開契約だけを持つ）。</summary>
    /// <param name="image">内包するコアの画像。</param>
    private sealed class ForeignAdapter(EsReadOnlyBitmap image) : IImage
    {
        /// <inheritdoc/>
        public Size Size => new(image.Width, image.Height);

        /// <inheritdoc/>
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
        {
        }

        /// <summary>内包するコアの画像を借用で取得する。</summary>
        /// <returns>内包するコアの画像。</returns>
        public EsReadOnlyBitmap GetEsBitmap() => image;
    }
}
