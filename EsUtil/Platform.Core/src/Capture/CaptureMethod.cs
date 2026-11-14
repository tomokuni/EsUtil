namespace EsUtil.Platform.Core.Capture;

/// <summary>画面のキャプチャ方式。</summary>
/// <remarks>
/// <b>方式で何が変わるか</b>: 結果の画像は同じだが、撮れる内容と速さが違う。
/// <list type="bullet">
/// <item><see cref="Gdi"/>: 画面へ描かれた結果をコピーする。 単発では速いが、<b>他のウィンドウに隠れた領域は写らない</b>。</item>
/// <item><see cref="WindowsGraphicsCapture"/>: 合成前の内容を GPU から取得する。 隠れた領域も写るが、
/// <b>初回に GPU とセッションの初期化を伴う</b>ため単発では遅い（実測 2026-11-14: 400x300 で GDI 12ms / WGC 276ms）。</item>
/// </list>
/// <b>既定は <see cref="Gdi"/></b>: 単発撮影が中心の本アプリでは初期化のコストが見合わず、
/// また WGC は環境によって枠が出る・カーソルを外せない（<see cref="WindowsGraphicsCaptureSupport"/> を参照）。<br/>
/// <b>使えない環境がある</b>: 実際に使えるかは <see cref="WindowsGraphicsCaptureSupport"/> が返す。
/// 設定画面はその値で選択肢を無効にする。
/// </remarks>
public enum CaptureMethod
{
    /// <summary>GDI の <c>BitBlt</c>（既定。 単発撮影が速い）。</summary>
    Gdi,

    /// <summary>Windows.Graphics.Capture（隠れた領域も撮れる。 初回は遅い）。</summary>
    WindowsGraphicsCapture,
}
