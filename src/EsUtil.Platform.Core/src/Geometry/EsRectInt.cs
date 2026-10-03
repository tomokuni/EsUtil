using System.Runtime.CompilerServices;

namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の整数矩形を表す値オブジェクト。</summary>
/// <remarks>
/// <see cref="EsRect"/>（小数を扱う DIP 相当の矩形）の<b>整数版</b>である。<br/>
/// 画面物理ピクセル矩形（DPI スケーリング適用後）・画像ローカルピクセルの矩形・仮想スクリーン境界など、<br/>
/// 整数でしか意味を持たない矩形に使用する。<br/>
/// <b>整数であることは「画面物理ピクセル」を意味しない</b>ため、単位が読む側に必要な箇所はドキュメントに明記する。<br/>
/// 各 UI 実装の整数矩形型との変換は Interop 層が提供する。<br/>
/// <br/>
/// <b>Contains の区間は半開区間</b>（左上を含み、右下を含まない）であり、MewUI・WinUI3 と同じである。<br/>
/// <b>Union の規則</b>は <see cref="EsRect.Union(EsRect)"/> と対称である（空の矩形は相手を返す）。
/// </remarks>
/// <param name="X">左上 X 座標（整数）。</param>
/// <param name="Y">左上 Y 座標（整数）。</param>
/// <param name="Width">幅（整数）。</param>
/// <param name="Height">高さ（整数）。</param>
public readonly record struct EsRectInt(int X, int Y, int Width, int Height)
{
    /// <summary>空の矩形（全て 0）を取得する。</summary>
    /// <value>左上が原点で幅・高さとも 0 の矩形。</value>
    public static readonly EsRectInt Empty = default;

    /// <summary>右端の X 座標（左端 + 幅）を取得する（排他的境界）。</summary>
    /// <value>右端の X 座標（整数）。</value>
    public int Right => X + Width;

    /// <summary>幅または高さが 0 以下かどうか（切り出せる範囲が無い）を取得する。</summary>
    /// <value>幅または高さが 0 以下の場合は true。</value>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>下端の Y 座標（上端 + 高さ）を取得する（排他的境界）。</summary>
    /// <value>下端の Y 座標（画面物理ピクセル）。</value>
    public int Bottom => Y + Height;

    /// <summary>右下端の座標（右端・下端）を取得する。</summary>
    /// <value>右下端の座標（画面物理ピクセル）。</value>
    public EsPointInt BottomRight => new(Right, Bottom);

    /// <summary>左上端の座標（左端・上端）を取得する。</summary>
    /// <value>左上端の座標（画面物理ピクセル）。</value>
    /// <remarks>矩形の原点（画像の原点等）を取り出すときに、UI が <c>new EsPointInt(rect.X, rect.Y)</c> を書かないようにする。</remarks>
    public EsPointInt TopLeft => new(X, Y);

    /// <summary>右上端の座標（右端・上端）を取得する。</summary>
    /// <value>右上端の座標（画面物理ピクセル）。</value>
    public EsPointInt TopRight => new(Right, Y);

    /// <summary>左下端の座標（左端・下端）を取得する。</summary>
    /// <value>左下端の座標（画面物理ピクセル）。</value>
    public EsPointInt BottomLeft => new(X, Bottom);

    /// <summary>矩形の中心座標を取得する（整数除算）。</summary>
    /// <value>中心の座標（画面物理ピクセル）。</value>
    public EsPointInt Center => new(X + Width / 2, Y + Height / 2);

    /// <summary>幅・高さを表す整数サイズを取得する。</summary>
    /// <value>幅・高さをそのまま引き継いだサイズ。</value>
    public EsSizeInt Size => new(Width, Height);

    /// <summary>左・上・右・下の座標から整数矩形を生成する。</summary>
    /// <param name="left">左端の X 座標。</param>
    /// <param name="top">上端の Y 座標。</param>
    /// <param name="right">右端の X 座標。</param>
    /// <param name="bottom">下端の Y 座標。</param>
    /// <returns>指定の 4 辺を持つ矩形（幅・高さは右端 − 左端 / 下端 − 上端）。</returns>
    /// <remarks>.NET 標準の <c>Rectangle.FromLTRB</c> と同じ操作である。</remarks>
    public static EsRectInt FromLTRB(int left, int top, int right, int bottom)
        => new(left, top, right - left, bottom - top);

    /// <summary>2 つの対角点から整数矩形を生成する。</summary>
    /// <param name="topLeft">左上の座標。</param>
    /// <param name="bottomRight">右下の座標。</param>
    /// <returns>2 点を対角とする矩形。</returns>
    public static EsRectInt FromPoints(EsPointInt topLeft, EsPointInt bottomRight)
        => FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);

    /// <summary>小数矩形から整数矩形を作る。</summary>
    /// <param name="rect">変換元の小数矩形。</param>
    /// <param name="rounding">丸め方法（既定 <see cref="EsRounding.Round"/>）。</param>
    /// <returns>指定の方法で丸めた整数矩形（X・Y・幅・高さを個別に丸める）。</returns>
    /// <remarks>
    /// 丸め規則は <c>EsMath.RoundToInt</c> が単一所有する。<br/>
    /// .NET 標準の <c>Rectangle.Ceiling</c> / <c>Round</c> / <c>Truncate</c> は <paramref name="rounding"/> 指定に対応する。<br/>
    /// <b>四隅を丸めてから差を取る規則ではない</b>ため、右端・下端が 1 px 変わることがある（.NET 標準と同じ規則）。
    /// </remarks>
    public static EsRectInt FromEsRect(EsRect rect, EsRounding rounding = EsRounding.Round)
        => new(
            EsMath.RoundToInt(rect.X, rounding),
            EsMath.RoundToInt(rect.Y, rounding),
            EsMath.RoundToInt(rect.Width, rounding),
            EsMath.RoundToInt(rect.Height, rounding));

    /// <summary>X 座標だけを差し替えた矩形を返す。</summary>
    /// <param name="x">新しい左上 X 座標。</param>
    /// <returns>他の成分を引き継いだ矩形。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRectInt WithX(int x) => new(x, Y, Width, Height);

    /// <summary>Y 座標だけを差し替えた矩形を返す。</summary>
    /// <param name="y">新しい左上 Y 座標。</param>
    /// <returns>他の成分を引き継いだ矩形。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRectInt WithY(int y) => new(X, y, Width, Height);

    /// <summary>幅だけを差し替えた矩形を返す。</summary>
    /// <param name="width">新しい幅。</param>
    /// <returns>他の成分を引き継いだ矩形。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRectInt WithWidth(int width) => new(X, Y, width, Height);

    /// <summary>高さだけを差し替えた矩形を返す。</summary>
    /// <param name="height">新しい高さ。</param>
    /// <returns>他の成分を引き継いだ矩形。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRectInt WithHeight(int height) => new(X, Y, Width, height);

    /// <summary>指定量だけ平行移動した矩形を返す。</summary>
    /// <param name="dx">X 方向の移動量。</param>
    /// <param name="dy">Y 方向の移動量。</param>
    /// <returns>移動後の矩形（幅・高さは変わらない）。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRectInt Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);

    /// <summary>4 辺を指定量だけ広げた（狭めた）矩形を返す。</summary>
    /// <param name="dx">左右へ広げる量（負値は狭める）。</param>
    /// <param name="dy">上下へ広げる量（負値は狭める）。</param>
    /// <returns>左上を (−dx, −dy) だけ移動し、幅・高さを 2×dx / 2×dy だけ増減した矩形。</returns>
    public EsRectInt Inflate(int dx, int dy) => new(X - dx, Y - dy, Width + 2 * dx, Height + 2 * dy);

    /// <summary>指定した点が本矩形の内側に含まれるかどうかを判定する。</summary>
    /// <param name="point">判定する点（画面物理ピクセル）。</param>
    /// <returns>含まれる場合は <see langword="true"/>。</returns>
    /// <remarks>左端・上端は含み、右端・下端は含まない（半開区間）。<see cref="Width"/> / <see cref="Height"/> が 0 以下の場合は常に false。</remarks>
    public bool Contains(EsPointInt point)
        => point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    /// <summary>本矩形が別の矩形を完全に含むかどうかを判定する（右端・下端を含まない）。</summary>
    /// <param name="rect">判定する矩形。</param>
    /// <returns>完全に含む場合は true。</returns>
    public bool Contains(EsRectInt rect)
        => rect.X >= X && rect.Right <= Right && rect.Y >= Y && rect.Bottom <= Bottom;

    /// <summary>本矩形と別の矩形が重なり合うかどうかを判定する。</summary>
    /// <param name="other">判定する相手の矩形。</param>
    /// <returns>重なりがある場合は true。</returns>
    public bool Intersects(EsRectInt other)
        => other.X < Right && X < other.Right && other.Y < Bottom && Y < other.Bottom;

    /// <summary>本矩形と別の矩形の交差（重なり合う部分）を求める。</summary>
    /// <param name="other">交差を求める相手の矩形。</param>
    /// <returns>2 つの矩形の重なり部分。 重なりが無い場合は左上座標を保った空の矩形。</returns>
    /// <remarks><see cref="EsRect.Intersect(EsRect)"/> と同じ規則である（非交差時は左上を保つ）。</remarks>
    public EsRectInt Intersect(EsRectInt other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return new EsRectInt(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>2 つの矩形を包含する最小の矩形を返す（和集合）。</summary>
    /// <param name="other">もう一方の矩形。</param>
    /// <returns>両者を包含する矩形。 いずれかが空（幅・高さが 0 以下）の場合はもう一方を返す。</returns>
    /// <remarks>仮想スクリーン境界（全モニタのバウンディングボックス）の算出に使用する。</remarks>
    public EsRectInt Union(EsRectInt other)
    {
        if (Width <= 0 || Height <= 0) return other;
        if (other.Width <= 0 || other.Height <= 0) return this;

        var left = Math.Min(X, other.X);
        var top = Math.Min(Y, other.Y);
        var right = Math.Max(Right, other.Right);
        var bottom = Math.Max(Bottom, other.Bottom);
        return new EsRectInt(left, top, right - left, bottom - top);
    }

    /// <summary>点を含むように拡大した矩形を返す（和集合）。</summary>
    /// <param name="point">含める点。</param>
    /// <returns>点を含む最小の矩形。 本矩形が空の場合は点を左上とする面積 0 の矩形。</returns>
    /// <remarks>WinUI3 の <c>Rect.Union(Point)</c> と同じ操作である。</remarks>
    public EsRectInt Union(EsPointInt point)
    {
        if (Width <= 0 || Height <= 0) return new EsRectInt(point.X, point.Y, 0, 0);

        var left = Math.Min(X, point.X);
        var top = Math.Min(Y, point.Y);
        var right = Math.Max(Right, point.X);
        var bottom = Math.Max(Bottom, point.Y);
        return new EsRectInt(left, top, right - left, bottom - top);
    }

    /// <summary>小数を扱う矩形（<see cref="EsRect"/>）へ変換する。</summary>
    /// <returns>成分をそのまま引き継いだ矩形。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRect ToEsRect() => new(X, Y, Width, Height);

    /// <summary>タプルから生成する。</summary>
    /// <param name="value">(X, Y, Width, Height) 形式のタプル。</param>
    /// <returns>タプルの成分を引き継いだ矩形。</returns>
    public static EsRectInt FromTuple((int X, int Y, int Width, int Height) value)
        => new(value.X, value.Y, value.Width, value.Height);

    /// <summary>タプルへ変換する。</summary>
    /// <returns>(X, Y, Width, Height) 形式のタプル。</returns>
    public (int X, int Y, int Width, int Height) ToTuple() => (X, Y, Width, Height);
}
