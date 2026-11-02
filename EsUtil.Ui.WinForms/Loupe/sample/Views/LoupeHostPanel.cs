using EsUtil.Ui.WinForms.Loupe.Sample.Internal;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using DrawingBrush = System.Drawing.SolidBrush;
using DrawingColor = System.Drawing.Color;
using DrawingPen = System.Drawing.Pen;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace EsUtil.Ui.WinForms.Loupe.Sample.Views;

/// <summary>テスト画像とルーペを重ねるホスト パネル（ルーペの配置とマウス位置の通知を担う）。</summary>
/// <remarks>
/// <b>示すこと</b>: ルーペの表示位置・表示サイズはコアが決める（<c>LoupeControl.ResolveLayout</c>）ため、<br/>
/// ホストは結果の矩形で子を配置するだけでよい（位置の計算もクランプも持たない）。<br/>
/// <br/>
/// <b>座標</b>: テスト画像は元のピクセル サイズのまま 1:1（物理ピクセル）で描くため、<br/>
/// パネル内のクライアント座標は元画像ピクセル座標と一致する。<br/>
/// 表示レイアウトの解決は DIP を要求するため、クライアント領域を <c>LoupeControl.ToDipSize</c> で換算して渡す。<br/>
/// <br/>
/// <b>選択矩形の位置</b>: 画像上の選択矩形は破線枠と座標ラベルで示す。 <b>破線枠はルーペの下</b>（子の描画が後）だが、<br/>
/// <b>座標ラベルはルーペの上</b>に置く（<see cref="SelectionLabelOverlay"/>。 ラベルがルーペに隠れると読めない。 実測 2026-11-02）。
/// </remarks>
internal sealed class LoupeHostPanel : Control
{
    /// <summary>背景のテスト画像（複製済み。 描画のたびには複製しない）。</summary>
    private readonly Bitmap _image;

    /// <summary>選択矩形の座標ラベルに使うフォント。</summary>
    private readonly Font _labelFont;

    /// <summary>重ねるルーペ。</summary>
    private readonly List<LoupeControl> _loupes = [];

    /// <summary>テスト画像のピクセル サイズ。</summary>
    private readonly EsSize _sourceSize;

    /// <summary>選択矩形（対象座標）。 null または非表示の場合は描画しない。</summary>
    private EsRect? _selectionRect;

    /// <summary>選択矩形を表示するかどうか。</summary>
    private bool _isSelectionVisible;

    /// <summary>選択矩形の座標ラベルの文言。 null または空白の場合は描画しない。</summary>
    private string? _selectionLabel;

    /// <summary>選択矩形の座標ラベルをルーペより前面に描く重ね紙。</summary>
    private readonly SelectionLabelOverlay _labelOverlay = new();

    /// <summary>テスト画像とルーペを重ねるホスト パネルを初期化する。</summary>
    /// <param name="source">背景のテスト画像（読み取りの面）。 null 不可。</param>
    /// <remarks>
    /// 背景は GDI+ で描くため、画像を 1 度だけ複製する<br/>
    /// （複製の実装はサンプルの <c>Internal</c> が持つ。 依存関係 §4.1）。
    /// </remarks>
    public LoupeHostPanel(EsReadOnlyBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _sourceSize = new EsSize(source.Width, source.Height);
        _image = source.ToDrawingBitmap();
        _labelFont = new Font(Font.FontFamily, 9f);

        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);

        // 座標ラベルは子として持ち、ルーペより前面に置く（親の描画は子より先のため、描くと潜る）
        Controls.Add(_labelOverlay);
    }

    /// <summary>画像上のマウス移動を通知する（パネル内のクライアント座標 = 元画像ピクセル座標）。</summary>
    internal event EventHandler<PointerMovedEventArgs>? PointerMoved;

    /// <summary>重ねるルーペを追加する。</summary>
    /// <param name="loupe">追加するルーペ。</param>
    /// <remarks>
    /// ルーペは子として加える（選択矩形の破線枠より後に描かれる）。<br/>
    /// <b>座標ラベルはルーペより前面に置く</b>: 子は追加順に前面になるため、ルーペを足すたびに重ね紙を最前面へ戻す。
    /// </remarks>
    internal void AddLoupe(LoupeControl loupe)
    {
        ArgumentNullException.ThrowIfNull(loupe);

        _loupes.Add(loupe);
        Controls.Add(loupe);
        _labelOverlay.BringToFront();
        UpdateLoupeBounds();
    }

    /// <summary>画像上へ示す選択矩形を設定する。</summary>
    /// <param name="rect">選択矩形（対象座標 = 元画像ピクセル座標）。</param>
    /// <param name="isVisible">表示するかどうか。</param>
    /// <param name="label">座標ラベルの文言。 null または空白の場合はラベルを描画しない。</param>
    internal void SetSelectionRect(EsRect rect, bool isVisible, string? label)
    {
        _selectionRect = rect;
        _isSelectionVisible = isVisible && rect.Width > 0 && rect.Height > 0;
        _selectionLabel = label;

        UpdateLabelOverlay();
        Invalidate();
    }

    /// <summary>座標ラベルの重ね紙の文言と位置を更新する。</summary>
    /// <remarks>
    /// 置く位置は選択矩形の左上の少し上（上に余白が無ければ矩形の内側）である（他 UI と同じ規則）。<br/>
    /// 大きさは文言の実寸から決める（文字が切れない）。
    /// </remarks>
    private void UpdateLabelOverlay()
    {
        if (!_isSelectionVisible || _selectionRect is not { } rect || string.IsNullOrWhiteSpace(_selectionLabel))
        {
            _labelOverlay.SetLabel(null, Rectangle.Empty);
            return;
        }

        var size = TextRenderer.MeasureText(_selectionLabel, _labelFont);
        var width = size.Width + 4;
        var height = size.Height;
        var top = (int)rect.Y - height - 1;
        if (top < 0)
        {
            top = (int)rect.Y + 1;
        }

        _labelOverlay.SetLabel(_selectionLabel, new Rectangle((int)rect.X + 4, top, width, height));
        _labelOverlay.BringToFront();
    }

    /// <summary>ルーペの配置（表示位置・表示サイズ）をコアの解決結果で更新する。</summary>
    /// <remarks>
    /// 拡大中心が未設定の場合と、親領域が表示サイズを収められない場合は大きさ 0 にする<br/>
    /// （「配置しない = 描画しない」の判断はコアが単一所有する）。<br/>
    /// マウス移動のたびに呼ばれるため、ルーペのサイズは DIP → 物理ピクセルへ換算して設定する。
    /// </remarks>
    internal void UpdateLoupeBounds()
    {
        var parentDip = _loupes.Count > 0 ? _loupes[0].ToDipSize(ClientSize) : default;

        foreach (var loupe in _loupes)
        {
            var layout = loupe.ResolveLayout(parentDip);
            if (layout.Position is not { } position)
            {
                loupe.Bounds = Rectangle.Empty;
                continue;
            }

            var size = loupe.ToClientPixel(layout.ClientSize);
            loupe.Bounds = new Rectangle(
                loupe.ToClientPixel(position.X), loupe.ToClientPixel(position.Y), size, size);
        }
    }

    /// <summary>背景のテスト画像と選択矩形の破線枠を描く。</summary>
    /// <param name="e">描画のイベント引数。</param>
    /// <remarks>ルーペは子コントロールであるため、本パネルの描画の後に（重なった部分だけ）描かれる。</remarks>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // 画像は 1:1（物理ピクセル）で描く（クライアント座標 = 元画像ピクセル座標 にする）
        e.Graphics.DrawImage(_image, 0, 0, _image.Width, _image.Height);

        PaintSelection(e.Graphics);
    }

    /// <summary>画像上のマウス移動を通知する。</summary>
    /// <param name="e">マウス イベント引数。</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        RaisePointerMoved(e.Location);
    }

    /// <summary>サイズが変わったときにルーペの配置を更新する。</summary>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// ルーペの表示位置は親領域の大きさで補正される（端に寄った場合の合わせ込み）ため、<br/>
    /// 親領域が変わったら解決し直す。
    /// </remarks>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateLoupeBounds();
    }

    /// <summary>テスト画像のピクセル サイズを要求サイズとして返す。</summary>
    /// <param name="proposedSize">親から提示されたサイズ。</param>
    /// <returns>テスト画像のピクセル サイズ。</returns>
    public override Size GetPreferredSize(Size proposedSize)
        => new((int)_sourceSize.Width, (int)_sourceSize.Height);

    /// <summary>保持している描画資源を解放する。</summary>
    /// <param name="disposing">マネージド資源も解放するかどうか。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _labelOverlay.Dispose();
            _labelFont.Dispose();
            _image.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>マウス位置（元画像ピクセル座標）を通知する。</summary>
    /// <param name="location">クライアント座標のマウス位置。</param>
    /// <remarks>クライアント座標 = 元画像ピクセル座標であるため、そのまま通知できる。</remarks>
    private void RaisePointerMoved(Point location)
    {
        var handler = PointerMoved;
        handler?.Invoke(this, new PointerMovedEventArgs(new EsPoint(location.X, location.Y)));
    }

    /// <summary>選択矩形の破線枠と座標ラベルを描く。</summary>
    /// <param name="graphics">描画先。</param>
    private void PaintSelection(Graphics graphics)
    {
        if (!_isSelectionVisible || _selectionRect is not { } rect)
        {
            return;
        }

        var bounds = new DrawingRectangleF((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);

        // 内側の塗りは他 UI と同じ半透明のオレンジ（枠だけだと選択範囲の広がりが分かりにくい）
        using var fill = new DrawingBrush(DrawingColor.FromArgb(0x30, 0xFF, 0x8C, 0x00));
        graphics.FillRectangle(fill, bounds);

        using var pen = new DrawingPen(DrawingColor.FromArgb(255, 255, 140, 0), 2f) { DashPattern = [3f, 2f] };
        graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);

        // 座標ラベルはここでは描かない（子の重ね紙がルーペより前面に描く）
    }
}
