namespace EsUtil.Ui.WinUI3.Loupe.Internal;

/// <summary>描画する矩形の列（描画順を持つ。 単位は <see cref="LoupeRect"/> と同じ）。</summary>
/// <remarks>
/// <b>順序が意味を持つ</b>: 先に追加した矩形を先に描く（重なりは追加順で決まる）。<br/>
/// <b>使い回す</b>: 描画のたびに作り直すと割り当てが増えるため、<see cref="Clear"/> して詰め直す使い方を認める。<br/>
/// 大きさが 0 以下の矩形は追加しない（描いても何も現れず、要素の無駄になる）。
/// <para/>
/// <b>複製元: EsUtil.SaveEvidence.Platform.WinUI3.Drawing.DrawList</b>（単独パッケージとして配布するための意図的な複製。 依存関係 §4.1）。
/// </remarks>
public sealed class LoupeRectList
{
    /// <summary>矩形の列（追加順）。</summary>
    private readonly List<LoupeRect> _rects = [];

    /// <summary>矩形の件数を取得する。</summary>
    /// <value>追加済みの矩形の件数。</value>
    public int Count => _rects.Count;

    /// <summary>矩形の列を取得する（読み取り専用のビュー）。</summary>
    /// <value>追加順の矩形。</value>
    public IReadOnlyList<LoupeRect> Rects => _rects;

    /// <summary>すべての矩形を消す。</summary>
    public void Clear() => _rects.Clear();

    /// <summary>矩形を追加する。</summary>
    /// <param name="x">左端。</param>
    /// <param name="y">上端。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="colorArgb">塗り色（<c>0xAARRGGBB</c>）。</param>
    public void Add(double x, double y, double width, double height, uint colorArgb)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _rects.Add(new LoupeRect(x, y, width, height, colorArgb));
    }

    /// <summary>矩形を追加する。</summary>
    /// <param name="rect">追加する矩形。</param>
    public void Add(LoupeRect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        _rects.Add(rect);
    }
}
