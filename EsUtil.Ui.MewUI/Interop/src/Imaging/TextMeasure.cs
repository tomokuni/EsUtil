using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.MewUI.Interop.Imaging;

/// <summary>MewUI のテキストエンジンで文字列の実寸を測る（ラベルの配置の唯一の入口）。</summary>
/// <remarks>
/// <b>位置付け</b>: 実寸は描画に使うテキスト エンジンで測らなければ一致しない。<br/>
/// MewUI のエンジンは文字の送りを物理ピクセルの格子へ丸めるため、別のエンジンで測った値より広い値を返す。<br/>
/// その値で背景を作ると<b>文字が要素からはみ出して切れる</b>ため、本クラスが実寸の測定を単一所有する。<br/>
/// <br/>
/// <b>測定用の要素</b>: ラベルを表示する要素と同じフォント（テーマのフォント）で測るため、
/// ツリー外の <see cref="TextBlock"/> を使う。<br/>
/// ツリー外でも DPI はアプリケーションから解決されるため、要素と同じ丸めで測れる（実測で確認済み）。<br/>
/// <br/>
/// <b>前提条件</b>: グラフィックス バックエンドが登録されていること（描画中は常に満たされる）。
/// バックエンドを持たない環境（ウィンドウを生成しない単体テスト）では測定できないため、
/// 呼び出し側は測定関数を差し替えられる入口（配置計算の <c>measure</c> 引数）を使う。
/// </remarks>
public static class TextMeasure
{
    /// <summary>測定に使うツリー外の要素（フォントと文字列を写して測る）。</summary>
    private static TextBlock? s_probe;

    /// <summary>折り返しの測定に使うツリー外の要素（単一行の測定と状態を共有しない）。</summary>
    private static TextBlock? s_wrapProbe;

    /// <summary>テキストの実寸を測る。</summary>
    /// <param name="text">測定するテキスト。 null 不可。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>テキストの実寸（DIP。 描画エンジンが報告する送り幅と行高）。</returns>
    /// <exception cref="InvalidOperationException">グラフィックス バックエンドが登録されていない場合。</exception>
    /// <remarks>
    /// ラベルの背景は「文字の実寸 + 内側余白」で作るため、実寸が小さいと背景から文字がはみ出して切れる
    /// （描画に使うエンジンで測ることで、実際に描かれる幅と一致させる）。
    /// </remarks>
    public static EsSize Measure(string text, double fontSize)
    {
        var probe = s_probe ??= new TextBlock();
        probe.Text = text;
        probe.FontSize = fontSize;
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var size = probe.DesiredSize;
        return new EsSize(size.Width, size.Height);
    }

    /// <summary>折り返したテキストの実寸を測る。</summary>
    /// <param name="text">測定するテキスト。 null 不可。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="isBold">太字で測るかどうか。</param>
    /// <param name="maxWidth">折り返す幅（DIP）。</param>
    /// <returns>折り返した後の実寸（DIP）。 高さは全行の合計。</returns>
    /// <exception cref="InvalidOperationException">グラフィックス バックエンドが登録されていない場合。</exception>
    /// <remarks>
    /// <b>表示する要素と同じ規則で測る</b>: ラベルは折り返す <see cref="TextBlock"/> として置くため、<br/>
    /// 同じ折り返し設定の要素を幅で計測する（文字数からの見積もりでは行数を誤り、省略されないまま切れる）。
    /// </remarks>
    public static EsSize MeasureWrapped(string text, double fontSize, bool isBold, double maxWidth)
    {
        var probe = s_wrapProbe ??= new TextBlock();
        probe.Text = text;
        probe.FontSize = fontSize;
        probe.FontWeight = isBold ? FontWeight.Bold : FontWeight.Normal;
        probe.TextWrapping = TextWrapping.Wrap;
        probe.Measure(new Size(maxWidth, double.PositiveInfinity));

        var size = probe.DesiredSize;
        return new EsSize(size.Width, size.Height);
    }
}
