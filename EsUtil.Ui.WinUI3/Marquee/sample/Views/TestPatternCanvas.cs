using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace EsUtil.Ui.WinUI3.Marquee.Sample.Views;

/// <summary>選択の対象（背景のテスト パターン）を表示する面。</summary>
/// <remarks>
/// <b>示すこと</b>: コントロールは選択の装飾だけを表示し、背景は透明のまま残す（下の内容が透ける）。<br/>
/// そのため背景は別の面（本クラス）が表示する。 画像ファイルを用意せずに選択の位置と寸法が読めるよう、<br/>
/// 格子・色見本・中心の十字を置く。<br/>
/// <b>描画ライブラリを使わない</b>: 要素（<see cref="Rectangle"/>）とブラシだけでパターンを表す<br/>
/// （移行前は Skia の描画面へ直接描いていた）。
/// </remarks>
public sealed partial class TestPatternCanvas : Canvas
{
    /// <summary>格子の間隔（DIP）。</summary>
    private const double GridStep = 50.0;

    /// <summary>色見本の一辺（DIP）。</summary>
    private const double SwatchSize = 80.0;

    /// <summary>格子線の色。</summary>
    private static readonly Color s_gridColor = Color.FromArgb(0xFF, 0x60, 0x60, 0x60);

    /// <summary>色見本の色（左から順に並べる）。</summary>
    private static readonly Color[] s_swatchColors =
    [
        Color.FromArgb(0xFF, 0xE5, 0x39, 0x35),
        Color.FromArgb(0xFF, 0xFB, 0x8C, 0x00),
        Color.FromArgb(0xFF, 0xFD, 0xD8, 0x35),
        Color.FromArgb(0xFF, 0x43, 0xA0, 0x47),
        Color.FromArgb(0xFF, 0x1E, 0x88, 0xE5),
        Color.FromArgb(0xFF, 0x8E, 0x24, 0xAA),
    ];

    /// <summary>パターンを表示する面を初期化する。</summary>
    /// <remarks>寸法が決まった時点でパターンを組み立てる（要素の数が寸法に依存するため）。</remarks>
    public TestPatternCanvas()
    {
        IsHitTestVisible = false;
        SizeChanged += (_, e) => Rebuild(e.NewSize.Width, e.NewSize.Height);
    }

    /// <summary>パターンを組み立てる。</summary>
    /// <param name="width">幅（DIP）。</param>
    /// <param name="height">高さ（DIP）。</param>
    private void Rebuild(double width, double height)
    {
        Children.Clear();
        if (width <= 0 || height <= 0)
        {
            return;
        }

        // 下地（濃い灰色）
        AddRect(0, 0, width, height, Color.FromArgb(0xFF, 0x30, 0x30, 0x30));

        for (var x = GridStep; x < width; x += GridStep)
        {
            AddRect(x, 0, 1, height, s_gridColor);
        }

        for (var y = GridStep; y < height; y += GridStep)
        {
            AddRect(0, y, width, 1, s_gridColor);
        }

        for (var index = 0; index < s_swatchColors.Length; index++)
        {
            var x = 20.0 + (index * (SwatchSize + 10.0));
            var y = height - SwatchSize - 20.0;
            if (x + SwatchSize > width || y < 0)
            {
                break;
            }

            AddRect(x, y, SwatchSize, SwatchSize, s_swatchColors[index]);
        }

        // 中心の十字（選択の基準を目視できるようにする）
        const double Length = 20;
        var white = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        AddRect((width / 2) - Length, height / 2, Length * 2, 1, white);
        AddRect(width / 2, (height / 2) - Length, 1, Length * 2, white);
    }

    /// <summary>矩形を 1 つ置く。</summary>
    /// <param name="x">左端（DIP）。</param>
    /// <param name="y">上端（DIP）。</param>
    /// <param name="width">幅（DIP）。</param>
    /// <param name="height">高さ（DIP）。</param>
    /// <param name="color">塗り色。</param>
    private void AddRect(double x, double y, double width, double height, Color color)
    {
        var rectangle = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new SolidColorBrush(color),
            IsHitTestVisible = false,
        };

        SetLeft(rectangle, x);
        SetTop(rectangle, y);
        Children.Add(rectangle);
    }
}
