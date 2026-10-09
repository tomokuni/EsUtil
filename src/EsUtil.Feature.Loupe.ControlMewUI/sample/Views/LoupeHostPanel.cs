using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe.MewUI.Sample.Views;

/// <summary>テスト画像とルーペを重ねるホスト パネル（ルーペの配置とマウス位置の通知を担う）。</summary>
/// <remarks>
/// <b>示すこと</b>: ルーペの表示位置・表示サイズはコアが決める（<c>LoupeControl.ResolveLayout</c>）ため、<br/>
/// ホストは結果の矩形で子を配置するだけでよい（位置の計算もクランプも持たない）。<br/>
/// <br/>
/// <b>座標</b>: 画像は元のピクセル サイズの DIP で 1:1 に置くため、パネル内の DIP 座標は元画像ピクセル座標と一致する。<br/>
/// マウス位置は <see cref="PointerMoved"/> で通知し、ルーペの中心の設定はホスト（ウィンドウ）が行う。<br/>
/// <br/>
/// <b>選択矩形の位置</b>: 画像上の選択矩形は <see cref="SelectionRectOverlay"/>（破線枠）と座標ラベルで示す<br/>
/// （画像とルーペのどちらが先に描かれるかは本パネルの子の順序が決める。 ルーペは選択矩形より後に置く）。
/// </remarks>
internal sealed class LoupeHostPanel : Panel
{
    /// <summary>背景のテスト画像。</summary>
    private readonly Image _image;

    /// <summary>選択矩形の位置を示す破線枠（画像の上へ重ねる）。</summary>
    private readonly SelectionRectOverlay _overlay = new();

    /// <summary>選択矩形の座標ラベル（破線枠の左上へ重ねる）。</summary>
    private readonly TextBlock _selectionLabel = new();

    /// <summary>重ねるルーペ（図形のみ。 入力は受けない）。</summary>
    private readonly List<LoupeControl> _loupes = [];

    /// <summary>テスト画像のピクセル サイズ（DIP で 1:1 に置く）。</summary>
    private readonly EsSize _sourceSize;

    /// <summary>選択矩形（対象座標）。 null または非表示の場合は描画しない。</summary>
    private EsRect? _selectionRect;

    /// <summary>選択矩形を表示するかどうか。</summary>
    private bool _isSelectionVisible;

    /// <summary>テスト画像とルーペを重ねるホスト パネルを初期化する。</summary>
    /// <param name="image">背景のテスト画像。</param>
    /// <param name="sourceSize">テスト画像のピクセル サイズ。</param>
    public LoupeHostPanel(Image image, EsSize sourceSize)
    {
        _image = image;
        _sourceSize = sourceSize;

        // ラベルは破線枠の左上へ置く（はみ出しを抑えるため 11 DIP の小さめの文字にする）
        _selectionLabel.FontSize(11);
        _selectionLabel.Foreground(Color.FromRgb(0xFF, 0x8C, 0x00));
        _selectionLabel.IsVisible = false;

        Add(image);
        Add(_overlay);
        Add(_selectionLabel);
    }

    /// <summary>画像上のマウス移動を通知する（パネル内の DIP 座標 = 元画像ピクセル座標）。</summary>
    internal event EventHandler<PointerMovedEventArgs>? PointerMoved;

    /// <summary>マウス移動を通知する（ルーペの中心の設定はホストが行う）。</summary>
    /// <param name="e">マウス イベント引数。</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var position = e.GetPosition(this);
        PointerMoved?.Invoke(this, new PointerMovedEventArgs(new EsPoint(position.X, position.Y)));
    }

    /// <summary>重ねるルーペを追加する。</summary>
    /// <param name="loupe">追加するルーペ（入力は受けない設定にする）。</param>
    /// <remarks>ルーペは選択矩形の破線枠より後に置く（ルーペの上に破線が重ならない）。</remarks>
    internal void AddLoupe(LoupeControl loupe)
    {
        loupe.IsHitTestVisible = false;
        _loupes.Add(loupe);
        Add(loupe);
    }

    /// <summary>画像上へ示す選択矩形を設定する。</summary>
    /// <param name="rect">選択矩形（対象座標 = 元画像ピクセル座標）。</param>
    /// <param name="isVisible">表示するかどうか。</param>
    /// <param name="label">座標ラベルの文言。 null または空白の場合はラベルを表示しない。</param>
    internal void SetSelectionRect(EsRect rect, bool isVisible, string? label)
    {
        _selectionRect = rect;
        _isSelectionVisible = isVisible && rect.Width > 0 && rect.Height > 0;

        _overlay.SetRect(_isSelectionVisible ? rect : null);
        _overlay.IsVisible = _isSelectionVisible;

        _selectionLabel.Text = label ?? string.Empty;
        _selectionLabel.IsVisible = _isSelectionVisible && !string.IsNullOrWhiteSpace(label);

        InvalidateArrange();
    }

    /// <summary>テスト画像のサイズを要求サイズとして返す。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ。</param>
    /// <returns>テスト画像のピクセル サイズ。</returns>
    protected override Size MeasureContent(Size availableSize)
    {
        _image.Measure(new Size(_sourceSize.Width, _sourceSize.Height));
        _overlay.Measure(availableSize);
        _selectionLabel.Measure(availableSize);

        foreach (var loupe in _loupes)
        {
            loupe.Measure(availableSize);
        }

        return new Size(_sourceSize.Width, _sourceSize.Height);
    }

    /// <summary>テスト画像・選択矩形の破線枠・ルーペを配置する。</summary>
    /// <param name="bounds">本パネルへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// ルーペはコアが解決した位置・サイズへ置き、非表示（拡大中心が未設定・親領域が表示サイズを収められない）の場合は<br/>
    /// サイズ 0 で配置する（「配置しない」の判断はコアが単一所有する）。
    /// </remarks>
    protected override void ArrangeContent(Rect bounds)
    {
        _image.Arrange(new Rect(bounds.X, bounds.Y, _sourceSize.Width, _sourceSize.Height));

        ArrangeSelection(bounds);

        foreach (var loupe in _loupes)
        {
            var layout = loupe.IsVisible
                ? loupe.ResolveLayout(new EsSize(bounds.Size.Width, bounds.Size.Height))
                : default;

            if (layout.Position is not { } position)
            {
                loupe.Arrange(new Rect(bounds.X, bounds.Y, 0, 0));
                continue;
            }

            loupe.Arrange(new Rect(bounds.X + position.X, bounds.Y + position.Y, layout.ClientSize, layout.ClientSize));
        }
    }

    /// <summary>選択矩形の破線枠と座標ラベルを配置する。</summary>
    /// <param name="bounds">本パネルへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// 画像を DIP で 1:1 に置くため、選択矩形（対象座標 = 元画像ピクセル）の座標をそのまま加算して配置できる。<br/>
    /// ラベルは枠の左上の外側（上へ 20 DIP）へ置き、上端を超える場合は枠の内側へ落とす。
    /// </remarks>
    private void ArrangeSelection(Rect bounds)
    {
        if (!_isSelectionVisible || _selectionRect is not { } rect)
        {
            _overlay.Arrange(new Rect(bounds.X, bounds.Y, 0, 0));
            _selectionLabel.Arrange(new Rect(bounds.X, bounds.Y, 0, 0));
            return;
        }

        _overlay.Arrange(new Rect(bounds.X + rect.X, bounds.Y + rect.Y, rect.Width, rect.Height));

        var labelHeight = _selectionLabel.DesiredSize.Height;
        var labelTop = rect.Y - labelHeight - 1;
        if (bounds.Y + labelTop < bounds.Y)
        {
            labelTop = rect.Y + 1;
        }

        _selectionLabel.Arrange(new Rect(
            bounds.X + rect.X + 4,
            bounds.Y + labelTop,
            _selectionLabel.DesiredSize.Width,
            labelHeight));
    }
}
