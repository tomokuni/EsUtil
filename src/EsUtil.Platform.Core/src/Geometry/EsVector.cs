using System.Runtime.CompilerServices;

namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の 2 次元ベクター（オフセット）を表す値オブジェクト。</summary>
/// <remarks>
/// コアロジックは本型を使用し、UI 境界でのみ各 UI フレームワークのベクター型へ変換する（一方向依存）。<br/>
/// 各 UI 実装は、自身のベクター型との相互変換を Interop 層で提供する。<br/>
/// <br/>
/// <b>各フレームワークの対応</b>: <see cref="LengthSquared"/> / <see cref="Length"/> / <see cref="Normalize"/> /<br/>
/// <see cref="Dot"/> は .NET 標準（<c>System.Numerics.Vector2</c>）・Avalonia・MewUI に対応する<br/>
/// （Avalonia の <c>SquaredLength</c> と同義）。<see cref="Cross"/> / <see cref="Min"/> / <see cref="Max"/> / <see cref="Abs"/> は<br/>
/// .NET 標準・Avalonia に対応する。<br/>
/// <b>ゼロ ベクターの正規化</b>はゼロ ベクターを返す（MewUI と同じ。 非数を返さない）。<br/>
/// <b>WinUI3</b> には単純なベクター型が無い（<c>Windows.Foundation.Numerics.Vector2</c> は X / Y のみ）ため対応しない。
/// </remarks>
/// <param name="X">X 方向の成分（呼び出し側の座標系に従う）。</param>
/// <param name="Y">Y 方向の成分（呼び出し側の座標系に従う）。</param>
public readonly record struct EsVector(double X, double Y)
{
    /// <summary>ゼロ ベクター（0, 0）を取得する。</summary>
    /// <value>成分とも 0 のベクター。</value>
    public static readonly EsVector Zero = new(0, 0);

    /// <summary>成分とも 1 のベクターを取得する。</summary>
    /// <value>成分とも 1 のベクター（成分ごとの乗算の単位元）。</value>
    public static readonly EsVector One = new(1, 1);

    /// <summary>X 軸方向の単位ベクター（1, 0）を取得する。</summary>
    /// <value>X 成分が 1、Y 成分が 0 のベクター。</value>
    public static readonly EsVector UnitX = new(1, 0);

    /// <summary>Y 軸方向の単位ベクター（0, 1）を取得する。</summary>
    /// <value>X 成分が 0、Y 成分が 1 のベクター。</value>
    public static readonly EsVector UnitY = new(0, 1);

    /// <summary>長さの 2 乗を取得する。</summary>
    /// <value>X² + Y²（常に 0 以上）。</value>
    /// <remarks>Avalonia の <c>Vector.SquaredLength</c> と同じ値である。 平方根を避けたい比較（距離の大小判定等）で使う。</remarks>
    public double LengthSquared => X * X + Y * Y;

    /// <summary>長さ（ユークリッド ノルム）を取得する。</summary>
    /// <value>√(X² + Y²)（常に 0 以上）。</value>
    public double Length => Math.Sqrt(LengthSquared);

    /// <summary>X 成分だけを差し替えたベクターを返す。</summary>
    /// <param name="x">新しい X 成分。</param>
    /// <returns>Y 成分を引き継いだベクター。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsVector WithX(double x) => new(x, Y);

    /// <summary>Y 成分だけを差し替えたベクターを返す。</summary>
    /// <param name="y">新しい Y 成分。</param>
    /// <returns>X 成分を引き継いだベクター。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsVector WithY(double y) => new(X, y);

    /// <summary>成分の絶対値を取ったベクターを返す。</summary>
    /// <returns>各成分の絶対値を持つベクター。</returns>
    public EsVector Abs() => new(Math.Abs(X), Math.Abs(Y));

    /// <summary>同じ向きの単位ベクターを返す。</summary>
    /// <returns>長さ 1 のベクター。 ゼロ ベクターの場合はゼロ ベクター。</returns>
    /// <remarks>ゼロ ベクターで非数（0 ÷ 0）を返さないため、長さが 0 の場合は <see cref="Zero"/> を返す。</remarks>
    public EsVector Normalize()
    {
        var length = Length;
        return length > 0 ? new EsVector(X / length, Y / length) : Zero;
    }

    /// <summary>成分ごとに小さい方を採ったベクターを返す。</summary>
    /// <param name="left">1 つ目のベクター。</param>
    /// <param name="right">2 つ目のベクター。</param>
    /// <returns>成分ごとの最小値を持つベクター。</returns>
    public static EsVector Min(EsVector left, EsVector right)
        => new(Math.Min(left.X, right.X), Math.Min(left.Y, right.Y));

    /// <summary>成分ごとに大きい方を採ったベクターを返す。</summary>
    /// <param name="left">1 つ目のベクター。</param>
    /// <param name="right">2 つ目のベクター。</param>
    /// <returns>成分ごとの最大値を持つベクター。</returns>
    public static EsVector Max(EsVector left, EsVector right)
        => new(Math.Max(left.X, right.X), Math.Max(left.Y, right.Y));

    /// <summary>2 つのベクターの内積を求める。</summary>
    /// <param name="a">1 つ目のベクター。</param>
    /// <param name="b">2 つ目のベクター。</param>
    /// <returns>内積（a.X × b.X + a.Y × b.Y）。</returns>
    public static double Dot(EsVector a, EsVector b) => a.X * b.X + a.Y * b.Y;

    /// <summary>2 つのベクターの外積（2 次元のスカラー）を求める。</summary>
    /// <param name="a">1 つ目のベクター。</param>
    /// <param name="b">2 つ目のベクター。</param>
    /// <returns>外積（a.X × b.Y − a.Y × b.X）。 正なら b は a の左回り。</returns>
    public static double Cross(EsVector a, EsVector b) => a.X * b.Y - a.Y * b.X;

    /// <summary>座標へ変換する。</summary>
    /// <returns>X・Y をそのまま引き継いだ座標。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsPoint ToEsPoint() => new(X, Y);

    /// <summary>座標から生成する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>X・Y をそのまま引き継いだベクター。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EsVector FromEsPoint(EsPoint point) => new(point.X, point.Y);

    /// <summary>タプルから生成する。</summary>
    /// <param name="value">(X, Y) 形式のタプル。</param>
    /// <returns>タプルの成分を引き継いだベクター。</returns>
    public static EsVector FromTuple((double X, double Y) value) => new(value.X, value.Y);

    /// <summary>タプルへ変換する。</summary>
    /// <returns>(X, Y) 形式のタプル。</returns>
    public (double X, double Y) ToTuple() => (X, Y);

    /// <summary>成分ごとに加算する。</summary>
    /// <param name="a">1 つ目のベクター。</param>
    /// <param name="b">2 つ目のベクター。</param>
    /// <returns>成分ごとの和。</returns>
    public static EsVector operator +(EsVector a, EsVector b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>成分ごとに減算する。</summary>
    /// <param name="a">1 つ目のベクター。</param>
    /// <param name="b">2 つ目のベクター。</param>
    /// <returns>成分ごとの差。</returns>
    public static EsVector operator -(EsVector a, EsVector b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>符号を反転する。</summary>
    /// <param name="vector">反転するベクター。</param>
    /// <returns>成分の符号を反転したベクター。</returns>
    public static EsVector operator -(EsVector vector) => new(-vector.X, -vector.Y);

    /// <summary>成分ごとに乗算する（スケーリング）。</summary>
    /// <param name="vector">対象のベクター。</param>
    /// <param name="k">係数。</param>
    /// <returns>成分ごとに係数倍したベクター。</returns>
    public static EsVector operator *(EsVector vector, double k) => new(vector.X * k, vector.Y * k);

    /// <summary>成分ごとに乗算する（スケーリング）。</summary>
    /// <param name="k">係数。</param>
    /// <param name="vector">対象のベクター。</param>
    /// <returns>成分ごとに係数倍したベクター。</returns>
    public static EsVector operator *(double k, EsVector vector) => new(vector.X * k, vector.Y * k);

    /// <summary>成分ごとに除算する。</summary>
    /// <param name="vector">対象のベクター。</param>
    /// <param name="k">除数。</param>
    /// <returns>成分ごとに除算したベクター。</returns>
    public static EsVector operator /(EsVector vector, double k) => new(vector.X / k, vector.Y / k);
}
