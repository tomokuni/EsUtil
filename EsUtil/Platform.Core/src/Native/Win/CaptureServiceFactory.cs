using EsUtil.Platform.Core.Capture;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>キャプチャ方式に対応する <see cref="ICaptureService"/> を作る。</summary>
/// <remarks>
/// <b>使えない方式は GDI へ落とす</b>: 設定に WGC が保存されていても、実行中の環境で使えない場合
/// （古い OS・GPU 不在・リモート セッション）は GDI を返す。 呼び出し側は方式を意識せずに撮影でき、
/// <b>撮影そのものが失敗しない</b>。<br/>
/// <br/>
/// <b>設定画面の表示と判定を一致させる</b>: 選択肢の有効・無効は <see cref="WindowsGraphicsCaptureSupport"/> が返し、
/// 本クラスも同じ判定を使う（画面では選べるのに撮れない、という食い違いを作らない）。
/// </remarks>
public static class CaptureServiceFactory
{
    /// <summary>指定の方式に対応するキャプチャ サービスを作る。</summary>
    /// <param name="method">設定された方式。</param>
    /// <param name="includeCursor">撮影にカーソルを含めるか。</param>
    /// <param name="hideBorder">撮影中の枠を消すか（消せない環境では無視される）。</param>
    /// <returns>キャプチャ サービス。 WGC が使えない環境では GDI の実装。</returns>
    /// <remarks>
    /// 返したインスタンスは呼び出し側が所有する（破棄は不要。 どちらも状態を持たない）。<br/>
    /// <b>カーソルを写さない設定で 1903 未満の場合</b>は、WGC がカーソルを外せないため GDI を返す（設定に反しない）。
    /// </remarks>
    public static ICaptureService Create(CaptureMethod method, bool includeCursor = true, bool hideBorder = true)
        => Resolve(method) == CaptureMethod.WindowsGraphicsCapture
            ? new WinGraphicsCaptureService(includeCursor, hideBorder)
            : new WinCaptureService();

    /// <summary>実際に使われる方式を返す（設定値が使えない場合は GDI）。</summary>
    /// <param name="method">設定された方式。</param>
    /// <returns>実際に使われる方式。</returns>
    /// <remarks>ログへ「設定は WGC だが GDI で撮った」と残すために使う。</remarks>
    public static CaptureMethod Resolve(CaptureMethod method)
        => method == CaptureMethod.WindowsGraphicsCapture && WindowsGraphicsCaptureSupport.IsSupported
            ? CaptureMethod.WindowsGraphicsCapture
            : CaptureMethod.Gdi;
}
