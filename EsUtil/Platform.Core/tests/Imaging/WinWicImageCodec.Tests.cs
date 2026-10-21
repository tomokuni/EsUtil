using System.Drawing;
using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Imaging;

/// <summary>読み込み時に EXIF の向き（<c>Orientation</c>）を適用することを検証するテスト（Windows 専用・並列実行）。</summary>
/// <remarks>
/// <b>向きを持つ画像を自前で組み立てる理由</b>: WIC のエンコーダーで向きを書き出すにはメタデータの書き込み器が要り、
/// 検証したいのは<b>読み込み側</b>である。 JPEG の APP1（EXIF）セグメントを差し込む方が、
/// 実際のカメラが作るファイルに近く、依存も増えない。<br/>
/// <b>寸法で判定する理由</b>: 向き 6 / 8 は縦横が入れ替わるため、画素を比べなくても適用の有無が分かる
/// （JPEG は非可逆であり画素の一致は期待できない）。
/// </remarks>
[SupportedOSPlatform("windows")]
public class WinWicImageCodecTests
{
    /// <summary>一時ファイルのパスを作る。</summary>
    /// <param name="extension">拡張子（先頭のドットを含む）。</param>
    /// <returns>一時フォルダー内の一意なパス。</returns>
    private static string TempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"se_exif_{Guid.NewGuid():N}{extension}");

    [Theory]
    [InlineData(1, 8, 4)]
    [InlineData(2, 8, 4)]
    [InlineData(3, 8, 4)]
    [InlineData(4, 8, 4)]
    [InlineData(5, 4, 8)]
    [InlineData(6, 4, 8)]
    [InlineData(7, 4, 8)]
    [InlineData(8, 4, 8)]
    internal void Load_EXIFの向きを適用する(ushort orientation, int expectedWidth, int expectedHeight)
    {
        var path = TempPath(".jpg");
        try
        {
            WriteJpegWithOrientation(path, 8, 4, orientation);

            using var loaded = EsReadOnlyBitmap.Load(path);

            Assert.Equal(expectedWidth, loaded.Size.Width);
            Assert.Equal(expectedHeight, loaded.Size.Height);
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    internal void Load_向きが無い画像は元の寸法のまま()
    {
        var path = TempPath(".jpg");
        try
        {
            using (var source = EsBitmap.Create(8, 4, Color.FromArgb(255, 10, 20, 30)))
            {
                using var read = EsReadOnlyBitmap.FromBitmap(source);
                read.Save(path);
            }

            using var loaded = EsReadOnlyBitmap.Load(path);

            Assert.Equal(8, loaded.Size.Width);
            Assert.Equal(4, loaded.Size.Height);
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    /// <summary>向きを持つ JPEG を書き出す。</summary>
    /// <param name="path">保存先のパス。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="orientation">EXIF の向き（1〜8）。</param>
    private static void WriteJpegWithOrientation(string path, int width, int height, ushort orientation)
    {
        using (var source = EsBitmap.Create(width, height, Color.FromArgb(255, 10, 20, 30)))
        {
            using var read = EsReadOnlyBitmap.FromBitmap(source);
            read.Save(path);
        }

        File.WriteAllBytes(path, AddExifOrientation(File.ReadAllBytes(path), orientation));
    }

    /// <summary>JPEG の先頭（<c>SOI</c>）の直後へ EXIF の向きを差し込む。</summary>
    /// <param name="jpeg">元の JPEG。</param>
    /// <param name="orientation">EXIF の向き（1〜8）。</param>
    /// <returns>向きを持つ JPEG。</returns>
    /// <remarks>
    /// <b>並び</b>: <c>APP1</c> マーカー（<c>FFE1</c>）→ 長さ（自身を含む 2 バイト）→ <c>Exif\0\0</c> →
    /// TIFF ヘッダー（リトルエンディアン）→ IFD0（1 件だけ）→ 次の IFD の位置（0）。<br/>
    /// <b>IFD0 の 1 件</b>: タグ 274（<c>Orientation</c>）/ 型 3（<c>SHORT</c>）/ 個数 1 / 値（4 バイトへ詰める）。
    /// </remarks>
    private static byte[] AddExifOrientation(byte[] jpeg, ushort orientation)
    {
        var segment = new List<byte>
        {
            0xFF, 0xE1,                         // APP1
            0x00, 0x00,                         // 長さ（後で埋める）
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00, // "Exif\0\0"
            0x49, 0x49, 0x2A, 0x00,             // "II"（リトルエンディアン）と 42
            0x08, 0x00, 0x00, 0x00,             // IFD0 の位置（8）
            0x01, 0x00,                         // エントリ数（1）
            0x12, 0x01,                         // タグ 274（Orientation）
            0x03, 0x00,                         // 型 3（SHORT）
            0x01, 0x00, 0x00, 0x00,             // 個数（1）
            (byte)(orientation & 0xFF), (byte)(orientation >> 8), 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,             // 次の IFD の位置（0）
        };

        // 長さは自身（2 バイト）を含み、マーカー（2 バイト）を含まない
        var length = segment.Count - 2;
        segment[2] = (byte)(length >> 8);
        segment[3] = (byte)(length & 0xFF);

        return [.. jpeg.AsSpan(0, 2), .. segment, .. jpeg.AsSpan(2)];
    }
}
