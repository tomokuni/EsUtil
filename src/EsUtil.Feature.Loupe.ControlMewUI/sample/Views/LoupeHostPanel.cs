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
/// マウス位置は <see cref="PointerMoved"/> で通知し、ルーペの中心の設定はホスト（ウィンドウ）が行う。
/// </remarks>
internal sealed class LoupeHostPanel : Panel
{
    /// <summary>背景のテスト画像。</summary>
    private readonly Image _image;

    /// <summary>重ねるルーペ（図形のみ。 入力は受けない）。</summary>
    private readonly List<LoupeControl> _loupes = [];

    /// <summary>テスト画像のピクセル サイズ（DIP で 1:1 に置く）。</summary>
    private readonly EsSize _sourceSize;

    /// <summary>テスト画像とルーペを重ねるホスト パネルを初期化する。</summary>
    /// <param name="image">背景のテスト画像。</param>
    /// <param name="sourceSize">テスト画像のピクセル サイズ。</param>
    public LoupeHostPanel(Image image, EsSize sourceSize)
    {
        _image = image;
        _sourceSize = sourceSize;
        Add(image);
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
    internal void AddLoupe(LoupeControl loupe)
    {
        loupe.IsHitTestVisible = false;
        _loupes.Add(loupe);
        Add(loupe);
    }

    /// <summary>テスト画像のサイズを要求サイズとして返す。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ。</param>
    /// <returns>テスト画像のピクセル サイズ。</returns>
    protected override Size MeasureContent(Size availableSize)
    {
        _image.Measure(new Size(_sourceSize.Width, _sourceSize.Height));

        foreach (var loupe in _loupes)
        {
            loupe.Measure(availableSize);
        }

        return new Size(_sourceSize.Width, _sourceSize.Height);
    }

    /// <summary>テスト画像とルーペを配置する（ルーペはコアが解決した位置・サイズへ置く）。</summary>
    /// <param name="bounds">本パネルへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    /// <remarks>
    /// 非表示（拡大中心が未設定・親領域が表示サイズを収められない）の場合はサイズ 0 で配置する<br/>
    /// （「配置しない」の判断はコアが単一所有する）。
    /// </remarks>
    protected override void ArrangeContent(Rect bounds)
    {
        _image.Arrange(new Rect(bounds.X, bounds.Y, _sourceSize.Width, _sourceSize.Height));

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
}
