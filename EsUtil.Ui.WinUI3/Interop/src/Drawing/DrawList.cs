using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Interop.Drawing;

/// <summary>描画する矩形の列（描画順を持つ）。</summary>
/// <remarks>
/// <b>UI 型を持たない</b>: 本型は <see cref="DrawRect"/> と <see cref="DrawRoundRect"/> の列だけを持つ。<br/>
/// 描画計画（コア）から本型を組み立てる変換は純粋であり、ウィンドウ無しで検証できる<br/>
/// （XAML 要素の生成を伴う層は本型を受け取って反映するだけにする）。<br/>
/// <b>順序が意味を持つ</b>: 先に追加した矩形を先に描く（重なりは追加順で決まる）。<br/>
/// <b>使い回す</b>: 描画のたびに作り直すと割り当てが増えるため、<see cref="Clear"/> して詰め直す使い方を認める。
/// </remarks>
public sealed class DrawList
{
    /// <summary>矩形の列（追加順）。</summary>
    private readonly List<DrawRect> _rects = [];

    /// <summary>角丸矩形の列（追加順）。</summary>
    private readonly List<DrawRoundRect> _roundRects = [];

    /// <summary>矩形の件数を取得する。</summary>
    /// <value>追加済みの矩形の件数。</value>
    public int RectCount => _rects.Count;

    /// <summary>角丸矩形の件数を取得する。</summary>
    /// <value>追加済みの角丸矩形の件数。</value>
    public int RoundRectCount => _roundRects.Count;

    /// <summary>矩形の列を取得する（読み取り専用のビュー）。</summary>
    /// <value>追加順の矩形。</value>
    public IReadOnlyList<DrawRect> Rects => _rects;

    /// <summary>角丸矩形の列を取得する（読み取り専用のビュー）。</summary>
    /// <value>追加順の角丸矩形。</value>
    public IReadOnlyList<DrawRoundRect> RoundRects => _roundRects;

    /// <summary>すべての矩形を消す。</summary>
    public void Clear()
    {
        _rects.Clear();
        _roundRects.Clear();
    }

    /// <summary>矩形を追加する。</summary>
    /// <param name="bounds">矩形。</param>
    /// <param name="color">塗り色。</param>
    /// <remarks>大きさが 0 以下の矩形は追加しない（描いても何も現れず、要素の無駄になる）。</remarks>
    public void AddRect(EsRect bounds, DrawingColor color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _rects.Add(new DrawRect(bounds, color));
    }

    /// <summary>角丸矩形を追加する。</summary>
    /// <param name="bounds">矩形。</param>
    /// <param name="cornerRadius">角の半径。 0 以下の場合は四角形として追加する。</param>
    /// <param name="color">塗り色。</param>
    /// <remarks>大きさが 0 以下の矩形は追加しない（描いても何も現れない）。</remarks>
    public void AddRoundRect(EsRect bounds, double cornerRadius, DrawingColor color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _roundRects.Add(new DrawRoundRect(bounds, cornerRadius, color));
    }
}
