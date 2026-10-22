using System.Collections.Concurrent;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;

namespace EsUtil.Ui.WinUI3.Loupe.Sample.Views;

/// <summary>テスト画像と選択矩形の枠を表示する面（ルーペの土台になる面）。</summary>
/// <remarks>
/// <b>示すこと</b>: 拡大元の画像はコアの画素バッファ（<see cref="EsReadOnlyBitmap"/>）であり、<br/>
/// ホスト側は標準の XAML 要素だけで表示できる（画像は <see cref="WriteableBitmap"/>、枠は <see cref="Rectangle"/>）。<br/>
/// 描画ライブラリを必要としない。<br/>
/// <br/>
/// <b>座標</b>: 本面は画像のピクセル サイズと同じ DIP サイズで置くため、<br/>
/// ホスト内の DIP 座標は元画像ピクセル座標と一致する。<br/>
/// <br/>
/// <b>入力</b>: 本面自身は入力を受けない（ホストの親が受ける。 枠は重ねるだけの表示である）。<br/>
/// <b>公開の理由</b>: XAML から配置するため、XAML が参照できる可視性（<c>public</c>）にする。
/// </remarks>
public sealed partial class SourceCanvas : Canvas
{
    /// <summary>画像を表示する要素。</summary>
    private readonly Image _image = new() { IsHitTestVisible = false, Stretch = Stretch.Fill };

    /// <summary>選択矩形を示す枠。</summary>
    private readonly Rectangle _selection = new()
    {
        IsHitTestVisible = false,
        Stroke = new SolidColorBrush(Microsoft.UI.Colors.DeepSkyBlue),
        StrokeThickness = 1,
        Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0x30, 0x00, 0xB0, 0xFF)),
        Visibility = Visibility.Collapsed,
    };

    /// <summary>画像の変換結果のキャッシュ（同じ画像を渡し直したときに作り直さない）。</summary>
    private readonly ConcurrentDictionary<EsReadOnlyBitmap, WriteableBitmap> _cache = new();

    /// <summary>テスト画像と選択矩形の枠を表示する面を初期化する。</summary>
    public SourceCanvas()
    {
        Children.Add(_image);
        Children.Add(_selection);
    }

    /// <summary>表示するテスト画像を設定する。</summary>
    /// <param name="source">テスト画像。 null の場合は画像を表示しない。 借用であり破棄しない。</param>
    public void SetSource(EsReadOnlyBitmap? source)
    {
        if (source is null)
        {
            _image.Source = null;
            return;
        }

        _image.Source = _cache.GetOrAdd(source, static image => ToBitmap(image));
    }

    /// <summary>選択矩形の枠を設定する。</summary>
    /// <param name="rect">選択矩形（元画像ピクセル座標）。</param>
    /// <param name="isVisible">表示する場合は true。</param>
    public void SetSelection(EsRect rect, bool isVisible)
    {
        _selection.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        if (!isVisible)
        {
            return;
        }

        SetLeft(_selection, rect.X);
        SetTop(_selection, rect.Y);
        _selection.Width = rect.Width;
        _selection.Height = rect.Height;
    }

    /// <summary>画像を <see cref="WriteableBitmap"/> へ変換する。</summary>
    /// <param name="image">変換する画像。</param>
    /// <returns>変換した画像。</returns>
    /// <remarks>画素形式は <see cref="WriteableBitmap"/> が要求する BGRA8888 である（コアの内部表現と同じ）。</remarks>
    private static WriteableBitmap ToBitmap(EsReadOnlyBitmap image)
    {
        var bitmap = new WriteableBitmap(image.Width, image.Height);
        using (var target = bitmap.PixelBuffer.AsStream())
        {
            target.Write(image.ReadOnlyPixels);
        }

        bitmap.Invalidate();
        return bitmap;
    }
}
