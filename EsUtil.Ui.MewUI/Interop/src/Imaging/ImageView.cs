using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Ui.MewUI.Interop.Imaging;

/// <summary><see cref="EsReadOnlyBitmap"/> を親が割り当てた領域へ描画する表示ビュー（MewUI 標準描画版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Adapter（<see cref="EsReadOnlyBitmap"/> を MewUI の要素として適合させる）。<br/>
/// <b>用途</b>: 「割り当てられた領域いっぱいへ 1 枚の画像を敷く」表示に使う。 表示サイズは親のレイアウトに従う
/// （本クラス自身は要求サイズを持たない）。 縦横比から大きさを決める表示は、本クラスを継承して
/// <c>MeasureContent</c> を上書きする。<br/>
/// <b>配置方法</b>: <see cref="UniformDraw"/> が false（既定）の場合は領域全体へ引き伸ばし、
/// true の場合は縦横比を保って中央へ収める（余白は描画しない）。<br/>
/// <b>描画</b>: フレームワーク標準の <see cref="Control.OnRender(IGraphicsContext)"/> へ描く（Skia のキャンバスを使わない）。
/// 配置の計算は <see cref="ImageRenderPlan"/>、描画手順は <see cref="ImageRenderer"/> が単一所有する
/// （本クラスはプロパティの配線だけを担う）。
/// </remarks>
public class ImageView : Control
{
    /// <summary><see cref="Source"/> プロパティ（表示する画像）。</summary>
    /// <remarks>
    /// 変更は描画とレイアウトの両方に影響しうる（縦横比から大きさを決める派生クラスがあるため）。
    /// 派生クラスが <c>MeasureContent</c> で <see cref="Source"/> を参照するので、両方を無効化対象にする。
    /// </remarks>
    public static readonly MewProperty<EsReadOnlyBitmap?> SourceProperty =
        MewProperty<EsReadOnlyBitmap?>.Register<ImageView>(
            nameof(Source), null, MewPropertyOptions.AffectsRender | MewPropertyOptions.AffectsLayout);

    /// <summary>描画を担う層（変換した画像を保持する）。</summary>
    private readonly ImageRenderer _renderer = new();

    /// <summary>表示する画像を取得または設定する。</summary>
    /// <value>表示する画像。 null の場合は何も描画しない（余白のみ）。</value>
    public EsReadOnlyBitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>縦横比を保って領域へ収めるかどうかを取得する。</summary>
    /// <value>true の場合は縦横比を保って中央へ収める。 false の場合は領域全体へ引き伸ばす。 既定値は false。</value>
    /// <remarks>派生クラスが固定値を与える（プロパティにすると、使い分けのない側へ不要な設定面が増える）。</remarks>
    protected virtual bool UniformDraw => false;

    /// <inheritdoc/>
    protected override void OnRender(IGraphicsContext context)
        => _renderer.Render(
            context, Source, RenderSize.Width, RenderSize.Height, UniformDraw ? ImageDrawMode.Uniform : ImageDrawMode.Fill);
}
