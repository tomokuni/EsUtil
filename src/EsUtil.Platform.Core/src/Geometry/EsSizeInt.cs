using System.Runtime.CompilerServices;

namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の整数のサイズ（幅・高さ）を表す値オブジェクト。</summary>
/// <remarks>
/// <see cref="EsSize"/>（小数を扱う DIP 相当のサイズ）の<b>整数版</b>である。<br/>
/// 画像の実寸・画面物理ピクセルの寸法など、整数でしか意味を持たない寸法に使用する<br/>
/// （<see cref="EsPointInt"/> / <see cref="EsRectInt"/> と同じ整数系の値型）。<br/>
/// 各 UI 実装の寸法型との変換は Interop 層が提供する。<br/>
/// <br/>
/// <b><see cref="IsEmpty"/> の意味</b>: 「幅または高さが 0 以下（面積を持たない）」である。<br/>
/// .NET 標準・WinUI3・MewUI の <c>Size.IsEmpty</c> は「幅・高さとも 0」を意味するため、本型では <see cref="EsSize.HasArea"/> を使う。
/// </remarks>
/// <param name="Width">幅（整数）。</param>
/// <param name="Height">高さ（整数）。</param>
public readonly record struct EsSizeInt(int Width, int Height)
{
    /// <summary>幅・高さとも 0 のサイズを取得する。</summary>
    /// <value>幅・高さとも 0 のサイズ。</value>
    public static readonly EsSizeInt Zero = default;

    /// <summary>幅または高さが 0 以下かどうか（面積を持たない）を取得する。</summary>
    /// <value>幅または高さが 0 以下の場合は true。</value>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>幅だけを差し替えたサイズを返す。</summary>
    /// <param name="width">新しい幅。</param>
    /// <returns>高さを引き継いだサイズ。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsSizeInt WithWidth(int width) => new(width, Height);

    /// <summary>高さだけを差し替えたサイズを返す。</summary>
    /// <param name="height">新しい高さ。</param>
    /// <returns>幅を引き継いだサイズ。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsSizeInt WithHeight(int height) => new(Width, height);

    /// <summary>小数を扱うサイズ（<see cref="EsSize"/>）へ変換する。</summary>
    /// <returns>幅・高さをそのまま引き継いだサイズ。</returns>
    /// <remarks>整数の寸法をコアの寸法型として扱う場合に使用する（値は変わらない）。</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsSize ToEsSize() => new(Width, Height);

    /// <summary>小数サイズから整数サイズを作る。</summary>
    /// <param name="size">変換元の小数サイズ。</param>
    /// <param name="rounding">丸め方法（既定 <see cref="EsRounding.Round"/>）。</param>
    /// <returns>指定の方法で丸めた整数サイズ。</returns>
    /// <remarks>
    /// 丸め規則は <c>EsMath.RoundToInt</c> が単一所有する。<br/>
    /// .NET 標準の <c>Size.Ceiling</c> / <c>Size.Round</c> / <c>Size.Truncate</c> は <paramref name="rounding"/> 指定に対応する。
    /// </remarks>
    public static EsSizeInt FromEsSize(EsSize size, EsRounding rounding = EsRounding.Round)
        => new(EsMath.RoundToInt(size.Width, rounding), EsMath.RoundToInt(size.Height, rounding));

    /// <summary>タプルから生成する。</summary>
    /// <param name="value">(Width, Height) 形式のタプル。</param>
    /// <returns>タプルの成分を引き継いだサイズ。</returns>
    public static EsSizeInt FromTuple((int Width, int Height) value) => new(value.Width, value.Height);

    /// <summary>タプルへ変換する。</summary>
    /// <returns>(Width, Height) 形式のタプル。</returns>
    public (int Width, int Height) ToTuple() => (Width, Height);

    /// <summary>2 つのサイズを成分ごとに加算する。</summary>
    /// <param name="a">1 つ目のサイズ。</param>
    /// <param name="b">2 つ目のサイズ。</param>
    /// <returns>成分ごとの和。</returns>
    public static EsSizeInt operator +(EsSizeInt a, EsSizeInt b) => new(a.Width + b.Width, a.Height + b.Height);

    /// <summary>2 つのサイズを成分ごとに減算する。</summary>
    /// <param name="a">1 つ目のサイズ。</param>
    /// <param name="b">2 つ目のサイズ。</param>
    /// <returns>成分ごとの差。</returns>
    public static EsSizeInt operator -(EsSizeInt a, EsSizeInt b) => new(a.Width - b.Width, a.Height - b.Height);
}
