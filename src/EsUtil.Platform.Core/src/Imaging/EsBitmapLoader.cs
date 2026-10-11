using System.Runtime.Versioning;
using EsUtil.Platform.Core.Native.Win;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>ファイルの読み込み（デコード）の手順を 2 型で 1 回だけ書く内部ヘルパー。</summary>
/// <remarks>
/// <b>本型に置く理由</b>: デコードした画素を整列したバッファへ書き込む手順は、読み取り専用の画像
/// （<see cref="EsReadOnlyBitmap"/>）と書き換え可能な画像（<see cref="EsBitmap"/>）で同一であり、
/// <b>公開面の <c>Load</c> は戻り型だけが違う</b>。 共通の手順をここへ集約し、公開面は生成する型を渡すだけにする。<br/>
/// <b>0 埋めを省く</b>: 生成の直後にデコード結果で全領域を書くため、未初期化で確保する。<br/>
/// <b>中間バッファを作らない</b>: デコードは確保した画素領域へ直接書く（実測: 4K PNG の読み込みで 29.9 ms。
/// 中間バッファを挟むと +31 MiB と +2.7 ms ＝ +8%）。<br/>
/// <b>Windows 専用</b>（デコードの実装は GDI+ のエンコーダーであり、<see cref="WinGdiPlusImageCodec"/> が単一所有する）。
/// </remarks>
internal static class EsBitmapLoader
{
    /// <summary>ファイルを読み込んで画像を生成する（公開面の <c>Load</c> の共通手順）。</summary>
    /// <typeparam name="T">生成する画像の型（<see cref="EsReadOnlyBitmap"/> または <see cref="EsBitmap"/>）。</typeparam>
    /// <param name="filePath">読み込むファイルのパス（形式は内容から判別される）。 null 不可。</param>
    /// <param name="create">確保した画素バッファを受け取って画像を生成する処理。</param>
    /// <returns>読み込んだ画像。 所有権は呼び出し側へ移る。</returns>
    /// <remarks><b><paramref name="create"/> が例外を投げた場合は確保した領域を解放する</b>（呼び出し側に解放の機会が無いため）。</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合、ファイルが存在しない場合、
    /// または画像として解釈できない場合。</exception>
    [SupportedOSPlatform("windows")]
    internal static T Load<T>(string filePath, Func<EsBitmapBuffer, T> create)
        where T : EsReadOnlyBitmap
        => WinGdiPlusImageCodec.DecodeFromFile(filePath, (width, height, fill) =>
        {
            var buffer = EsBitmapBuffer.Allocate(width, height, clear: false);
            try
            {
                fill(buffer.Pixels);
                return create(buffer);
            }
            catch
            {
                // 確保した領域を解放してから例外を伝える（呼び出し側に解放の機会が無い）
                buffer.Dispose();
                throw;
            }
        });
}
