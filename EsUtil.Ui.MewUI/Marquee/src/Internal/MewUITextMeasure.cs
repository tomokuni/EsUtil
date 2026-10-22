using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

// 複製元: EsUtil.SaveEvidence.Platform.MewUI.Imaging（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Ui.MewUI.Marquee.Internal;

/// <summary>MewUI のテキストエンジンで文字列の実寸を測る（ラベルの配置の唯一の入口）。</summary>
/// <remarks>
/// <b>位置付け</b>: 実寸は描画に使うテキスト エンジンで測らなければ一致しない。<br/>
/// MewUI のエンジン（DirectWrite の GDI 互換レイアウト）は文字の送りを物理ピクセルの格子へ丸めるため、<br/>
/// 別のエンジンで測った値より広い値を返す。<br/>
/// 小さい値で背景を作ると<b>文字が要素からはみ出して切れる</b>ため、本クラスが実寸の測定を単一所有する。<br/>
/// <br/>
/// <b>測定用の要素</b>: ラベルを表示する要素と同じフォント（テーマのフォント）で測るため、
/// ツリー外の <see cref="TextBlock"/> を使う。<br/>
/// ツリー外でも DPI はアプリケーションから解決されるため、要素と同じ丸めで測れる（実測で確認済み）。<br/>
/// <br/>
/// <b>グラフィックス バックエンドが無い場合</b>: 描画エンジンを持たない環境では測定できません。
/// その場合は呼び出し側が測定関数を差し替えてください（実アプリでは常にエンジンで測ります）。
/// </remarks>
internal static class MewUITextMeasure
{
    /// <summary>測定に使うツリー外の要素（フォントと文字列を写して測る）。</summary>
    private static TextBlock? s_probe;


    /// <summary>テキストの実寸を測る。</summary>
    /// <param name="text">測定するテキスト。 null 不可。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>テキストの実寸（DIP。 描画エンジンが報告する送り幅と行高）。</returns>
    /// <remarks>
    /// ラベルの背景は「文字の実寸 + 内側余白」で作るため、実寸が小さいと背景から文字がはみ出して切れる<br/>
    /// （描画に使うエンジンで測ることで、実際に描かれる幅と一致させる）。
    /// </remarks>
    /// <exception cref="InvalidOperationException">グラフィックス バックエンドが登録されていない場合。</exception>
    public static EsSize Measure(string text, double fontSize)
    {
        return MeasureWithEngine(text, fontSize);
    }

    /// <summary>描画エンジン（MewUI のテキスト レイアウト）で実寸を測る。</summary>
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
