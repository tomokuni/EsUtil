using Aprillz.MewUI.Text;
using EsUtil.Feature.StyledText;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>1 フレームの中で作った文字のレイアウトを使い回す（同じ内容なら同じ実体を返す）。</summary>
/// <remarks>
/// <b>文字の描画とキャレット・選択範囲で同じ実体を使う</b>: 同じ内容のレイアウトを作り直しても、
/// 描画側が内部で丸め直すと<b>描かれた文字の位置</b>と<b>測った位置</b>が食い違います。
/// 1 フレームの中で
/// 同じ書式・同じ文字列のレイアウトは 1 度だけ作り、描画と位置計算の両方で共有します。
/// </remarks>
internal sealed class AnnotationTextLayoutCache
{
    /// <summary>作ったレイアウト（書式と文字列が同じなら同じ実体）。</summary>
    private readonly Dictionary<(string Text, TextStyle Style), ITextLayout> _layouts = [];

    /// <summary>書式と文字列からレイアウトを取得する（無ければ作る）。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>レイアウト。</returns>
    internal ITextLayout Create(string text, TextStyle style)
    {
        var key = (text, style);
        if (_layouts.TryGetValue(key, out var layout))
        {
            return layout;
        }

        layout = AnnotationTextMetrics.CreateLayout(text, style, ignoreItalic: false);
        _layouts[key] = layout;
        return layout;
    }
}
