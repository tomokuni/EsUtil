using System.Runtime.CompilerServices;

namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の整数座標を表す値オブジェクト。</summary>
/// <remarks>
/// <see cref="EsPoint"/>（小数を扱う DIP 相当の座標）の<b>整数版</b>である。<br/>
/// 画面物理ピクセル座標（DPI スケーリング適用後）・画像ローカルピクセル座標・仮想スクリーンの座標など、<br/>
/// 整数でしか意味を持たない座標に使用する。<br/>
/// <b>整数であることは「画面物理ピクセル」を意味しない</b>（画像ピクセルも整数である）ため、<br/>
/// 単位が読む側に必要な箇所はドキュメント（param / value）に単位を明記する。<br/>
/// 小数を扱う <see cref="EsPoint"/> への変換は本型が単一所有する（UI が成分を書き写すと、同じ換算が各所に散る）。<br/>
/// 各 UI 実装の整数座標型との変換は Interop 層が提供する。<br/>
/// <br/>
/// <b>各フレームワークの対応</b>: <see cref="IsEmpty"/> / <see cref="Offset(int,int)"/> / <c>+</c> / <c>-</c> は<br/>
/// .NET 標準（<c>System.Drawing.Point</c>）に対応し、丸めは <see cref="FromEsPoint(EsPoint, EsRounding)"/> が<br/>
/// <c>Point.Truncate</c> / <c>Point.Round</c> / <c>Point.Ceiling</c> に対応する（1 つのメソッドへ集約）。
/// </remarks>
/// <param name="X">X 座標（整数）。</param>
/// <param name="Y">Y 座標（整数）。</param>
public readonly record struct EsPointInt(int X, int Y)
{
    /// <summary>原点（0, 0）を取得する。</summary>
    /// <value>X・Y とも 0 の整数座標。</value>
    public static readonly EsPointInt Zero;

    /// <summary>原点かどうか（X・Y とも 0）を取得する。</summary>
    /// <value>X・Y とも 0 の場合は true。</value>
    public bool IsEmpty => X == 0 && Y == 0;

    /// <summary>X 座標だけを差し替えた座標を返す。</summary>
    /// <param name="x">新しい X 座標。</param>
    /// <returns>Y 座標を引き継いだ座標。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsPointInt WithX(int x) => new(x, Y);

    /// <summary>Y 座標だけを差し替えた座標を返す。</summary>
    /// <param name="y">新しい Y 座標。</param>
    /// <returns>X 座標を引き継いだ座標。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsPointInt WithY(int y) => new(X, y);

    /// <summary>指定量だけ平行移動した座標を返す。</summary>
    /// <param name="dx">X 方向の移動量。</param>
    /// <param name="dy">Y 方向の移動量。</param>
    /// <returns>移動後の座標。</returns>
    /// <remarks>座標で移動する場合は <see cref="operator +(EsPointInt, EsPointInt)"/> を使う（同じ内容のため別メソッドを設けない）。</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsPointInt Offset(int dx, int dy) => new(X + dx, Y + dy);

    /// <summary>小数を扱う座標（<see cref="EsPoint"/>）へ変換する。</summary>
    /// <returns>X / Y をそのまま引き継いだ座標。</returns>
    /// <remarks>整数座標をコアの座標型として扱う場合に使用する（値は変わらない）。</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsPoint ToEsPoint() => new(X, Y);

    /// <summary>小数座標から整数座標を作る。</summary>
    /// <param name="point">変換元の小数座標。</param>
    /// <param name="rounding">丸め方法（既定 <see cref="EsRounding.Round"/>）。</param>
    /// <returns>指定の方法で丸めた整数座標。</returns>
    /// <remarks>
    /// 丸め規則は <c>EsMath.RoundToInt</c> が単一所有する（各 UI が <c>Math.Round</c> を書くと規則が散る）。<br/>
    /// .NET 標準の <c>Point.Truncate</c> / <c>Point.Round</c> / <c>Point.Ceiling</c> は<br/>
    /// 本メソッドの <paramref name="rounding"/> 指定に対応する。
    /// </remarks>
    public static EsPointInt FromEsPoint(EsPoint point, EsRounding rounding = EsRounding.Round)
        => new(EsMath.RoundToInt(point.X, rounding), EsMath.RoundToInt(point.Y, rounding));

    /// <summary>タプルから生成する。</summary>
    /// <param name="value">(X, Y) 形式のタプル。</param>
    /// <returns>タプルの成分を引き継いだ座標。</returns>
    public static EsPointInt FromTuple((int X, int Y) value) => new(value.X, value.Y);

    /// <summary>タプルへ変換する。</summary>
    /// <returns>(X, Y) 形式のタプル。</returns>
    public (int X, int Y) ToTuple() => (X, Y);

    /// <summary>2 つの座標を成分ごとに加算する。</summary>
    /// <param name="a">1 つ目の座標。</param>
    /// <param name="b">2 つ目の座標。</param>
    /// <returns>成分ごとの和。</returns>
    public static EsPointInt operator +(EsPointInt a, EsPointInt b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>2 つの座標を成分ごとに減算する。</summary>
    /// <param name="a">1 つ目の座標。</param>
    /// <param name="b">2 つ目の座標。</param>
    /// <returns>成分ごとの差。</returns>
    public static EsPointInt operator -(EsPointInt a, EsPointInt b) => new(a.X - b.X, a.Y - b.Y);
}
