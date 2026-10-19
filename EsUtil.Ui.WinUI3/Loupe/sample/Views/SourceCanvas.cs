using EsUtil.Ui.WinUI3.Loupe.Sample.Internal;
using EsUtil.Ui.WinUI3.Loupe.Sample.Presentation;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace EsUtil.Ui.WinUI3.Loupe.Sample.Views;

/// <summary>テスト画像と選択矩形の破線枠を描くキャンバス（ルーペの土台になる面）。</summary>
/// <remarks>
/// <b>示すこと</b>: 拡大元の画像はコアの画素バッファ（<see cref="EsReadOnlyBitmap"/>）であり、<br/>
/// ホスト側でも Skia のキャンバスへ描ける（画像の変換は本サンプルの Internal の複製が担う）。<br/>
/// <br/>
/// <b>座標</b>: 本キャンバスは画像のピクセル サイズと同じ DIP サイズで置くため、<br/>
/// ホスト内の DIP 座標は元画像ピクセル座標と一致する（描画は表示倍率を掛けて物理ピクセルへ合わせる）。<br/>
/// <br/>
/// <b>入力</b>: 本キャンバス自身は入力を受けない（ホストの親が受ける。 破線は重ねるだけの表示である）。<br/>
/// <b>公開の理由</b>: XAML から配置するため、XAML が参照できる可視性（<c>public</c>）にする。
/// </remarks>
public sealed class SourceCanvas : SKXamlCanvas
{
    /// <summary>描くテスト画像。 null の場合は何も描かない。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>描く選択矩形（対象座標 = 元画像ピクセル座標）。 null の場合は描かない。</summary>
    private EsRect? _selection;

    /// <summary>選択矩形を表示するかどうか。</summary>
    private bool _isSelectionVisible;

    /// <summary>テスト画像と選択矩形の破線枠を描く。</summary>
    /// <param name="e">描画先の表面（Skia のキャンバス）と描画先の情報（物理ピクセルの大きさ）。</param>
    /// <remarks>
    /// 描画面は物理ピクセルであるため、DIP（= 元画像ピクセル座標）の値を表示倍率で換算して描く。<br/>
    /// 前フレームの内容を残さないよう、最初に透明で塗る（毎回の描画で全体を作り直す）。
    /// </remarks>
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var scaling = ActualWidth > 0 ? e.Info.Width / ActualWidth : 1.0;

        if (_source is { } source)
        {
            // 画像は元のピクセル サイズで 1:1 に置く（表示倍率ぶんの物理ピクセルへ描く）
            using var bitmap = source.ToSKBitmap();
            canvas.DrawBitmap(
                bitmap,
                new SKRect(0, 0, source.Width, source.Height),
                new SKRect(0, 0, (float)(source.Width * scaling), (float)(source.Height * scaling)));
        }

        if (_isSelectionVisible && _selection is { } selection)
        {
            SelectionRectPainter.Paint(
                canvas,
                new SKRect(
                    (float)(selection.X * scaling),
                    (float)(selection.Y * scaling),
                    (float)(selection.Right * scaling),
                    (float)(selection.Bottom * scaling)),
                scaling,
                withFill: true);
        }
    }

    /// <summary>描くテスト画像を設定する。</summary>
    /// <param name="source">テスト画像。 null の場合は画像を描かない。 借用であり破棄しない。</param>
    /// <remarks>設定すると描画を無効化する（次の描画で反映される）。</remarks>
    public void SetSource(EsReadOnlyBitmap? source)
    {
        _source = source;
        Invalidate();
    }

    /// <summary>描く選択矩形を設定する。</summary>
    /// <param name="rect">選択矩形（対象座標 = 元画像ピクセル座標）。</param>
    /// <param name="isVisible">表示するかどうか。</param>
    /// <remarks>空の矩形（幅または高さが 0 以下）は描かない（判断は呼び出し側と描画側の 1 箇所に置く）。</remarks>
    public void SetSelection(EsRect rect, bool isVisible)
    {
        _selection = rect;
        _isSelectionVisible = isVisible;
        Invalidate();
    }
}
