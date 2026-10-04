using System.Runtime.CompilerServices;

namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の 2 次元サイズを表す値オブジェクト。</summary>
/// <remarks>
/// コアロジックは本型を使用し、UI 境界でのみ各 UI フレームワークのサイズ型へ変換する（一方向依存）。<br/>
/// 各 UI 実装は、自身のサイズ型との相互変換を Interop 層で提供する。<br/>
/// 「レイアウト前の領域（幅・高さが 0）ではクランプしない」という領域の規則は本型が単一所有する。<br/>
/// <br/>
/// <b>各フレームワークの対応</b>: <see cref="HasArea"/>（幅・高さとも正）は本型独自の判定であり、<br/>
/// <see cref="WithWidth"/> / <see cref="WithHeight"/> / <see cref="Constrain"/> / <see cref="AspectRatio"/> /<br/>
/// <see cref="Inflate(EsThickness)"/> / <see cref="Deflate(EsThickness)"/> と四則演算は<br/>
/// Avalonia・MewUI・.NET 標準に対応する（.NET 標準・WinUI3・MewUI の <c>IsEmpty</c>（幅・高さとも 0）は<br/>
/// 「面積なし」を意味しないため、本型は <see cref="HasArea"/> を使う）。<br/>
/// <b>負の値</b>: MewUI は生成時に 0 へクランプするが、本型は保持する（Avalonia・.NET 標準と同じ。 正規化は呼び出し側が行う）。
/// </remarks>
/// <param name="Width">幅（呼び出し側の座標系に従う。 DIP または対象座標）。</param>
/// <param name="Height">高さ（呼び出し側の座標系に従う。 DIP または対象座標）。</param>
public readonly record struct EsSize(double Width, double Height)
{
    /// <summary>幅・高さとも 0 のサイズを取得する。</summary>
    /// <value>幅・高さとも 0 のサイズ。</value>
    public static readonly EsSize Zero = new(0, 0);

    /// <summary>幅・高さとも正の無限大のサイズを取得する。</summary>
    /// <value>上限が無いことを表すサイズ。</value>
    public static readonly EsSize Infinity = new(double.PositiveInfinity, double.PositiveInfinity);

    /// <summary>領域として面積が確定しているか（幅・高さとも正か）を取得する。</summary>
    /// <value>幅・高さとも 0 より大きい場合は true。</value>
    /// <remarks>レイアウト前・親未接続の領域は幅または高さが 0 になるため、確定していないと判定する。</remarks>
    public bool HasArea => Width > 0 && Height > 0;

    /// <summary>縦横比（幅 ÷ 高さ）を取得する。</summary>
    /// <value>幅 ÷ 高さ。 高さが 0 の場合は無限大または非数になる。</value>
    public double AspectRatio => Width / Height;

    /// <summary>幅だけを差し替えたサイズを返す。</summary>
    /// <param name="width">新しい幅。</param>
    /// <returns>高さを引き継いだサイズ。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsSize WithWidth(double width) => new(width, Height);

    /// <summary>高さだけを差し替えたサイズを返す。</summary>
    /// <param name="height">新しい高さ。</param>
    /// <returns>幅を引き継いだサイズ。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsSize WithHeight(double height) => new(Width, height);

    /// <summary>上限を適用したサイズを返す（成分ごとに小さい方を採る）。</summary>
    /// <param name="constraint">上限となるサイズ。</param>
    /// <returns>成分ごとに小さい方のサイズ。</returns>
    public EsSize Constrain(EsSize constraint)
        => new(Math.Min(Width, constraint.Width), Math.Min(Height, constraint.Height));

    /// <summary>4 辺の厚さの分だけ拡大したサイズを返す。</summary>
    /// <param name="thickness">加える厚さ。</param>
    /// <returns>幅へ左右、高さへ上下を加えたサイズ。</returns>
    public EsSize Inflate(EsThickness thickness)
        => new(Width + thickness.Horizontal, Height + thickness.Vertical);

    /// <summary>4 辺の厚さの分だけ縮小したサイズを返す（負のサイズにはならない）。</summary>
    /// <param name="thickness">減らす厚さ。</param>
    /// <returns>幅へ左右、高さへ上下を減らしたサイズ（結果は 0 以上）。</returns>
    /// <remarks>厚さが元のサイズを超える場合は 0 にクランプする（Avalonia・MewUI と同じ規則）。</remarks>
    public EsSize Deflate(EsThickness thickness)
        => new(Math.Max(0, Width - thickness.Horizontal), Math.Max(0, Height - thickness.Vertical));

    /// <summary>サイズを本領域の最小辺へクランプする（領域が未確定の場合はクランプしない）。</summary>
    /// <param name="size">クランプするサイズ（正の値）。</param>
    /// <returns>クランプ後のサイズ。 領域が未確定の場合は <paramref name="size"/> のまま。</returns>
    /// <remarks>正方形の領域へ収める用途を想定し、幅・高さの小さい方（最小辺）を上限とする。</remarks>
    public double ClampToArea(double size)
        => HasArea ? Math.Min(size, Math.Min(Width, Height)) : size;

    /// <summary>本領域内の位置（左上オフセット）を 1 軸分クランプする（領域が未確定の場合はクランプしない）。</summary>
    /// <param name="offset">クランプするオフセット（領域の左上を原点とする）。</param>
    /// <param name="size">領域内へ収める対象の 1 軸のサイズ。</param>
    /// <param name="axis">クランプに使う軸（<see cref="EsAxis.X"/> = 幅 / <see cref="EsAxis.Y"/> = 高さ）。</param>
    /// <returns>クランプ後のオフセット（0〜<c>領域の長さ − サイズ</c>）。 対象が収まらない場合は 0。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="axis"/> が <see cref="EsAxis"/> の定義値でない場合。</exception>
    /// <remarks>
    /// 軸ごとに呼ぶ（縦横で領域の長さが異なるため）。 領域の長さと対象のサイズの差を上限とするため、<br/>
    /// 対象が領域より大きい場合は 0（左上寄せ）になる。
    /// </remarks>
    public double ClampOffset(double offset, double size, EsAxis axis)
    {
        var areaLength = axis switch
        {
            EsAxis.X => Width,
            EsAxis.Y => Height,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "軸は EsAxis の定義値である必要があります。"),
        };

        return areaLength > 0 ? Math.Clamp(offset, 0, Math.Max(0, areaLength - size)) : offset;
    }

    /// <summary>2 つのサイズがほぼ等しいかどうかを判定する。</summary>
    /// <param name="other">比較するサイズ。</param>
    /// <returns>許容誤差の範囲で等しい場合は true。</returns>
    /// <remarks>許容誤差の判定は <c>EsMath.AreClose</c> が単一所有する。</remarks>
    public bool NearlyEquals(EsSize other) => EsMath.AreClose(Width, other.Width) && EsMath.AreClose(Height, other.Height);

    /// <summary>タプルから生成する。</summary>
    /// <param name="value">(Width, Height) 形式のタプル。</param>
    /// <returns>タプルの成分を引き継いだサイズ。</returns>
    public static EsSize FromTuple((double Width, double Height) value) => new(value.Width, value.Height);

    /// <summary>タプルへ変換する。</summary>
    /// <returns>(Width, Height) 形式のタプル。</returns>
    public (double Width, double Height) ToTuple() => (Width, Height);

    /// <summary>2 つのサイズを成分ごとに加算する。</summary>
    /// <param name="a">1 つ目のサイズ。</param>
    /// <param name="b">2 つ目のサイズ。</param>
    /// <returns>成分ごとの和。</returns>
    public static EsSize operator +(EsSize a, EsSize b) => new(a.Width + b.Width, a.Height + b.Height);

    /// <summary>2 つのサイズを成分ごとに減算する。</summary>
    /// <param name="a">1 つ目のサイズ。</param>
    /// <param name="b">2 つ目のサイズ。</param>
    /// <returns>成分ごとの差。</returns>
    public static EsSize operator -(EsSize a, EsSize b) => new(a.Width - b.Width, a.Height - b.Height);

    /// <summary>サイズを係数倍する。</summary>
    /// <param name="size">対象のサイズ。</param>
    /// <param name="k">係数。</param>
    /// <returns>成分ごとに係数倍したサイズ。</returns>
    public static EsSize operator *(EsSize size, double k) => new(size.Width * k, size.Height * k);

    /// <summary>サイズを除算する。</summary>
    /// <param name="size">対象のサイズ。</param>
    /// <param name="k">除数。</param>
    /// <returns>成分ごとに除算したサイズ。</returns>
    public static EsSize operator /(EsSize size, double k) => new(size.Width / k, size.Height / k);

    /// <summary>サイズをベクターの成分ごとに乗算する。</summary>
    /// <param name="size">対象のサイズ。</param>
    /// <param name="scale">成分ごとの係数。</param>
    /// <returns>成分ごとに乗算したサイズ。</returns>
    public static EsSize operator *(EsSize size, EsVector scale) => new(size.Width * scale.X, size.Height * scale.Y);

    /// <summary>2 つのサイズの比をベクターとして求める。</summary>
    /// <param name="left">比較するサイズ。</param>
    /// <param name="right">基準となるサイズ。</param>
    /// <returns>成分ごとの比（左 ÷ 右）を表すベクター。</returns>
    /// <remarks>縦横比の差（元画像と表示領域のスケール）を求めるために使う。</remarks>
    public static EsVector operator /(EsSize left, EsSize right)
        => new(left.Width / right.Width, left.Height / right.Height);

    // 演算子の代替メソッド（CA2225）。 演算子を持たない言語や、演算子を書けない文脈からも呼べるようにする。

    /// <summary>2 つのサイズを成分ごとに加算する（演算子 <c>+</c> の代替）。</summary>
    /// <param name="a">1 つ目のサイズ。</param>
    /// <param name="b">2 つ目のサイズ。</param>
    /// <returns>成分ごとの和。</returns>
    public static EsSize Add(EsSize a, EsSize b) => new(a.Width + b.Width, a.Height + b.Height);

    /// <summary>2 つのサイズを成分ごとに減算する（演算子 <c>-</c> の代替）。</summary>
    /// <param name="a">1 つ目のサイズ。</param>
    /// <param name="b">2 つ目のサイズ。</param>
    /// <returns>成分ごとの差。</returns>
    public static EsSize Subtract(EsSize a, EsSize b) => new(a.Width - b.Width, a.Height - b.Height);

    /// <summary>サイズを係数倍する（演算子 <c>*</c> の代替）。</summary>
    /// <param name="size">対象のサイズ。</param>
    /// <param name="k">係数。</param>
    /// <returns>成分ごとに係数倍したサイズ。</returns>
    public static EsSize Multiply(EsSize size, double k) => new(size.Width * k, size.Height * k);

    /// <summary>サイズを除算する（演算子 <c>/</c> の代替）。</summary>
    /// <param name="size">対象のサイズ。</param>
    /// <param name="k">除数。</param>
    /// <returns>成分ごとに除算したサイズ。</returns>
    public static EsSize Divide(EsSize size, double k) => new(size.Width / k, size.Height / k);

    /// <summary>サイズをベクターの成分ごとに乗算する（演算子 <c>*</c> の代替）。</summary>
    /// <param name="size">対象のサイズ。</param>
    /// <param name="scale">成分ごとの係数。</param>
    /// <returns>成分ごとに乗算したサイズ。</returns>
    public static EsSize Multiply(EsSize size, EsVector scale) => new(size.Width * scale.X, size.Height * scale.Y);

    /// <summary>2 つのサイズの比をベクターとして求める（演算子 <c>/</c> の代替）。</summary>
    /// <param name="left">比較するサイズ。</param>
    /// <param name="right">基準となるサイズ。</param>
    /// <returns>成分ごとの比（左 ÷ 右）を表すベクター。</returns>
    public static EsVector Divide(EsSize left, EsSize right)
        => new(left.Width / right.Width, left.Height / right.Height);
}
