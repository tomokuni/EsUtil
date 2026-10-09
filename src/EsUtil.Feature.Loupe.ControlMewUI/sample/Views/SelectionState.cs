using System.Globalization;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Loupe.MewUI.Sample.Views;

/// <summary>選択矩形の状態（表示可否と対象座標 = 元画像ピクセル座標の矩形）。</summary>
/// <remarks>
/// <b>示すこと</b>: ルーペのソース（拡大元画像）のどこを対象にしているかを、画像側の破線枠とルーペ内の破線で示す。<br/>
/// 本クラスはサンプルが持つ表示用の状態であり、コントロールの設定ではない（ルーペへは「拡大中心」だけを渡す）。<br/>
/// <b>変更通知</b>: 値が変わると <see cref="Changed"/> を発火し、ホストが破線枠・ラベル・ルーペの再描画を更新する。
/// </remarks>
internal sealed class SelectionState
{
    /// <summary>選択矩形の既定の左上 X（対象座標）。</summary>
    private const double DefaultX = 250;

    /// <summary>選択矩形の既定の左上 Y（対象座標）。</summary>
    private const double DefaultY = 180;

    /// <summary>選択矩形の既定の幅（対象座標）。</summary>
    private const double DefaultWidth = 300;

    /// <summary>選択矩形の既定の高さ（対象座標）。</summary>
    private const double DefaultHeight = 200;

    /// <summary>値の変更を通知するイベント。</summary>
    internal event EventHandler? Changed;

    /// <summary>値の変更を通知する。</summary>
    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>選択矩形を表示するかどうかを取得または設定する。</summary>
    internal bool IsVisible
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            RaiseChanged();
        }
    } = true;

    /// <summary>選択矩形の左上 X（対象座標）を取得または設定する。</summary>
    internal double X { get; set => Set(ref field, value); } = DefaultX;

    /// <summary>選択矩形の左上 Y（対象座標）を取得または設定する。</summary>
    internal double Y { get; set => Set(ref field, value); } = DefaultY;

    /// <summary>選択矩形の幅（対象座標）を取得または設定する。</summary>
    internal double Width { get; set => Set(ref field, value); } = DefaultWidth;

    /// <summary>選択矩形の高さ（対象座標）を取得または設定する。</summary>
    internal double Height { get; set => Set(ref field, value); } = DefaultHeight;

    /// <summary>現在の選択矩形（対象座標）を取得する。</summary>
    internal EsRect Rect => new(X, Y, Width, Height);

    /// <summary>座標ラベルの文言（左上と寸法）を取得する。</summary>
    internal string Label => string.Create(
        CultureInfo.InvariantCulture,
        $"({X:0}, {Y:0}) {Width:0}×{Height:0}");

    /// <summary>値を設定し、変化した場合に変更を通知する。</summary>
    /// <param name="field">設定対象のフィールド。</param>
    /// <param name="value">設定する値。</param>
    private void Set(ref double field, double value)
    {
        if (field.Equals(value))
        {
            return;
        }

        field = value;
        RaiseChanged();
    }
}
