using DrawingBrush = System.Drawing.SolidBrush;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinForms.Loupe.Sample.Views;

/// <summary>選択矩形の座標ラベルをルーペより前面に描く重ね紙。</summary>
/// <remarks>
/// <b>ルーペは子コントロールである</b>: 親の描画（<see cref="LoupeHostPanel.OnPaint"/>）は子より先に描かれるため、<br/>
/// 親がラベルを描くとルーペの下へ潜る（重なった部分の文字が欠ける。 実測 2026-11-02）。<br/>
/// <b>本コントロールは子として最後に置く</b>: 子は追加順に前面になるため、ルーペより後に置けばラベルが上になる。<br/>
/// <b>マウスは受け取らない</b>: 無効化（<see cref="Control.Enabled"/> = false）して、下のルーペと親へ操作を通す<br/>
/// （ラベルは見た目だけで、操作の対象ではない）。<br/>
/// <b>背景は半透明にする</b>: 画像の上でも文字が読めるよう、他 UI と同じ白 87% を敷く。
/// </remarks>
internal sealed class SelectionLabelOverlay : Control
{
    /// <summary>文字のフォント。</summary>
    private readonly Font _font;

    /// <summary>表示する文言。 null または空白の場合は描かない。</summary>
    private string? _text;

    /// <summary>重ね紙を初期化する。</summary>
    public SelectionLabelOverlay()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.SupportsTransparentBackColor,
            true);
        _font = new Font(Font.FontFamily, 9f, FontStyle.Bold);
        BackColor = DrawingColor.Transparent;
        Enabled = false;
        Visible = false;
    }

    /// <summary>表示する文言と、置く矩形を設定する。</summary>
    /// <param name="text">表示する文言。 null または空白の場合は隠す。</param>
    /// <param name="bounds">置く矩形（クライアント座標の物理ピクセル）。</param>
    internal void SetLabel(string? text, Rectangle bounds)
    {
        _text = text;
        Bounds = bounds;
        Visible = !string.IsNullOrWhiteSpace(text) && bounds.Width > 0 && bounds.Height > 0;
        Invalidate();
    }

    /// <summary>文言と背景を描く。</summary>
    /// <param name="e">描画のイベント引数。</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPaint(e);

        if (string.IsNullOrWhiteSpace(_text))
        {
            return;
        }

        using var background = new DrawingBrush(DrawingColor.FromArgb(0xDD, 0xFF, 0xFF, 0xFF));
        using var foreground = new DrawingBrush(DrawingColor.FromArgb(0xFF, 0xFF, 0x8C, 0x00));
        e.Graphics.FillRectangle(background, ClientRectangle);
        e.Graphics.DrawString(_text, _font, foreground, 2f, 0f);
    }

    /// <summary>保持している描画資源を解放する。</summary>
    /// <param name="disposing">マネージド資源も解放するかどうか。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
        }

        base.Dispose(disposing);
    }
}
