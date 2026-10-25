using System.Runtime.InteropServices.WindowsRuntime;
using EsUtil.Platform.Core.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace EsUtil.Ui.WinUI3.Loupe.Internal;

/// <summary><see cref="EsReadOnlyBitmap"/> の一部を拡大して表示する層（XAML の画像要素）。</summary>
/// <remarks>
/// <b>拡大は CPU で行う</b>: 表示のたびに、ルーペの表示領域（1 辺 160 DIP 程度）の画素を
/// <see cref="LoupeImageSampler"/> で切り出して <see cref="WriteableBitmap"/> へ書き、画像要素へ貼る。<br/>
/// 走査する画素は 4 万件ほどであり、GPU（Composition のスプライト）へ任せるより<b>確実である</b>
/// （スプライトは <c>LoadedImageSurface</c> の非同期読み込みに依存し、読み込みが終わらない間は
/// 何も表示されない。 実測 2026-10-25: 撮影画像の 51 MB の BMP を作っても表示されなかった）。<br/>
/// <br/>
/// <b>最近傍で拡大する</b>: ルーペは撮影画像の画素を確認する用途であるため、補間で色を混ぜない。<br/>
/// <b>画像の大きさは表示の実寸と一致させる</b>: 書き込む画像の寸法 = 画像の画素サイズであるため、
/// 表示（<see cref="Image.Stretch"/> = <see cref="Stretch.Fill"/>）で拡大・縮小は起きない。
/// </remarks>
internal sealed partial class LoupeImageView : Grid
{
    /// <summary>画像を表示する要素。</summary>
    private readonly Image _image = new()
    {
        Stretch = Stretch.Fill,
        IsHitTestVisible = false,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };

    /// <summary>拡大元の画像（同じ画像を渡し直したときの判定に使う）。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>表示している画像の画素（画像の寸法が変わったときだけ作り直す）。</summary>
    private WriteableBitmap? _bitmap;

    /// <summary>画素の作業領域（画像の寸法が変わったときだけ作り直す）。</summary>
    private byte[] _buffer = [];

    /// <summary>拡大画像の表示層を初期化する。</summary>
    /// <remarks>領域の外へはみ出さないよう、自分の大きさで切り取る。</remarks>
    public LoupeImageView()
    {
        Children.Add(_image);

        Clip = new RectangleGeometry();
        SizeChanged += (_, e) =>
        {
            if (Clip is RectangleGeometry clip)
            {
                clip.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            }
        };
    }

    /// <summary>拡大元の画像を設定する。</summary>
    /// <param name="image">拡大元の 100% 画像。 null の場合は何も表示しない。</param>
    /// <remarks>画素の切り出しは <see cref="Apply"/> が行う（本メソッドは参照を保持するだけ）。</remarks>
    public void SetSource(EsReadOnlyBitmap? image)
    {
        if (ReferenceEquals(_source, image))
        {
            return;
        }

        _source = image;
        if (image is not { IsDisposed: false })
        {
            _image.Source = null;
        }
    }

    /// <summary>描画計画の拡大画像を画像へ反映する。</summary>
    /// <param name="plan">描画計画（切り出し領域・描画先・画像の寸法を持つ）。</param>
    /// <remarks>
    /// 画像の寸法が変わったときだけ <see cref="WriteableBitmap"/> と作業領域を作り直す。<br/>
    /// 拡大画像が無い（拡大中心が未設定）場合は何も表示しない。
    /// </remarks>
    public void Apply(LoupeDrawPlan plan)
    {
        if (_source is not { IsDisposed: false } source || plan.MagnifiedImage is not { } magnified)
        {
            _image.Source = null;
            return;
        }

        var pixelSize = plan.ClientPixelSize;
        if (pixelSize <= 0)
        {
            _image.Source = null;
            return;
        }

        EnsureBitmap(pixelSize);
        LoupeImageSampler.Sample(
            source,
            magnified.CropSourceRect,
            magnified.DrawClientPixelRect,
            pixelSize,
            _buffer);

        using (var target = _bitmap!.PixelBuffer.AsStream())
        {
            target.Write(_buffer, 0, _buffer.Length);
        }

        _bitmap.Invalidate();
        _image.Source = _bitmap;
    }

    /// <summary>画像（<see cref="WriteableBitmap"/>）と作業領域を必要な寸法で用意する。</summary>
    /// <param name="pixelSize">画像の一辺（画素）。</param>
    /// <remarks>
    /// 画像の寸法は表示サイズ（DIP）ではなく<b>物理ピクセル</b>である（描画計画と同じ単位）。<br/>
    /// 要素の大きさ（DIP）は親（<see cref="LoupeVisualLayer"/>）が決めるため、ここでは設定しない
    /// （画像は画像の画素サイズで作り、要素の DIP サイズへ引き伸ばされる ＝ 物理ピクセルで 1:1 になる）。
    /// </remarks>
    private void EnsureBitmap(int pixelSize)
    {
        if (_bitmap is { } existing && existing.PixelWidth == pixelSize && existing.PixelHeight == pixelSize)
        {
            return;
        }

        _bitmap = new WriteableBitmap(pixelSize, pixelSize);
        _buffer = new byte[pixelSize * pixelSize * LoupeImageSampler.BytesPerPixel];
    }
}
