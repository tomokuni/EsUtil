using System.Globalization;
using System.Runtime.CompilerServices;

namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の 2 次元座標を表す値オブジェクト。</summary>
/// <remarks>
/// コアロジックは本型を使用し、UI 境界でのみ各 UI フレームワークの座標型へ変換する（一方向依存）。<br/>
/// 各 UI 実装は、自身の座標型との相互変換を Interop 層で提供する。<br/>
/// 座標ラベルの文言は <see cref="Format(int)"/> が単一所有する。<br/>
/// <br/>
/// <b>各フレームワークの対応</b>: <see cref="WithX"/> / <see cref="WithY"/> / <see cref="Offset(double,double)"/> は<br/>
/// MewUI（<c>Point</c>）と Avalonia（<c>WithX</c> / <c>WithY</c>）、<see cref="IsEmpty"/> は .NET 標準<br/>
/// （<c>System.Drawing.PointF.IsEmpty</c>）に対応する。<br/>
/// <b>Point − Point</b> は Avalonia が座標、MewUI がベクターを返し意味が相反するため、本型は<br/>
/// ベクター（<see cref="EsVector"/>）を返す側に統一する（座標が必要な場合は成分を使う）。<br/>
/// <b>Parse と ToString の上書き</b>は意図的に設けない（レコードの診断表現を丸めた表示文字列へ置き換えないため）。
/// </remarks>
/// <param name="X">X 座標（呼び出し側の座標系に従う。 DIP または対象座標）。</param>
/// <param name="Y">Y 座標（呼び出し側の座標系に従う。 DIP または対象座標）。</param>
public readonly record struct EsPoint(double X, double Y)
{
    /// <summary>表示に使える小数点以下の最大桁数（<see cref="double"/> の有効桁数）。</summary>
    private const int MaxDecimals = 15;

    /// <summary>原点（0, 0）を取得する。</summary>
    /// <value>X・Y とも 0 の座標。</value>
    public static readonly EsPoint Zero = new(0, 0);

    /// <summary>原点かどうか（X・Y とも 0）を取得する。</summary>
    /// <value>X・Y とも 0 の場合は true。</value>
    /// <remarks>
    /// .NET 標準（<c>System.Drawing.PointF.IsEmpty</c>）と同じ意味である（<see cref="Zero"/> と同じ判定）。<br/>
    /// サイズ・矩形の <c>IsEmpty</c> は「面積なし（0 以下）」を意味するため、意味が異なる。
    /// </remarks>
    public bool IsEmpty => X == 0 && Y == 0;

    /// <summary>座標を <c>(x, y)</c> 形式の表示用テキストへ整形する。</summary>
    /// <param name="decimals">小数点以下の桁数（0〜<see cref="MaxDecimals"/>。 既定 0 = 整数に丸める）。</param>
    /// <returns><c>(x, y)</c> 形式の表示テキスト（桁数に合わせて丸める）。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decimals"/> が 0 未満または <see cref="MaxDecimals"/> より大きい場合。</exception>
    /// <remarks>
    /// 座標ラベル（カーソル座標・ルーペ中心座標）の文言を単一所有する（各コントロールが書くと書式がずれる）。<br/>
    /// <c>ToString()</c> を上書きしないのは、レコードの診断表現（デバッガー・ログ・テストの失敗表示）を<br/>
    /// 丸めた表示文字列へ置き換えないためである（幾何の調査では小数を読む必要がある）。
    /// </remarks>
    public string Format(int decimals = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimals, MaxDecimals);

        var format = $"F{decimals}";
        return $"({X.ToString(format, CultureInfo.CurrentCulture)}, {Y.ToString(format, CultureInfo.CurrentCulture)})";
    }

    /// <summary>X 座標だけを差し替えた座標を返す。</summary>
    /// <param name="x">新しい X 座標。</param>
    /// <returns>Y 座標を引き継いだ座標。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsPoint WithX(double x) => new(x, Y);

    /// <summary>Y 座標だけを差し替えた座標を返す。</summary>
    /// <param name="y">新しい Y 座標。</param>
    /// <returns>X 座標を引き継いだ座標。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsPoint WithY(double y) => new(X, y);

    /// <summary>指定量だけ平行移動した座標を返す。</summary>
    /// <param name="dx">X 方向の移動量。</param>
    /// <param name="dy">Y 方向の移動量。</param>
    /// <returns>移動後の座標。</returns>
    /// <remarks>ベクターで移動する場合は <see cref="operator +(EsPoint, EsVector)"/> を使う（同じ内容のため別メソッドを設けない）。</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsPoint Offset(double dx, double dy) => new(X + dx, Y + dy);

    /// <summary>指定した座標までのユークリッド距離を求める。</summary>
    /// <param name="other">距離を測る相手の座標。</param>
    /// <returns>2 点間の距離（常に 0 以上）。</returns>
    public double DistanceTo(EsPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>2 つの座標がほぼ等しいかどうかを判定する。</summary>
    /// <param name="other">比較する座標。</param>
    /// <returns>許容誤差の範囲で等しい場合は true。</returns>
    /// <remarks>許容誤差の判定は <c>EsMath.AreClose</c> が単一所有する（<c>==</c> は浮動小数の厳密比較）。</remarks>
    public bool NearlyEquals(EsPoint other) => EsMath.AreClose(X, other.X) && EsMath.AreClose(Y, other.Y);

    /// <summary>ベクターへ変換する。</summary>
    /// <returns>X・Y をそのまま引き継いだベクター。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsVector ToEsVector() => new(X, Y);

    /// <summary>タプルから生成する。</summary>
    /// <param name="value">(X, Y) 形式のタプル。</param>
    /// <returns>タプルの成分を引き継いだ座標。</returns>
    public static EsPoint FromTuple((double X, double Y) value) => new(value.X, value.Y);

    /// <summary>タプルへ変換する。</summary>
    /// <returns>(X, Y) 形式のタプル。</returns>
    public (double X, double Y) ToTuple() => (X, Y);

    /// <summary>2 つの座標を成分ごとに加算する。</summary>
    /// <param name="a">1 つ目の座標。</param>
    /// <param name="b">2 つ目の座標。</param>
    /// <returns>成分ごとの和。</returns>
    public static EsPoint operator +(EsPoint a, EsPoint b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>座標をベクター分だけ平行移動する。</summary>
    /// <param name="point">移動する座標。</param>
    /// <param name="vector">移動量。</param>
    /// <returns>移動後の座標。</returns>
    public static EsPoint operator +(EsPoint point, EsVector vector) => new(point.X + vector.X, point.Y + vector.Y);

    /// <summary>2 つの座標の差分をベクターとして求める。</summary>
    /// <param name="a">1 つ目の座標。</param>
    /// <param name="b">2 つ目の座標。</param>
    /// <returns>a から b へのベクター。</returns>
    public static EsVector operator -(EsPoint a, EsPoint b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>座標をベクター分だけ逆方向へ平行移動する。</summary>
    /// <param name="point">移動する座標。</param>
    /// <param name="vector">移動量。</param>
    /// <returns>移動後の座標。</returns>
    public static EsPoint operator -(EsPoint point, EsVector vector) => new(point.X - vector.X, point.Y - vector.Y);

    /// <summary>座標の符号を反転する。</summary>
    /// <param name="point">反転する座標。</param>
    /// <returns>X・Y の符号を反転した座標。</returns>
    public static EsPoint operator -(EsPoint point) => new(-point.X, -point.Y);

    /// <summary>座標を係数倍する。</summary>
    /// <param name="point">対象の座標。</param>
    /// <param name="k">係数。</param>
    /// <returns>成分ごとに係数倍した座標。</returns>
    public static EsPoint operator *(EsPoint point, double k) => new(point.X * k, point.Y * k);

    /// <summary>座標を係数倍する。</summary>
    /// <param name="k">係数。</param>
    /// <param name="point">対象の座標。</param>
    /// <returns>成分ごとに係数倍した座標。</returns>
    public static EsPoint operator *(double k, EsPoint point) => new(point.X * k, point.Y * k);

    /// <summary>座標を除算する。</summary>
    /// <param name="point">対象の座標。</param>
    /// <param name="k">除数。</param>
    /// <returns>成分ごとに除算した座標。</returns>
    public static EsPoint operator /(EsPoint point, double k) => new(point.X / k, point.Y / k);

    // 演算子の代替メソッド（CA2225）。 演算子を持たない言語（F# 等）や、演算子を書けない文脈
    // （式木・リフレクション経由の呼び出し）からも同じ計算を呼べるようにする。

    /// <summary>2 つの座標を成分ごとに加算する（演算子 <c>+</c> の代替）。</summary>
    /// <param name="a">1 つ目の座標。</param>
    /// <param name="b">2 つ目の座標。</param>
    /// <returns>成分ごとの和。</returns>
    public static EsPoint Add(EsPoint a, EsPoint b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>座標をベクター分だけ平行移動する（演算子 <c>+</c> の代替）。</summary>
    /// <param name="point">移動する座標。</param>
    /// <param name="vector">移動量。</param>
    /// <returns>移動後の座標。</returns>
    public static EsPoint Add(EsPoint point, EsVector vector) => new(point.X + vector.X, point.Y + vector.Y);

    /// <summary>2 つの座標の差分をベクターとして求める（演算子 <c>-</c> の代替）。</summary>
    /// <param name="a">1 つ目の座標。</param>
    /// <param name="b">2 つ目の座標。</param>
    /// <returns>a から b へのベクター。</returns>
    public static EsVector Subtract(EsPoint a, EsPoint b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>座標をベクター分だけ逆方向へ平行移動する（演算子 <c>-</c> の代替）。</summary>
    /// <param name="point">移動する座標。</param>
    /// <param name="vector">移動量。</param>
    /// <returns>移動後の座標。</returns>
    public static EsPoint Subtract(EsPoint point, EsVector vector) => new(point.X - vector.X, point.Y - vector.Y);

    /// <summary>座標の符号を反転する（単項演算子 <c>-</c> の代替）。</summary>
    /// <param name="point">反転する座標。</param>
    /// <returns>X・Y の符号を反転した座標。</returns>
    public static EsPoint Negate(EsPoint point) => new(-point.X, -point.Y);

    /// <summary>座標を係数倍する（演算子 <c>*</c> の代替）。</summary>
    /// <param name="point">対象の座標。</param>
    /// <param name="k">係数。</param>
    /// <returns>成分ごとに係数倍した座標。</returns>
    public static EsPoint Multiply(EsPoint point, double k) => new(point.X * k, point.Y * k);

    /// <summary>座標を係数倍する（演算子 <c>*</c> の代替。 係数を左に取る形）。</summary>
    /// <param name="k">係数。</param>
    /// <param name="point">対象の座標。</param>
    /// <returns>成分ごとに係数倍した座標。</returns>
    public static EsPoint Multiply(double k, EsPoint point) => new(point.X * k, point.Y * k);

    /// <summary>座標を除算する（演算子 <c>/</c> の代替）。</summary>
    /// <param name="point">対象の座標。</param>
    /// <param name="k">除数。</param>
    /// <returns>成分ごとに除算した座標。</returns>
    public static EsPoint Divide(EsPoint point, double k) => new(point.X / k, point.Y / k);
}
