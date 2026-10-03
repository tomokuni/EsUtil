using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Platform.Core.Native;

/// <summary>画面/ウィンドウのキャプチャ機能を抽象化するインターフェイス。</summary>
/// <remarks>
/// 画面領域のキャプチャ（画面コピー）を提供する。<br/>
/// 実装は各プラットフォーム（Windows: System.Drawing + Win32 API）が提供する。<br/>
/// <br/>
/// **現状の注意（Windows 専用）**: 共有プラットフォームには Linux / macOS の実装は提供しておらず、<br/>
/// 実質的な価値はテスト容易性（モック）とカプセル化（System.Drawing 詳細の隠蔽）にある。<br/>
/// <br/>
/// 公開 API は UI フレームワーク非依存である:<br/>
/// - 画像は <see cref="EsBitmap"/>（ピクセルコンテナ）で受け渡しする（System.Drawing 型は公開しない）。<br/>
///   <see cref="EsBitmap"/> は SKBitmap を内部に持つため、バックグラウンドスレッドからも安全に生成できる。<br/>
/// - 座標は画面物理ピクセルのコア型（<see cref="EsRectInt"/> / <see cref="EsPointInt"/>）で受け渡しする。<br/>
/// <br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、テスト時のモックが可能になる（DIP）。
/// </remarks>
public interface ICaptureService
{
    /// <summary>指定された画面領域をキャプチャする（EsBitmap で返す）。</summary>
    /// <param name="bounds">キャプチャする画面領域（画面物理座標）。</param>
    /// <returns>キャプチャした EsBitmap。 失敗時は null。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// <see cref="EsBitmap"/>（SKBitmap 内部）の生成はスレッド非依存のため、<br/>
    /// 画面キャプチャ（GDI 同期コール等）と同一のバックグラウンドスレッドで実行可能である。<br/>
    /// </remarks>
    EsBitmap? CaptureArea(EsRectInt bounds);

    /// <summary>仮想スクリーン（全モニタを包含する領域）全体を 1 回のキャプチャで取得する。</summary>
    /// <param name="origin">仮想スクリーンの原点（全モニタのバウンディングボックス左上、画面物理座標。 プライマリモニタが (0,0) にない場合は負値）。</param>
    /// <returns>仮想スクリーン全体の EsBitmap。 キャプチャ失敗時は null。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// 仮想スクリーン境界の取得（Windows: GetSystemMetrics）と全体キャプチャをプラットフォーム実装に委ね、<br/>
    /// 利用側（選択サブシステム）はプラットフォーム固有 API に依存しない（DIP）。<br/>
    /// <see cref="EsBitmap"/>（SKBitmap 内部）の生成はスレッド非依存のため、<br/>
    /// 画面キャプチャ（GDI 同期コール等）と同一のバックグラウンドスレッドで実行可能である。<br/>
    /// </remarks>
    EsBitmap? CaptureVirtualScreen(out EsPointInt origin);
}
