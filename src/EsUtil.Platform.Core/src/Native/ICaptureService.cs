using System.ComponentModel;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Platform.Core.Native;

/// <summary>画面のキャプチャ機能を抽象化するインターフェイス。</summary>
/// <remarks>
/// 画面領域のキャプチャ（画面コピー）を提供する。<br/>
/// 実装は各プラットフォーム（Windows: GDI の <c>BitBlt</c>）が提供する。<br/>
/// <br/>
/// 公開 API は UI フレームワーク非依存である:<br/>
/// - 画像は <see cref="EsWritableBitmap"/>（ピクセルコンテナ）で受け渡しする（Win32 / GDI の型は公開しない）。<br/>
///   <b>生成した画像は書き換え可能な型で返す</b>（この画像を所有し、破棄するのは呼び出し側であるため）。<br/>
/// - 座標は画面物理ピクセルのコア型（<see cref="EsRectInt"/> / <see cref="EsPointInt"/>）で受け渡しする。<br/>
/// <br/>
/// <b>失敗の表し方</b>: 失敗の種類で 2 つに分ける（[`.github/例外処理規約.md`](../../../../.github/例外処理規約.md) §2・§3）。<br/>
/// - <b>指定領域の取得失敗（回復できない）</b>: <see cref="CaptureArea"/> が <see cref="Win32Exception"/> を投げる<br/>
///   （<b>どの領域で失敗したか</b>を例外メッセージへ残す。 呼び出し側に代替手段が無い）。<br/>
/// - <b>仮想スクリーンの境界が測れない（回復できる）</b>: <see cref="CaptureVirtualScreen"/> が <see langword="null"/> を返す<br/>
///   （境界が取得できない状態ではキャプチャ自体が成立しないが、撮影の中止として扱えばアプリは継続できる）。<br/>
/// <b>本層はログを出力しない</b>: 失敗の記録は呼び出し側が行う。<br/>
/// <br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、テスト時のモックが可能になる（DIP）。
/// </remarks>
public interface ICaptureService
{
    /// <summary>指定された画面領域をキャプチャする。</summary>
    /// <param name="bounds">キャプチャ領域（画面物理座標。 幅と高さは正の値）。</param>
    /// <returns>キャプチャした画像。 破棄は呼び出し側。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bounds"/> の幅または高さが 0 以下の場合。</exception>
    /// <exception cref="Win32Exception">画面の取得に失敗した場合。</exception>
    /// <remarks>
    /// <b>null を返さない理由</b>: 呼び出し側は取得できないと撮影を成立させられないため、
    /// 失敗は例外にして<b>対象領域</b>と原因（Win32 のエラー コード）を残す。<br/>
    /// 画像の生成はスレッド非依存のため、画面キャプチャ（GDI の同期コール等）と同一のバックグラウンド スレッドで実行できる。
    /// </remarks>
    EsWritableBitmap CaptureArea(EsRectInt bounds);

    /// <summary>仮想スクリーン（全モニタを包含する領域）全体を 1 回のキャプチャで取得する。</summary>
    /// <param name="origin">仮想スクリーンの原点（全モニタのバウンディングボックス左上、画面物理座標）。</param>
    /// <returns>仮想スクリーン全体の画像。 境界を取得できない場合は <see langword="null"/>。 破棄は呼び出し側。</returns>
    /// <exception cref="Win32Exception">画面の取得に失敗した場合。</exception>
    /// <remarks>
    /// 仮想スクリーン境界の取得（Windows: <c>GetSystemMetrics</c>）と全体キャプチャをプラットフォーム実装に委ね、<br/>
    /// 利用側（選択サブシステム）はプラットフォーム固有 API に依存しない（DIP）。<br/>
    /// <b><see langword="null"/> の意味は 1 つだけである</b>: 仮想スクリーンの境界が取得できない
    /// （幅・高さが 0 以下。 セッション切断等）。 画面の取得自体の失敗は <see cref="Win32Exception"/> になる。
    /// </remarks>
    EsWritableBitmap? CaptureVirtualScreen(out EsPointInt origin);
}
