using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace EsUtil.Ui.WinUI3.Marquee.Internal;

/// <summary>WinUI 3 のテキストエンジンで文字列の実寸を測る。</summary>
/// <remarks>
/// <b>位置付け</b>: 実寸は描画に使うテキスト エンジンで測らなければ一致しない。<br/>
/// 本コントロールはラベルを <see cref="TextBlock"/> として置くため、WinUI 3 のエンジン（DirectWrite）が<br/>
/// 報告する送り幅と行高で測る（背景の大きさと文字の大きさがずれない）。<br/>
/// <br/>
/// <b>測定用の要素</b>: 表示する要素と同じフォント（テーマのフォント）で測るため、ツリー外の <see cref="TextBlock"/> を使う。<br/>
/// ツリー外でも DPI は XAML のルートから解決されるため、要素と同じ丸めで測れる。<br/>
/// <br/>
/// <b>テキストエンジンが無い場合</b>: XAML のランタイムが初期化されていない環境（ウィンドウを生成しない単体テスト）では<br/>
/// 要素を生成できず測定できない。 その場合は文字数とフォントサイズからの近似を返す<br/>
/// （実アプリは常にエンジンで測る）。
/// <para/>
/// <b>複製元: EsUtil.SaveEvidence.Platform.WinUI3.Imaging.WinUI3TextMeasure</b>（単独パッケージとして配布するための意図的な複製。 依存関係 §4.1）。
/// </remarks>
internal static class MarqueeTextMeasure
{
    /// <summary>測定に使うツリー外の要素（フォントと文字列を写して測る）。</summary>
    private static TextBlock? s_probe;

    /// <summary>テキストエンジンで測定できるかどうか（要素を生成できない環境を検出したら false へ落とす）。</summary>
    private static bool s_engineAvailable = true;

    /// <summary>テキストの実寸を測る。</summary>
    /// <param name="text">測定するテキスト。 null 不可。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>テキストの実寸（DIP）。</returns>
    public static EsSize Measure(string text, double fontSize)
    {
        if (s_engineAvailable)
        {
            try
            {
                return MeasureWithEngine(text, fontSize);
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
            {
                // XAML のランタイムが無い環境（ウィンドウを生成しない単体テスト）では測定できない。
                // 以後は同じ失敗を繰り返さないよう近似へ切り替える
                s_engineAvailable = false;
            }
        }

        return Approximate(text, fontSize);
    }

    /// <summary>テキストエンジンを使えない環境での近似値を求める。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>近似の実寸（DIP）。</returns>
    /// <remarks>等幅フォントを仮定した概算である（1 文字 = フォントサイズの 0.6 倍、行高 = フォントサイズの 1.4 倍）。</remarks>
    private static EsSize Approximate(string text, double fontSize)
        => new(text.Length * fontSize * 0.6, fontSize * 1.4);

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
