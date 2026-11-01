using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace EsUtil.Ui.WinUI3.Interop.Imaging;

/// <summary>WinUI 3 のテキストエンジンで文字列の実寸を測る（ラベルを要素として置く場合の唯一の入口）。</summary>
/// <remarks>
/// <b>位置付け</b>: 実寸は描画に使うテキスト エンジンで測らなければ一致しない。<br/>
/// <see cref="TextBlock"/> を要素として置く表示（XAML のレイアウトに寸法を渡す表示）は、<br/>
/// WinUI 3 のエンジン（DirectWrite）が報告する送り幅と行高で測る必要がある。<br/>
/// <br/>
/// <b>測定用の要素</b>: 表示する要素と同じフォント（テーマのフォント）で測るため、ツリー外の <see cref="TextBlock"/> を使う。<br/>
/// ツリー外でも DPI は XAML のルートから解決されるため、要素と同じ丸めで測れる。<br/>
/// <br/>
/// <b>キャンバスへ文字を描かない</b>: WinUI 3 版は文字を <see cref="TextBlock"/> として置くため、測るエンジンと<br/>
/// 描くエンジンが一致する（ラベルの背景と文字の大きさがずれない）。<br/>
/// <br/>
/// <b>テキストエンジンが無い場合</b>: XAML のランタイムが初期化されていない環境（ウィンドウを生成しない単体テスト）では<br/>
/// 要素を生成できず測定できない。 その場合は文字数とフォントサイズからの近似を返す<br/>
/// （実アプリは常にエンジンで測る）。
/// </remarks>
public static class TextMeasure
{
    /// <summary>測定に使うツリー外の要素（フォントと文字列を写して測る）。</summary>
    private static TextBlock? s_probe;

    /// <summary>折り返しの測定に使うツリー外の要素（単一行の測定と状態を共有しない）。</summary>
    private static TextBlock? s_wrapProbe;

    /// <summary>テキストエンジンで測定できるかどうか（要素を生成できない環境を検出したら false へ落とす）。</summary>
    private static bool s_engineAvailable = true;

    /// <summary>テキストの実寸を測る。</summary>
    /// <param name="text">測定するテキスト。 null 不可。</param>
    /// <param name="fontSize">フォントサイズ（DIP = 論理ピクセル）。</param>
    /// <returns>テキストの実寸（DIP。 テキストエンジンが報告する送り幅と行高）。</returns>
    /// <remarks>
    /// ラベルの背景は「文字の実寸 + 内側余白」で作るため、実寸が小さいと背景から文字がはみ出して切れる<br/>
    /// （描画に使うエンジンで測ることで、実際に描かれる幅と一致させる）。
    /// </remarks>
    public static EsSize Measure(string text, double fontSize)
    {
        if (s_engineAvailable)
        {
            try
            {
                return MeasureWithEngine(text, fontSize);
            }
            catch (COMException)
            {
                // XAML のランタイムが無い環境（ウィンドウを生成しない単体テスト）では要素を生成できない。
                // 以後は同じ失敗を繰り返さないよう近似へ切り替える
                s_engineAvailable = false;
            }
            catch (InvalidOperationException)
            {
                // 要素は生成できたがレイアウトを実行できない場合（同上）。 以後は近似へ切り替える
                s_engineAvailable = false;
            }
        }

        return Approximate(text, fontSize);
    }

    /// <summary>テキストエンジンを使えない環境での近似値を求める。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>近似の実寸（DIP）。</returns>
    /// <remarks>
    /// 等幅フォントを仮定した概算である（1 文字 = フォントサイズの 0.6 倍、行高 = フォントサイズの 1.4 倍）。<br/>
    /// <b>実アプリでは使われない</b>: テキストエンジンが使える環境では常に <see cref="MeasureWithEngine"/> が測る。
    /// </remarks>
    private static EsSize Approximate(string text, double fontSize)
        => new(text.Length * fontSize * 0.6, fontSize * 1.4);

    /// <summary>折り返したテキストの実寸を測る。</summary>
    /// <param name="text">測定するテキスト。 null 不可。</param>
    /// <param name="fontSize">フォントサイズ（DIP = 論理ピクセル）。</param>
    /// <param name="isBold">太字で測るかどうか。</param>
    /// <param name="maxWidth">折り返す幅（DIP）。</param>
    /// <returns>折り返した後の実寸（DIP）。 高さは全行の合計。</returns>
    /// <remarks>
    /// <b>表示する要素と同じ規則で測る</b>: ラベルは折り返す <see cref="TextBlock"/> として置くため、<br/>
    /// 同じ折り返し設定の要素を幅で計測する（文字数からの見積もりでは行数を誤り、省略されないまま切れる）。
    /// </remarks>
    public static EsSize MeasureWrapped(string text, double fontSize, bool isBold, double maxWidth)
    {
        if (s_engineAvailable)
        {
            try
            {
                return MeasureWrappedWithEngine(text, fontSize, isBold, maxWidth);
            }
            catch (COMException)
            {
                // XAML のランタイムが無い環境（ウィンドウを生成しない単体テスト）では要素を生成できない。
                // 以後は同じ失敗を繰り返さないよう近似へ切り替える
                s_engineAvailable = false;
            }
            catch (InvalidOperationException)
            {
                // 要素は生成できたがレイアウトを実行できない場合（同上）。 以後は近似へ切り替える
                s_engineAvailable = false;
            }
        }

        return ApproximateWrapped(text, fontSize, maxWidth);
    }

    /// <summary>テキストエンジンを使えない環境での折り返しの近似値を求める。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="maxWidth">折り返す幅（DIP）。</param>
    /// <returns>近似の実寸（DIP）。</returns>
    /// <remarks>等幅フォントを仮定し、1 行に収まる文字数から行数を見積もる（実アプリでは使われない）。</remarks>
    private static EsSize ApproximateWrapped(string text, double fontSize, double maxWidth)
    {
        var lineHeight = fontSize * 1.4;
        var charWidth = fontSize * 0.6;
        var charsPerLine = maxWidth > 0 ? Math.Max(1, maxWidth / charWidth) : Math.Max(1, text.Length);
        var lines = Math.Max(1, Math.Ceiling(text.Length / charsPerLine));

        return new EsSize(Math.Min(maxWidth, text.Length * charWidth), lines * lineHeight);
    }

    /// <summary>テキストエンジン（WinUI 3 のテキスト レイアウト）で折り返した実寸を測る。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <param name="isBold">太字で測るかどうか。</param>
    /// <param name="maxWidth">折り返す幅（DIP）。</param>
    /// <returns>折り返した後の実寸（DIP）。</returns>
    private static EsSize MeasureWrappedWithEngine(string text, double fontSize, bool isBold, double maxWidth)
    {
        var probe = s_wrapProbe ??= new TextBlock();
        probe.Text = text;
        probe.FontSize = fontSize;
        probe.FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal;
        probe.TextWrapping = TextWrapping.Wrap;
        probe.Measure(new Size(maxWidth, double.PositiveInfinity));

        var size = probe.DesiredSize;
        return new EsSize(size.Width, size.Height);
    }

    /// <summary>テキストエンジン（WinUI 3 のテキスト レイアウト）で実寸を測る。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>テキストの実寸（DIP）。</returns>
    private static EsSize MeasureWithEngine(string text, double fontSize)
    {
        var probe = s_probe ??= new TextBlock();
        probe.Text = text;
        probe.FontSize = fontSize;
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var size = probe.DesiredSize;
        return new EsSize(size.Width, size.Height);
    }
}
