using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinUI3.Interop.Drawing;

// 複製元: EsUtil.Ui.MewUI.Marquee/src/MewUI/MarqueeRenderedEventArgs.cs（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Ui.WinUI3.Marquee;

/// <summary>描画完了（コントロールが図形とラベルを描き終えたこと）を通知するイベント引数。</summary>
/// <remarks>
/// <b>ホスト固有の付加情報を描くための入口である</b>: サイズラベルとカーソル座標ラベルは<br/>
/// コントロールが描くが、操作ラベル・情報ラベルなどアプリ固有の情報は<br/>
/// 本コントロールの概念ではないため、ホストが本イベントで自前で描く。<br/>
/// <br/>
/// <b>描画 API は公開しない</b>: ホストは <see cref="AddRect"/> で矩形を追加する。<br/>
/// 追加した矩形はコントロールの図形より前面、ラベルより背面に置かれる。<br/>
/// <br/>
/// <b>座標系</b>: <see cref="AddRect"/> の座標は DIP である（コントロールの左上を原点とする）。
/// </remarks>
/// <param name="overlay">追加描画を受け取る矩形の列（DIP）。</param>
/// <param name="content">1 フレーム分の描画内容。</param>
public sealed class MarqueeRenderedEventArgs(DrawList overlay, MarqueeDrawPlan content) : EventArgs
{
    /// <summary>追加描画を受け取る矩形の列を取得する。</summary>
    /// <value>ホストが <see cref="AddRect"/> で追加した矩形の列。</value>
    internal DrawList Overlay { get; } = overlay;

    /// <summary>1 フレーム分の描画内容（装飾・ハンドル・始点マーカー・ラベル・層の順序）。</summary>
    /// <value>コントロールが実際に描画に使った値（描くものが無い項目は空・null）。</value>
    /// <remarks>
    /// ラベルの契約も含むため、ホストは注目矩形（<see cref="MarqueeDrawPlan.FocusDipRect"/>）を基準に付加情報を配置できる。<br/>
    /// 文字の有無は <see cref="MarqueeDrawPlan.HasText"/> で判定できる。
    /// </remarks>
    public MarqueeDrawPlan Content { get; } = content;

    /// <summary>コントロールの面へ矩形を追加する。</summary>
    /// <param name="x">左端（DIP）。</param>
    /// <param name="y">上端（DIP）。</param>
    /// <param name="width">幅（DIP）。 0 以下の場合は追加しない。</param>
    /// <param name="height">高さ（DIP）。 0 以下の場合は追加しない。</param>
    /// <param name="argb">塗り色（<c>0xAARRGGBB</c>。 透過度はアルファ）。</param>
    /// <remarks>
    /// 追加した矩形は計画の層（装飾・ハンドル・始点マーカー）より前面、ラベルより背面へ置かれる。<br/>
    /// 移行前は <c>SKCanvas</c> へ直接描いていたが、描画 API を公開しないため矩形の追加に置き換えた。
    /// </remarks>
    public void AddRect(double x, double y, double width, double height, uint argb)
        => Overlay.AddRect(new EsRect(x, y, width, height), System.Drawing.Color.FromArgb((int)argb));
}
