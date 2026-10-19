using System.Runtime.CompilerServices;

namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の矩形（左上座標＋サイズ）を表す値オブジェクト。</summary>
/// <remarks>
/// コアロジックは本型を使用し、UI 境界でのみ各 UI フレームワークの矩形型へ変換する（一方向依存）。<br/>
/// 各 UI 実装は、自身の矩形型との相互変換を Interop 層で提供する。<br/>
/// <br/>
/// <b>各フレームワークの対応</b>: <see cref="FromLTRB"/> は .NET 標準（<c>RectangleF.FromLTRB</c>）、<br/>
/// <see cref="Union(EsRect)"/> / <see cref="Union(EsPoint)"/> は WinUI3 と Avalonia、<see cref="Intersects"/> /<br/>
/// <see cref="Contains(EsRect)"/> / <see cref="CenterRect"/> / <see cref="Normalize"/> / <see cref="WithX"/> 系は Avalonia・MewUI、<br/>
/// <see cref="Offset(double,double)"/> / <see cref="Inflate(double,double)"/> は .NET 標準・MewUI に対応する。<br/>
/// <b>Contains の区間は閉区間</b>（左右端・上下端とも含む）であり、Avalonia と同じである。 MewUI・WinUI3 は右端・下端を含まないため、<br/>
/// その判定が必要な箇所は <see cref="ContainsExclusive"/> を使う（置き換え前の挙動を壊さないため既定は現行のまま）。<br/>
/// <b>Left / Top / Position</b> は <see cref="X"/> / <see cref="Y"/> / <see cref="TopLeft"/> と同値のため設けない（同名の別 API を増やさない）。<br/>
/// <b>Intersect の非交差時</b>は左上座標を保った空の矩形を返す（Avalonia は <c>default</c> を返すため意味が異なる）。<br/>
/// <b>区間の流儀（<see cref="Interval"/>）</b>: 幅・高さが終点を含むかどうかを値として持つ。<br/>
/// ピクセル数へ変換するときの <c>+1</c> の判断は本値が単一所有し、下流の層は <see cref="EsRectInterval"/> を再判定しない。<br/>
/// 既定は <see cref="EsRectInterval.HalfOpen"/>（幅 = ピクセル数）であり、従来の挙動を変えない。
/// </remarks>
/// <param name="X">左上 X 座標（呼び出し側の座標系に従う）。</param>
/// <param name="Y">左上 Y 座標（呼び出し側の座標系に従う）。</param>
/// <param name="Width">幅（0 以下は空の矩形を表す）。</param>
/// <param name="Height">高さ（0 以下は空の矩形を表す）。</param>
/// <param name="Interval">幅・高さが終点を含むかどうか（既定は <see cref="EsRectInterval.HalfOpen"/> = 幅がピクセル数）。</param>
public readonly record struct EsRect(double X, double Y, double Width, double Height, EsRectInterval Interval = EsRectInterval.HalfOpen)
{
    /// <summary>空の矩形（全て 0）を取得する。</summary>
    /// <value>左上が原点で幅・高さとも 0 の矩形。</value>
    public static readonly EsRect Empty;

    /// <summary>幅・高さが終点を含むかどうか（ピクセル数へ変換するときの <c>+1</c> の判断）を取得する。</summary>
    /// <value><see cref="EsRectInterval.HalfOpen"/>（幅 = ピクセル数。 既定）または <see cref="EsRectInterval.Closed"/>。</value>
    /// <remarks>閉区間への変換（<c>+1</c>）は利用側が本値を見て 1 箇所で行う（各所で再判定しない）。</remarks>
    public bool IsClosedInterval => Interval == EsRectInterval.Closed;

    /// <summary>右端の X 座標（左上 X + 幅）。</summary>
    /// <value>呼び出し側の座標系に従う。</value>
    public double Right => X + Width;

    /// <summary>下端の Y 座標（左上 Y + 高さ）。</summary>
    /// <value>呼び出し側の座標系に従う。</value>
    public double Bottom => Y + Height;

    /// <summary>幅または高さが 0 以下（切り出せる範囲・描画できる面積が無い）かどうかを取得する。</summary>
    /// <value>幅または高さが 0 以下の場合は true。</value>
    /// <remarks>
    /// 整数矩形の <see cref="EsRectInt.IsEmpty"/> と同じ判定を提供する<br/>
    /// （UI が <c>Width &gt; 0 &amp;&amp; Height &gt; 0</c> を書くと、同じ意味の判定が 0 以下の比較として散る）。
    /// </remarks>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>左上座標（<see cref="X"/> / <see cref="Y"/> と同値）を取得する。</summary>
    /// <value>左上の座標。</value>
    public EsPoint TopLeft => new(X, Y);

    /// <summary>右上座標を取得する。</summary>
    /// <value>右上の座標。</value>
    public EsPoint TopRight => new(Right, Y);

    /// <summary>左下座標を取得する。</summary>
    /// <value>左下の座標。</value>
    public EsPoint BottomLeft => new(X, Bottom);

    /// <summary>右下座標を取得する。</summary>
    /// <value>右下の座標。</value>
    public EsPoint BottomRight => new(Right, Bottom);

    /// <summary>矩形の中心座標を取得する。</summary>
    /// <value>中心の座標。</value>
    public EsPoint Center => new(X + Width / 2, Y + Height / 2);

    /// <summary>幅・高さを表すサイズを取得する。</summary>
    /// <value>幅・高さをそのまま引き継いだサイズ。</value>
    public EsSize Size => new(Width, Height);

    /// <summary>左・上・右・下の座標から矩形を生成する。</summary>
    /// <param name="left">左端の X 座標。</param>
    /// <param name="top">上端の Y 座標。</param>
    /// <param name="right">右端の X 座標。</param>
    /// <param name="bottom">下端の Y 座標。</param>
    /// <returns>指定の 4 辺を持つ矩形（幅・高さは右端 − 左端 / 下端 − 上端）。</returns>
    /// <remarks>右端が左端より小さい場合は幅が負の矩形になる（正規化は <see cref="Normalize"/> が単一所有する）。</remarks>
    public static EsRect FromLTRB(double left, double top, double right, double bottom)
        => new(left, top, right - left, bottom - top);

    /// <summary>2 つの対角点から矩形を生成する。</summary>
    /// <param name="topLeft">左上の座標。</param>
    /// <param name="bottomRight">右下の座標。</param>
    /// <returns>2 点を対角とする矩形（負の幅・高さになる場合は正規化しない）。</returns>
    public static EsRect FromPoints(EsPoint topLeft, EsPoint bottomRight)
        => FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);

    /// <summary>整数矩形から矩形を生成する。</summary>
    /// <param name="rect">変換元の整数矩形。</param>
    /// <returns>成分をそのまま引き継いだ矩形。</returns>
    public static EsRect FromRectInt(EsRectInt rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>区間の流儀を差し替えた矩形を返す。</summary>
    /// <param name="interval">新しい区間の流儀。</param>
    /// <returns>座標・幅・高さを引き継ぎ、区間の流儀だけを差し替えた矩形。</returns>
    /// <remarks>生成の 1 箇所（ドラッグ由来 = <see cref="EsRectInterval.Closed"/>、検出由来 = <see cref="EsRectInterval.HalfOpen"/>）が本メソッドを使う。</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRect WithInterval(EsRectInterval interval) => new(X, Y, Width, Height, interval);

    /// <summary>X 座標だけを差し替えた矩形を返す。</summary>
    /// <param name="x">新しい左上 X 座標。</param>
    /// <returns>他の成分を引き継いだ矩形。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRect WithX(double x) => new(x, Y, Width, Height, Interval);

    /// <summary>Y 座標だけを差し替えた矩形を返す。</summary>
    /// <param name="y">新しい左上 Y 座標。</param>
    /// <returns>他の成分を引き継いだ矩形。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRect WithY(double y) => new(X, y, Width, Height, Interval);

    /// <summary>幅だけを差し替えた矩形を返す。</summary>
    /// <param name="width">新しい幅。</param>
    /// <returns>他の成分を引き継いだ矩形。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRect WithWidth(double width) => new(X, Y, width, Height, Interval);

    /// <summary>高さだけを差し替えた矩形を返す。</summary>
    /// <param name="height">新しい高さ。</param>
    /// <returns>他の成分を引き継いだ矩形。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRect WithHeight(double height) => new(X, Y, Width, height, Interval);

    /// <summary>左上座標だけを差し替えた矩形を返す。</summary>
    /// <param name="position">新しい左上座標。</param>
    /// <returns>幅・高さを引き継いだ矩形。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRect WithPosition(EsPoint position) => new(position.X, position.Y, Width, Height, Interval);

    /// <summary>サイズだけを差し替えた矩形を返す。</summary>
    /// <param name="size">新しいサイズ。</param>
    /// <returns>左上座標を引き継いだ矩形。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRect WithSize(EsSize size) => new(X, Y, size.Width, size.Height, Interval);

    /// <summary>指定量だけ平行移動した矩形を返す。</summary>
    /// <param name="dx">X 方向の移動量。</param>
    /// <param name="dy">Y 方向の移動量。</param>
    /// <returns>移動後の矩形（幅・高さは変わらない）。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EsRect Offset(double dx, double dy) => new(X + dx, Y + dy, Width, Height, Interval);

    /// <summary>4 辺を指定量だけ広げた（狭めた）矩形を返す。</summary>
    /// <param name="dx">左右へ広げる量（負値は狭める）。</param>
    /// <param name="dy">上下へ広げる量（負値は狭める）。</param>
    /// <returns>左上を (−dx, −dy) だけ移動し、幅・高さを 2×dx / 2×dy だけ増減した矩形。</returns>
    public EsRect Inflate(double dx, double dy) => new(X - dx, Y - dy, Width + 2 * dx, Height + 2 * dy, Interval);

    /// <summary>4 辺を厚さ分だけ広げた矩形を返す。</summary>
    /// <param name="thickness">辺ごとに広げる厚さ（負値は狭める）。</param>
    /// <returns>左・上を厚さ分移動し、幅・高さを左右・上下の合計だけ増減した矩形。</returns>
    public EsRect Inflate(EsThickness thickness)
        => new(X - thickness.Left, Y - thickness.Top, Width + thickness.Horizontal, Height + thickness.Vertical, Interval);

    /// <summary>4 辺を厚さ分だけ狭めた矩形を返す。</summary>
    /// <param name="thickness">辺ごとに狭める厚さ（負値は広げる）。</param>
    /// <returns>左・上を厚さ分移動し、幅・高さを左右・上下の合計だけ減らした矩形（負のサイズもそのまま返す）。</returns>
    /// <remarks>負のサイズを 0 にクランプしない（クランプするかは用途で異なる。 必要な箇所は <see cref="Normalize"/> を使う）。</remarks>
    public EsRect Deflate(EsThickness thickness)
        => new(X + thickness.Left, Y + thickness.Top, Width - thickness.Horizontal, Height - thickness.Vertical, Interval);

    /// <summary>幅・高さが正になるように正規化した矩形を返す。</summary>
    /// <returns>幅・高さが正の矩形（左上は対角へ移動する）。 既に正の場合は同じ値。 非数を含む場合は空の矩形。</returns>
    /// <remarks>Avalonia の <c>Normalize</c> と同じ規則である（非数を含む矩形は空にする）。</remarks>
    public EsRect Normalize()
    {
        if (double.IsNaN(X) || double.IsNaN(Y) || double.IsNaN(Width) || double.IsNaN(Height)
            || double.IsNaN(Right) || double.IsNaN(Bottom))
        {
            return Empty;
        }

        var x = X;
        var y = Y;
        var width = Width;
        var height = Height;

        if (width < 0)
        {
            x += width;
            width = -width;
        }

        if (height < 0)
        {
            y += height;
            height = -height;
        }

        return new EsRect(x, y, width, height, Interval);
    }

    /// <summary>指定した点が本矩形の内側に含まれるかどうかを判定する。</summary>
    /// <param name="point">判定する点。</param>
    /// <returns>含まれる場合は true。</returns>
    /// <remarks>
    /// 左右端・上下端のいずれも含む（閉区間）。 Avalonia の矩形型と同じ判定であり、
    /// 置き換え前の挙動（ハンドルのヒットテスト等）を維持する。
    /// </remarks>
    public bool Contains(EsPoint point)
        => point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;

    /// <summary>指定した点が本矩形の内側に含まれるかどうかを判定する（右端・下端を含まない）。</summary>
    /// <param name="point">判定する点。</param>
    /// <returns>含まれる場合は true。</returns>
    /// <remarks>MewUI・WinUI3 の <c>Contains</c> と同じ半開区間の判定である（画素の所有権を重複させない用途）。</remarks>
    public bool ContainsExclusive(EsPoint point)
        => point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    /// <summary>本矩形が別の矩形を完全に含むかどうかを判定する（左右端・上下端を含む）。</summary>
    /// <param name="rect">判定する矩形。</param>
    /// <returns>完全に含む場合は true。</returns>
    public bool Contains(EsRect rect) => Contains(rect.TopLeft) && Contains(rect.BottomRight);

    /// <summary>本矩形と別の矩形が重なり合うかどうかを判定する。</summary>
    /// <param name="other">判定する相手の矩形。</param>
    /// <returns>重なりがある場合は true。</returns>
    /// <remarks>Avalonia の <c>Intersects</c>（MewUI・.NET 標準は <c>IntersectsWith</c>）と同じ判定である。</remarks>
    public bool Intersects(EsRect other)
        => other.X < Right && X < other.Right && other.Y < Bottom && Y < other.Bottom;

    /// <summary>2 つの矩形を包含する最小の矩形を返す（和集合）。</summary>
    /// <param name="other">もう一方の矩形。</param>
    /// <returns>両者を包含する矩形。 いずれかが空（幅・高さが 0 以下）の場合はもう一方を返す。</returns>
    /// <remarks>整数矩形の <see cref="EsRectInt.Union(EsRectInt)"/> と同じ規則である。</remarks>
    public EsRect Union(EsRect other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;

        var left = Math.Min(X, other.X);
        var top = Math.Min(Y, other.Y);
        var right = Math.Max(Right, other.Right);
        var bottom = Math.Max(Bottom, other.Bottom);
        return new EsRect(left, top, right - left, bottom - top, Interval);
    }

    /// <summary>点を含むように拡大した矩形を返す（和集合）。</summary>
    /// <param name="point">含める点。</param>
    /// <returns>点を含む最小の矩形。 本矩形が空の場合は点を左上とする面積 0 の矩形。</returns>
    /// <remarks>WinUI3 の <c>Rect.Union(Point)</c> と同じ操作である。</remarks>
    public EsRect Union(EsPoint point)
    {
        if (IsEmpty) return new EsRect(point.X, point.Y, 0, 0, Interval);

        var left = Math.Min(X, point.X);
        var top = Math.Min(Y, point.Y);
        var right = Math.Max(Right, point.X);
        var bottom = Math.Max(Bottom, point.Y);
        return new EsRect(left, top, right - left, bottom - top, Interval);
    }

    /// <summary>指定した矩形を本矩形の中央へ配置した矩形を返す。</summary>
    /// <param name="rect">中央へ配置する矩形。</param>
    /// <returns>本矩形の中央に置いた、<paramref name="rect"/> と同じサイズの矩形。</returns>
    /// <remarks>Avalonia の <c>CenterRect</c> と同じ規則である。 区間の流儀は <paramref name="rect"/> から引き継ぐ（サイズを引き継ぐため）。</remarks>
    public EsRect CenterRect(EsRect rect)
        => new(X + (Width - rect.Width) / 2, Y + (Height - rect.Height) / 2, rect.Width, rect.Height, rect.Interval);

    /// <summary>本矩形の内側に太さ分の余白を取った 4 辺の矩形を計算する（ヒープ確保なし）。</summary>
    /// <param name="thickness">辺の太さ（矩形の内側へ取る量）。 負値はそのまま扱う（下限の正規化は呼び出し側が行う）。</param>
    /// <returns>上・下・左・右の 4 辺の矩形。</returns>
    /// <remarks>
    /// 上辺は上端、下辺は下端から太さ分、左右の辺は上下の辺の内側へ収まる（角で二重に塗らない）。<br/>
    /// 太さの下限（0.5 未満を無効化する・1 未満を 1 にする等）は用途で異なるため本メソッドは正規化しない。<br/>
    /// 選択矩形の枠線とルーペの外枠が同じ式を共用する（各コントロールが書くと片方だけ端の扱いが変わる）。<br/>
    /// 配列を必要とする場合は <see cref="ComputeInnerEdges(double)"/> を使う。
    /// </remarks>
    public EsRectEdges ResolveInnerEdges(double thickness)
    {
        var inner = Math.Max(0, Height - thickness * 2);
        return new EsRectEdges(
            new EsRect(X, Y, Width, thickness),                             // 上
            new EsRect(X, Bottom - thickness, Width, thickness),            // 下
            new EsRect(X, Y + thickness, thickness, inner),                 // 左
            new EsRect(Right - thickness, Y + thickness, thickness, inner)); // 右
    }

    /// <summary>本矩形の内側に太さ分の余白を取った 4 辺の矩形を計算する。</summary>
    /// <param name="thickness">辺の太さ（矩形の内側へ取る量）。 負値はそのまま扱う（下限の正規化は呼び出し側が行う）。</param>
    /// <returns>上・下・左・右の順の辺の矩形（4 個）。 左右の辺の高さは負にならず 0 にクランプされる。</returns>
    /// <remarks>
    /// 計算は <see cref="ResolveInnerEdges(double)"/> が単一所有する（本メソッドは配列を必要とする既存 API との互換のための委譲であり、<br/>
    /// 1 回の呼び出しで 4 要素の配列を確保する）。 毎フレーム呼ばれる箇所は <see cref="ResolveInnerEdges(double)"/> を使う。
    /// </remarks>
    public IReadOnlyList<EsRect> ComputeInnerEdges(double thickness) => ResolveInnerEdges(thickness).ToArray();

    /// <summary>2 つの矩形がほぼ等しいかどうかを判定する。</summary>
    /// <param name="other">比較する矩形。</param>
    /// <returns>全ての成分が許容誤差の範囲で等しい場合は true。</returns>
    /// <remarks>
    /// 許容誤差の判定は <c>EsMath.AreClose</c> が単一所有する。<br/>
    /// 幾何だけを比較し、区間の流儀（<see cref="Interval"/>）は比較しない（流儀は形状ではなく解釈の指定である）。
    /// </remarks>
    public bool NearlyEquals(EsRect other)
        => EsMath.AreClose(X, other.X) && EsMath.AreClose(Y, other.Y)
            && EsMath.AreClose(Width, other.Width) && EsMath.AreClose(Height, other.Height);

    /// <summary>整数矩形へ変換する。</summary>
    /// <param name="rounding">丸め方法（既定 <see cref="EsRounding.Round"/>）。</param>
    /// <returns>指定の方法で丸めた整数矩形（X・Y・幅・高さを個別に丸める）。</returns>
    /// <remarks>四隅を丸めてから差を取る規則が必要な箇所は本メソッドを使わない（結果が 1 px ずれる）。</remarks>
    public EsRectInt ToEsRectInt(EsRounding rounding = EsRounding.Round) => EsRectInt.FromEsRect(this, rounding);

    /// <summary>タプルから生成する。</summary>
    /// <param name="value">(X, Y, Width, Height) 形式のタプル。</param>
    /// <returns>タプルの成分を引き継いだ矩形。</returns>
    public static EsRect FromTuple((double X, double Y, double Width, double Height) value)
        => new(value.X, value.Y, value.Width, value.Height);

    /// <summary>タプルへ変換する。</summary>
    /// <returns>(X, Y, Width, Height) 形式のタプル。</returns>
    public (double X, double Y, double Width, double Height) ToTuple() => (X, Y, Width, Height);

    /// <summary>幾何の 4 成分（X・Y・幅・高さ）へ分解する。</summary>
    /// <param name="x">左上 X 座標。</param>
    /// <param name="y">左上 Y 座標。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <remarks>
    /// 区間の流儀（<see cref="Interval"/>）は分解の対象に含めない（幾何と解釈を分けて受け取る）。<br/>
    /// 5 成分（流儀を含む）で受け取りたい場合は <see cref="Interval"/> を直接参照する。
    /// </remarks>
    public void Deconstruct(out double x, out double y, out double width, out double height)
    {
        x = X;
        y = Y;
        width = Width;
        height = Height;
    }

    /// <summary>本矩形を領域（左上を原点・指定サイズ）内へクランプする（四隅の包絡矩形）。</summary>
    /// <param name="area">クランプ先の領域のサイズ（左上を原点とする。 幅・高さは 0 以上）。</param>
    /// <returns>領域内へ収めた矩形。 負の辺（幅・高さ）は 0 になる。</returns>
    /// <remarks>
    /// 四隅を個別にクランプし、その包絡矩形（bounding box）を返す（矩形全体を領域内へ収める）。<br/>
    /// 「四隅をクランプして包絡矩形を取る」規則を単一所有するため、一部だけがはみ出す場合も同じ手順で収まる。<br/>
    /// 領域の幅・高さが負の場合は <c>Math.Clamp</c> の前提に反する（呼び出し側が 0 以上を保証する）。
    /// </remarks>
    public EsRect ClampToArea(EsSize area)
    {
        var left = Math.Clamp(X, 0, area.Width);
        var top = Math.Clamp(Y, 0, area.Height);
        var right = Math.Clamp(Right, 0, area.Width);
        var bottom = Math.Clamp(Bottom, 0, area.Height);
        return new EsRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top), Interval);
    }

    /// <summary>本矩形と別の矩形の交差（重なり合う部分）を求める。</summary>
    /// <param name="other">交差を求める相手の矩形。</param>
    /// <returns>2 つの矩形の重なり部分。 重なりが無い場合は空の矩形。</returns>
    /// <remarks>
    /// 「重なりが無い場合は空」を単一所有するため、呼び出し側は <see cref="IsEmpty"/> で判定できる<br/>
    /// （<c>Math.Max(0, ...)</c> による幅・高さの切り下げを各所へ書かせない）。
    /// </remarks>
    public EsRect Intersect(EsRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return new EsRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top), Interval);
    }
}
