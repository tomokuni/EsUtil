using System.Globalization;

namespace EsUtil.Platform.Core.Svg;

/// <summary>SVG のパス データ（<c>d</c> 属性）を UI に依存しない図形の列へ変換する。</summary>
/// <remarks>
/// <b>なぜ自前で解析するか</b>: Fluent System Icons の SVG は 4 つの UI 版が共有するが、WinUI 3 には<br/>
/// SVG を読む API が無く、XAML の型変換（文字列 → <c>Geometry</c>）はリフレクション経路のため AOT で使えない。<br/>
/// 本クラスは<b>共有する SVG ファイルを唯一の出所にする</b>ために最小限の部分集合だけを解析する<br/>
/// （アイコンは <c>M</c> / <c>L</c> / <c>H</c> / <c>V</c> / <c>C</c> / <c>Z</c> だけで構成される。 実測 2026-10-20）。<br/>
/// <br/>
/// <b>UI に依存しない形で返す理由</b>: 画素への展開（<see cref="SvgPathRasterizer"/>）と WinUI 3 の<br/>
/// 形状への変換（<see cref="SvgIconSource"/> と各 UI の <c>IconLoader</c>）が同じ解析結果を使う。 WinUI 3 の <c>PathGeometry</c> の<br/>
/// コレクションは<b>列挙できない</b>（<c>IList</c> の列挙子の取得が実行時に失敗する。 実測 2026-10-20）ため、<br/>
/// 図形は<b>追加するだけ</b>にする。<br/>
/// <br/>
/// <b>未対応のコマンド</b>（円弧 <c>A</c> 等）が現れた場合は解析を失敗させ、呼び出し側は空の形状で代替する<br/>
/// （誤った形を描くより、描かない方が安全である）。
/// </remarks>
public static class SvgPathParser
{
    /// <summary>SVG のパス データを図形の列へ変換する。</summary>
    /// <param name="data">パス データ（<c>d</c> 属性の値）。</param>
    /// <returns>図形の列。 解析できない場合は空。</returns>
    /// <remarks>例外は投げない（アイコンの欠落・書式の相違で UI を壊さないため）。</remarks>
    public static IReadOnlyList<SvgPathFigure> Parse(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return [];
        }

        try
        {
            return Build(data);
        }
        catch (FormatException)
        {
            // 未対応・壊れたパス データは空で代替する（描画は何も現れない）
            return [];
        }
    }

    /// <summary>パス データを読み、図形を組み立てる。</summary>
    /// <param name="data">パス データ。</param>
    /// <returns>図形の列。</returns>
    /// <exception cref="FormatException">未対応のコマンドが現れた場合、または数値を読み取れない場合。</exception>
    private static List<SvgPathFigure> Build(string data)
    {
        var figures = new List<SvgPathFigure>();

        // 現在位置と部分パスの開始位置（Z で戻る先）
        double x = 0, y = 0, startX = 0, startY = 0;

        // 直前の 3 次ベジェの第 2 制御点（S の第 1 制御点はこの点の反射）と、直前の 2 次ベジェの制御点
        SvgPathFigureBuilder? figure = null;

        var index = 0;
        while (true)
        {
            SkipSeparators(data, ref index);
            if (index >= data.Length)
            {
                break;
            }

            var command = data[index];
            if (!char.IsAsciiLetter(command))
            {
                throw new FormatException("コマンドの位置に文字以外があります。");
            }

            index++;
            var relative = char.IsAsciiLetterLower(command);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    {
                        ReadPoint(data, ref index, out var mx, out var my);
                        if (relative)
                        {
                            mx += x;
                            my += y;
                        }

                        AddFigure(figures, ref figure, mx, my);
                        x = mx;
                        y = my;
                        startX = mx;
                        startY = my;

                        // M の後ろに続く座標は L として扱う（SVG の規則）
                        while (TryReadPoint(data, ref index, out var lx, out var ly))
                        {
                            var nx = relative ? x + lx : lx;
                            var ny = relative ? y + ly : ly;
                            Require(figure).AddLine(nx, ny);
                            x = nx;
                            y = ny;
                        }

                        break;
                    }

                case 'L':
                    while (TryReadPoint(data, ref index, out var lx, out var ly))
                    {
                        var nx = relative ? x + lx : lx;
                        var ny = relative ? y + ly : ly;
                        Require(figure).AddLine(nx, ny);
                        x = nx;
                        y = ny;
                    }

                    break;

                case 'H':
                    while (TryReadNumber(data, ref index, out var hx))
                    {
                        var nx = relative ? x + hx : hx;
                        Require(figure).AddLine(nx, y);
                        x = nx;
                    }

                    break;

                case 'V':
                    while (TryReadNumber(data, ref index, out var vy))
                    {
                        var ny = relative ? y + vy : vy;
                        Require(figure).AddLine(x, ny);
                        y = ny;
                    }

                    break;

                case 'C':
                    while (TryReadPoint(data, ref index, out var c1x, out var c1y))
                    {
                        if (!TryReadPoint(data, ref index, out var c2x, out var c2y)
                            || !TryReadPoint(data, ref index, out var ex, out var ey))
                        {
                            throw new FormatException("3 次ベジェの座標が不足しています。");
                        }

                        if (relative)
                        {
                            c1x += x; c1y += y;
                            c2x += x; c2y += y;
                            ex += x; ey += y;
                        }

                        Require(figure).AddCubic(c1x, c1y, c2x, c2y, ex, ey);
                        x = ex;
                        y = ey;
                    }

                    break;

                case 'Z':
                    Require(figure).Close();
                    x = startX;
                    y = startY;
                    break;

                default:
                    throw new FormatException($"未対応のコマンドです: {command}");
            }
        }

        if (figure is not null)
        {
            figures.Add(figure.Build());
        }

        return figures;
    }

    /// <summary>図形を確定して次の図形を開始する。</summary>
    /// <param name="figures">確定済みの図形の一覧。</param>
    /// <param name="figure">組み立て中の図形。</param>
    /// <param name="x">開始点の X 座標。</param>
    /// <param name="y">開始点の Y 座標。</param>
    private static void AddFigure(
        List<SvgPathFigure> figures, ref SvgPathFigureBuilder? figure, double x, double y)
    {
        if (figure is not null)
        {
            figures.Add(figure.Build());
        }

        figure = SvgPathFigureBuilder.Start(x, y);
    }

    /// <summary>組み立て中の図形があることを確かめる。</summary>
    /// <param name="figure">組み立て中の図形。</param>
    /// <returns>組み立て中の図形。</returns>
    /// <exception cref="FormatException">先頭の <c>M</c> より前に描画コマンドが現れた場合。</exception>
    private static SvgPathFigureBuilder Require(SvgPathFigureBuilder? figure)
        => figure ?? throw new FormatException("M より前に描画コマンドがあります。");

    /// <summary>区切り文字（空白・カンマ）を読み飛ばす。</summary>
    /// <param name="data">パス データ。</param>
    /// <param name="index">読み取り位置。</param>
    private static void SkipSeparators(string data, ref int index)
    {
        while (index < data.Length && (char.IsWhiteSpace(data[index]) || data[index] == ','))
        {
            index++;
        }
    }

    /// <summary>座標の組を読む。</summary>
    /// <param name="data">パス データ。</param>
    /// <param name="index">読み取り位置。</param>
    /// <param name="x">読み取った X 座標。</param>
    /// <param name="y">読み取った Y 座標。</param>
    /// <returns>読み取れた場合は true。</returns>
    private static bool TryReadPoint(string data, ref int index, out double x, out double y)
    {
        var position = index;
        if (TryReadNumber(data, ref index, out x) && TryReadNumber(data, ref index, out y))
        {
            return true;
        }

        index = position;
        x = 0;
        y = 0;
        return false;
    }

    /// <summary>座標の組を読み、読めない場合は例外にする。</summary>
    /// <param name="data">パス データ。</param>
    /// <param name="index">読み取り位置。</param>
    /// <param name="x">読み取った X 座標。</param>
    /// <param name="y">読み取った Y 座標。</param>
    /// <exception cref="FormatException">座標を読み取れない場合。</exception>
    private static void ReadPoint(string data, ref int index, out double x, out double y)
    {
        if (!TryReadPoint(data, ref index, out x, out y))
        {
            throw new FormatException("座標を読み取れません。");
        }
    }

    /// <summary>数値を 1 つ読む。</summary>
    /// <param name="data">パス データ。</param>
    /// <param name="index">読み取り位置（読めた分だけ進む）。</param>
    /// <param name="value">読み取った値。</param>
    /// <returns>読み取れた場合は true。</returns>
    /// <remarks>符号・小数・指数表記に対応する（<c>1.5e-3</c> など）。</remarks>
    private static bool TryReadNumber(string data, ref int index, out double value)
    {
        SkipSeparators(data, ref index);

        var start = index;
        if (index < data.Length && (data[index] == '+' || data[index] == '-'))
        {
            index++;
        }

        var digits = false;
        while (index < data.Length && char.IsAsciiDigit(data[index]))
        {
            index++;
            digits = true;
        }

        if (index < data.Length && data[index] == '.')
        {
            index++;
            while (index < data.Length && char.IsAsciiDigit(data[index]))
            {
                index++;
                digits = true;
            }
        }

        if (!digits)
        {
            index = start;
            value = 0;
            return false;
        }

        // 指数部（符号付き）
        if (index < data.Length && (data[index] == 'e' || data[index] == 'E'))
        {
            var exponentStart = index;
            index++;
            if (index < data.Length && (data[index] == '+' || data[index] == '-'))
            {
                index++;
            }

            if (index < data.Length && char.IsAsciiDigit(data[index]))
            {
                while (index < data.Length && char.IsAsciiDigit(data[index]))
                {
                    index++;
                }
            }
            else
            {
                index = exponentStart;
            }
        }

        value = double.Parse(data.AsSpan(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>図形の組み立て（解析中の中間状態）。</summary>
    /// <remarks>構造体にして、解析のたびに新しいリストを作らないようにする。</remarks>
    private sealed class SvgPathFigureBuilder
    {
        /// <summary>開始点。</summary>
        private double _startX;

        /// <summary>開始点。</summary>
        private double _startY;

        /// <summary>線分・曲線の列。</summary>
        private List<SvgPathSegment>? _segments;

        /// <summary>図形が閉じられているかどうか。</summary>
        private bool _isClosed;

        /// <summary>組み立て中の図形があるかどうか。</summary>
        public bool IsOpen => _segments is not null;

        /// <summary>図形を開始する。</summary>
        /// <param name="x">開始点の X 座標。</param>
        /// <param name="y">開始点の Y 座標。</param>
        /// <returns>開始した図形。</returns>
        public static SvgPathFigureBuilder Start(double x, double y)
            => new() { _startX = x, _startY = y, _segments = [] };

        /// <summary>線分を追加する。</summary>
        /// <param name="x">終点の X 座標。</param>
        /// <param name="y">終点の Y 座標。</param>
        public void AddLine(double x, double y)
            => _segments!.Add(SvgPathSegment.Line(x, y));

        /// <summary>3 次ベジェを追加する。</summary>
        /// <param name="c1x">第 1 制御点の X 座標。</param>
        /// <param name="c1y">第 1 制御点の Y 座標。</param>
        /// <param name="c2x">第 2 制御点の X 座標。</param>
        /// <param name="c2y">第 2 制御点の Y 座標。</param>
        /// <param name="x">終点の X 座標。</param>
        /// <param name="y">終点の Y 座標。</param>
        public void AddCubic(double c1x, double c1y, double c2x, double c2y, double x, double y)
            => _segments!.Add(SvgPathSegment.Cubic(c1x, c1y, c2x, c2y, x, y));

        /// <summary>図形を閉じる。</summary>
        public void Close() => _isClosed = true;

        /// <summary>図形を確定する。</summary>
        /// <returns>確定した図形。</returns>
        public SvgPathFigure Build()
            => new(_startX, _startY, _segments ?? [], _isClosed);
    }
}
