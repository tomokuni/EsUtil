using System.Collections.Immutable;

namespace EsUtil.Feature.Annotation;

/// <summary>書式付きの文字列（<see cref="TextRun"/> の並び）。</summary>
/// <remarks>
/// <b>1 文字だけ書式を変えられる</b>（run を分割して書式を変える）。 空の run は描画されない。<br/>
/// <b>改行は <c>\n</c> で表す</b>（吹き出し内の明示改行。 折り返しは <see cref="AnnotationPresenter"/> が行う）。
/// </remarks>
public sealed class RichText
{
    /// <summary>文字を持たない <see cref="RichText"/> を取得する。</summary>
    public static RichText Empty { get; } = new([]);

    /// <summary>空の文字列で初期化する。</summary>
    public RichText()
    {
        Runs = [];
    }

    /// <summary>run の並びで初期化する。</summary>
    /// <param name="runs">run の並び。</param>
    public RichText(ImmutableArray<TextRun> runs)
    {
        Runs = runs.IsDefault ? [] : runs;
    }

    /// <summary>run の並びを取得または設定する。</summary>
    public ImmutableArray<TextRun> Runs { get; set; }

    /// <summary>描く文字を持たないかどうかを取得する。</summary>
    /// <value>すべての run が空の場合は <see langword="true"/>。</value>
    public bool IsEmpty
    {
        get
        {
            foreach (var run in Runs)
            {
                if (!string.IsNullOrEmpty(run.Text))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>書式を除いた文字列を取得する。</summary>
    /// <value>run を連結した文字列。</value>
    public string PlainText => string.Concat(Runs.Select(run => run.Text));

    /// <summary>単一の書式の文字列から生成する。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>1 つの run からなる <see cref="RichText"/>。</returns>
    public static RichText Create(string text, AnnotationTextStyle style)
        => string.IsNullOrEmpty(text) ? Empty : new RichText([new TextRun(text, style)]);

    /// <summary>指定された範囲の書式を差し替えた新しい <see cref="RichText"/> を返す（文字単位の書式変更）。</summary>
    /// <param name="start">範囲の開始位置（0 起点。 文字数で数える）。</param>
    /// <param name="length">範囲の長さ（文字数。 0 以下は「全体」）。</param>
    /// <param name="style">適用する書式。</param>
    /// <returns>書式を差し替えた新しい値（元の値は変更しない）。</returns>
    /// <remarks>
    /// <b>run を境界で分割する</b>: 1 つの run の途中だけ書式を変える場合は、その位置で run を 2 つ（または 3 つ）に分ける。
    /// 挿入・削除で境界がずれないよう、run が自分の文字列を持つ設計と対になっている。<br/>
    /// 範囲が文字数を超える場合は末尾までに丸める（例外にしない）。
    /// </remarks>
    public RichText ApplyStyle(int start, int length, AnnotationTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return WriteStyle(start, length, _ => style);
    }

    /// <summary>指定された範囲の書式を 1 文字ずつ変換した新しい <see cref="RichText"/> を返す。</summary>
    /// <param name="start">範囲の開始位置（0 起点。 文字数で数える）。</param>
    /// <param name="length">範囲の長さ（文字数。 0 以下は「全体」）。</param>
    /// <param name="transform">現在の書式を受け取って新しい書式を返す関数（run ごとに呼ぶ）。</param>
    /// <returns>書式を変換した新しい値（元の値は変更しない）。</returns>
    /// <remarks>
    /// <b>変えない属性を保つ</b>: 「太字にする」「色だけ変える」のように 1 つの属性だけを変えるときは
    /// <see cref="ApplyStyle"/> ではなく本メソッドを使う（範囲内の run ごとに他の属性を残せる）。<br/>
    /// <b>切り替えの判断は呼び出し側で 1 回だけ行う</b>: 範囲の先頭の書式で「太字にする／戻す」を決め、
    /// その値を transform で全 run へ書く（run ごとに反転すると範囲の中で太字と通常が入れ替わる）。
    /// </remarks>
    public RichText TransformStyle(int start, int length, Func<AnnotationTextStyle, AnnotationTextStyle> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return WriteStyle(start, length, transform);
    }

    /// <summary>指定された範囲の書式を書き換えた新しい <see cref="RichText"/> を返す。</summary>
    /// <param name="start">範囲の開始位置（0 起点）。</param>
    /// <param name="length">範囲の長さ（0 以下は全体）。</param>
    /// <param name="styleFor">現在の書式から新しい書式を作る関数。</param>
    /// <returns>書き換えた新しい値。</returns>
    private RichText WriteStyle(int start, int length, Func<AnnotationTextStyle, AnnotationTextStyle> styleFor)
    {
        if (IsEmpty)
        {
            return this;
        }

        var total = PlainText.Length;
        var from = Math.Clamp(start, 0, total);
        var to = length <= 0 ? total : Math.Clamp(from + length, from, total);
        if (from == to)
        {
            return this;
        }

        var runs = ImmutableArray.CreateBuilder<TextRun>();
        var position = 0;
        foreach (var run in Runs)
        {
            var runStart = position;
            var runEnd = position + run.Text.Length;
            position = runEnd;

            if (runEnd <= from || runStart >= to)
            {
                AddMerged(runs, run);
                continue;
            }

            // 範囲の手前（書式を変えない部分）
            var localFrom = Math.Max(from, runStart) - runStart;
            var localTo = Math.Min(to, runEnd) - runStart;
            if (localFrom > 0)
            {
                AddMerged(runs, run with { Text = run.Text[..localFrom] });
            }

            AddMerged(runs, new TextRun(run.Text[localFrom..localTo], styleFor(run.Style)));

            if (localTo < run.Text.Length)
            {
                AddMerged(runs, run with { Text = run.Text[localTo..] });
            }
        }

        return new RichText(runs.ToImmutable());
    }

    /// <summary>書式を位置で引き継いで本文を置き換えた新しい <see cref="RichText"/> を返す。</summary>
    /// <param name="text">新しい文字列（明示改行は <c>\n</c>）。</param>
    /// <returns>書式を引き継いだ文字列。</returns>
    /// <remarks>
    /// <b>書式は文字位置で引き継ぐ</b>: 元の文字列と同じ範囲は元の run の書式を使い、増えた部分は末尾の書式にする
    /// （編集のたびにすべての書式が消えないようにする）。 書式そのものを変えるときは <see cref="ApplyStyle"/> を使う。
    /// </remarks>
    public RichText WithPlainText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return Empty;
        }

        var previous = PlainText;
        var last = Math.Max(0, previous.Length - 1);
        var runs = ImmutableArray.CreateBuilder<TextRun>();

        for (var i = 0; i < text.Length; i++)
        {
            var style = StyleAt(Math.Min(i, last));
            if (runs.Count > 0 && Equals(runs[^1].Style, style))
            {
                runs[^1] = runs[^1] with { Text = runs[^1].Text + text[i] };
                continue;
            }

            runs.Add(new TextRun(text[i].ToString(), style));
        }

        return new RichText(runs.ToImmutable());
    }

    /// <summary>指定位置の文字の書式を取得する。</summary>
    /// <param name="index">文字の位置（0 起点）。</param>
    /// <returns>その文字の書式。 範囲外・空の場合は <see cref="AnnotationTextStyle.Default"/>。</returns>
    /// <remarks>書式ツールバーの初期表示（選択位置の現在値）に使う。</remarks>
    public AnnotationTextStyle StyleAt(int index)
    {
        var position = 0;
        foreach (var run in Runs)
        {
            position += run.Text.Length;
            if (index < position)
            {
                return run.Style;
            }
        }

        return Runs.LastOrDefault()?.Style ?? AnnotationTextStyle.Default;
    }

    /// <summary>指定された範囲の文字がすべて条件を満たすかどうかを返す。</summary>
    /// <param name="start">範囲の開始位置（0 起点。 文字数で数える）。</param>
    /// <param name="length">範囲の長さ（文字数。 0 以下は「全体」）。</param>
    /// <param name="match">書式が条件を満たすかどうかを返す関数。</param>
    /// <returns>範囲のすべての文字が条件を満たす場合は <see langword="true"/>。 範囲に文字が無い場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// <b>「範囲のすべて」で判定する</b>: 書式ツールバーの押下状態は、範囲の先頭の文字ではなく
    /// <b>範囲のすべてが同じ属性か</b>で決める（一部だけ太字の範囲でボタンが押されて見えるのを防ぐ）。
    /// </remarks>
    public bool AllMatch(int start, int length, Func<AnnotationTextStyle, bool> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var total = PlainText.Length;
        var from = Math.Clamp(start, 0, total);
        var to = length <= 0 ? total : Math.Clamp(from + length, from, total);
        if (from >= to)
        {
            return false;
        }

        var position = 0;
        foreach (var run in Runs)
        {
            var runStart = position;
            var runEnd = position + run.Text.Length;
            position = runEnd;

            if (runEnd <= from || runStart >= to)
            {
                continue;
            }

            if (!match(run.Style))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>指定位置へ文字列を挿入した新しい <see cref="RichText"/> を返す（本文の入力に使う）。</summary>
    /// <param name="index">挿入位置（0 起点。 文字数で数える）。</param>
    /// <param name="text">挿入する文字列（明示改行は <c>\n</c>）。</param>
    /// <returns>挿入後の新しい値（元の値は変更しない）。</returns>
    /// <remarks>
    /// <b>挿入する文字は位置の書式を引き継ぐ</b>（一般的な入力欄と同じ。 太字の途中へ入力すると太字になる）。
    /// 位置が範囲外の場合は先頭・末尾へ丸める（例外にしない）。
    /// </remarks>
    public RichText Insert(int index, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return this;
        }

        var plain = PlainText;
        var at = Math.Clamp(index, 0, plain.Length);

        // 挿入する文字は「直前の文字」の書式を引き継ぐ（文末で入力しても直前の書式が続く）
        var style = StyleAt(at > 0 ? at - 1 : 0);

        var runs = ImmutableArray.CreateBuilder<TextRun>();
        AddRange(runs, plain, 0, at);
        AddMerged(runs, new TextRun(text, style));
        AddRange(runs, plain, at, plain.Length);
        return new RichText(runs.ToImmutable());
    }

    /// <summary>指定範囲の文字を除いた新しい <see cref="RichText"/> を返す（本文の削除に使う）。</summary>
    /// <param name="start">範囲の開始位置（0 起点）。</param>
    /// <param name="length">範囲の長さ（文字数）。</param>
    /// <returns>削除後の新しい値（元の値は変更しない）。</returns>
    /// <remarks>範囲が文字数を超える場合は末尾までに丸める（例外にしない）。</remarks>
    public RichText Remove(int start, int length)
    {
        if (length <= 0)
        {
            return this;
        }

        var plain = PlainText;
        var from = Math.Clamp(start, 0, plain.Length);
        var to = Math.Clamp(from + length, from, plain.Length);
        if (from == to)
        {
            return this;
        }

        var runs = ImmutableArray.CreateBuilder<TextRun>();
        AddRange(runs, plain, 0, from);
        AddRange(runs, plain, to, plain.Length);
        return new RichText(runs.ToImmutable());
    }

    /// <summary>文字列の範囲を、位置ごとの書式で run へ追加する。</summary>
    /// <param name="runs">追加先。</param>
    /// <param name="plain">元の文字列。</param>
    /// <param name="from">開始位置（含む）。</param>
    /// <param name="to">終了位置（含まない）。</param>
    private void AddRange(ImmutableArray<TextRun>.Builder runs, string plain, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            AddMerged(runs, new TextRun(plain[i].ToString(), StyleAt(i)));
        }
    }

    /// <summary>末尾の run と同じ書式なら連結して追加する（run が細かく分かれないようにする）。</summary>
    /// <param name="runs">追加先。</param>
    /// <param name="run">追加する run。</param>
    private static void AddMerged(ImmutableArray<TextRun>.Builder runs, TextRun run)
    {
        if (string.IsNullOrEmpty(run.Text))
        {
            return;
        }

        if (runs.Count > 0 && Equals(runs[^1].Style, run.Style))
        {
            runs[^1] = runs[^1] with { Text = runs[^1].Text + run.Text };
            return;
        }

        runs.Add(run);
    }
}
