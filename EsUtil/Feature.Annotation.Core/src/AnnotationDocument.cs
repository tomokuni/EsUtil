using System.Collections.Immutable;
using System.Text.Json.Serialization;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>画像に重ねる注釈の集合（JSON ファイルの内容）。</summary>
/// <remarks>
/// <b>並び順が重なり順</b>である（先頭が最背面、末尾が最前面）。<br/>
/// <b>画像の寸法を持つ</b>: 注釈の座標は元画像ピクセルであり、別の大きさの画像へ読み込むと位置がずれる。<br/>
/// <b>画像そのものは持たない</b>（注釈は画像に焼き込まない。 保存時に合成した画像を別ファイルへ書く）。<br/>
/// <b>不変である</b>: 変更は <c>With...</c> が新しい文書を返す（変化が無い場合は同じ実体を返す）。
/// 変更履歴・描画中の値・保存した値が同じ実体を指して書き換わる事故を型で防ぐ。
/// </remarks>
public sealed record AnnotationDocument
{
    /// <summary>現在の JSON スキーマの版を取得する。</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>スキーマの版を取得する。</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>元画像の幅（ピクセル）を取得する。</summary>
    public int ImageWidth { get; init; }

    /// <summary>元画像の高さ（ピクセル）を取得する。</summary>
    public int ImageHeight { get; init; }

    /// <summary>元画像のファイル名を取得する（同じフォルダからの再編集の手掛かり）。</summary>
    public string? ImageFileName { get; init; }

    /// <summary>注釈の並び（先頭が最背面、末尾が最前面）を取得する。</summary>
    public ImmutableArray<AnnotationItem> Items { get; init; } = [];

    /// <summary>対象の画像の大きさ（1 未満は 1 として扱う）を取得する。</summary>
    /// <value>元画像ピクセルの大きさ。</value>
    /// <remarks>幅と高さから導く値のため JSON には書かない（保存形式は <see cref="ImageWidth"/> / <see cref="ImageHeight"/> が単一所有する）。</remarks>
    [JsonIgnore]
    public EsSize ImageSize => AnnotationGeometry.ResolveImageSize(this);

    /// <summary>画像の寸法を指定して初期化する。</summary>
    /// <param name="imageWidth">元画像の幅（ピクセル）。</param>
    /// <param name="imageHeight">元画像の高さ（ピクセル）。</param>
    /// <param name="imageFileName">元画像のファイル名。</param>
    public AnnotationDocument(int imageWidth, int imageHeight, string? imageFileName = null)
    {
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        ImageFileName = imageFileName;
    }

    /// <summary>空の注釈集合で初期化する（JSON の読み込み用）。</summary>
    public AnnotationDocument()
    {
    }

    /// <summary>注釈を最前面へ追加した新しい文書を返す。</summary>
    /// <param name="item">追加する注釈。</param>
    /// <returns>追加後の文書。</returns>
    public AnnotationDocument Add(AnnotationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return this with { Items = Items.Add(item) };
    }

    /// <summary>指定された識別子の注釈を差し替えた新しい文書を返す。</summary>
    /// <param name="item">差し替える注釈（<see cref="AnnotationItem.Id"/> で位置を決める）。</param>
    /// <returns>差し替え後の文書（見つからない場合は同じ実体）。</returns>
    public AnnotationDocument Replace(AnnotationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = IndexOf(item.Id);
        return index < 0 ? this : this with { Items = Items.SetItem(index, item) };
    }

    /// <summary>指定された識別子の注釈を除いた新しい文書を返す。</summary>
    /// <param name="id">除く注釈の識別子。</param>
    /// <returns>削除後の文書（見つからない場合は同じ実体）。</returns>
    public AnnotationDocument Remove(string id)
    {
        var index = IndexOf(id);
        return index < 0 ? this : this with { Items = Items.RemoveAt(index) };
    }

    /// <summary>指定された識別子の注釈を取得する。</summary>
    /// <param name="id">注釈の識別子。</param>
    /// <returns>見つかった注釈。 無い場合は <see langword="null"/>。</returns>
    public AnnotationItem? Find(string? id)
        => string.IsNullOrEmpty(id) ? null : Items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));

    /// <summary>指定された識別子の注釈の矩形を、画像の内側へ収めたうえで差し替えた文書を返す。</summary>
    /// <param name="id">注釈の識別子。</param>
    /// <param name="bounds">新しい矩形（元画像ピクセル。 画像の外へ出た分は収める）。</param>
    /// <returns>差し替えた文書（見つからない・同じ値の場合は同じ実体）。</returns>
    /// <remarks>
    /// <b>矩形を変更できる唯一の入口</b>である。 収める規則は <see cref="AnnotationGeometry.ClampRect"/> が、
    /// しっぽの追従は <see cref="WithTail"/> が単一所有する（本体だけ動かしてしっぽを取り残さない）。
    /// </remarks>
    public AnnotationDocument WithBounds(string id, EsRect bounds)
    {
        if (Find(id) is not { } item)
        {
            return this;
        }

        var clamped = AnnotationGeometry.ClampRect(bounds, ImageSize);
        if (item.Bounds.NearlyEquals(clamped))
        {
            return this;
        }

        var moved = item with { Bounds = clamped };
        if (moved is BubbleAnnotation bubble)
        {
            // **しっぽは本体に付いて動く**: 本体だけ動かすと、しっぽが元の位置に取り残されて
            // 本体から離れた細い三角形になる。 収める規則は ClampTail が単一所有する。
            var dx = clamped.X - item.Bounds.X;
            var dy = clamped.Y - item.Bounds.Y;
            var tip = new EsPoint(bubble.Tail.Tip.X + dx, bubble.Tail.Tip.Y + dy);
            moved = bubble with { Tail = ClampTail(bubble.Tail with { Tip = tip }) };
        }

        return Replace(moved);
    }

    /// <summary>指定された吹き出しのしっぽの先端を、画像の内側へ収めたうえで差し替えた文書を返す。</summary>
    /// <param name="id">吹き出しの識別子。</param>
    /// <param name="tip">新しい先端（元画像ピクセル。 画像の外へ出た分は収める）。</param>
    /// <returns>差し替えた文書（見つからない・吹き出しでない・同じ値の場合は同じ実体）。</returns>
    public AnnotationDocument WithTailTip(string id, EsPoint tip)
        => Find(id) is BubbleAnnotation bubble ? WithTailCore(bubble, bubble.Tail with { Tip = tip }) : this;

    /// <summary>指定された吹き出しのしっぽを、先端を画像の内側へ収めたうえで差し替えた文書を返す。</summary>
    /// <param name="id">吹き出しの識別子。</param>
    /// <param name="tail">新しいしっぽ（先端は画像の外へ出た分を収める）。</param>
    /// <returns>差し替えた文書（見つからない・吹き出しでない・同じ値の場合は同じ実体）。</returns>
    /// <remarks>
    /// <b>しっぽを変更できる唯一の入口</b>である（幅・辺・根本の位置を変える場合もここを通す）。
    /// </remarks>
    public AnnotationDocument WithTail(string id, BubbleTail tail)
        => Find(id) is BubbleAnnotation bubble ? WithTailCore(bubble, tail) : this;

    /// <summary>指定された注釈の外観を、安全域へ補正したうえで差し替えた文書を返す。</summary>
    /// <param name="id">注釈の識別子。</param>
    /// <param name="appearance">新しい外観（枠線の太さ・角丸は安全域へ補正する）。</param>
    /// <returns>差し替えた文書（見つからない・同じ値の場合は同じ実体）。</returns>
    public AnnotationDocument WithAppearance(string id, AnnotationAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        if (Find(id) is not { } item)
        {
            return this;
        }

        var normalized = appearance.Normalized();
        return item.Appearance == normalized ? this : Replace(item with { Appearance = normalized });
    }

    /// <summary>指定された吹き出しの本文の余白を、安全域へ補正したうえで差し替えた文書を返す。</summary>
    /// <param name="id">吹き出しの識別子。</param>
    /// <param name="padding">新しい余白（元画像ピクセル。 0〜上限へ補正する）。</param>
    /// <returns>差し替えた文書（見つからない・吹き出しでない・同じ値の場合は同じ実体）。</returns>
    public AnnotationDocument WithBodyPadding(string id, double padding)
    {
        if (Find(id) is not BubbleAnnotation bubble)
        {
            return this;
        }

        var coerced = AnnotationLimits.CoerceBodyPadding(padding);
        return bubble.BodyPadding.Equals(coerced) ? this : Replace(bubble with { BodyPadding = coerced });
    }

    /// <summary>吹き出しの本文を差し替えた文書を返す（<see cref="BubbleAnnotation"/> だけが対象）。</summary>
    /// <param name="bubbleId">吹き出しの識別子。</param>
    /// <param name="text">差し替える本文。</param>
    /// <returns>差し替えた文書（見つからない・同じ文字列の場合は同じ実体）。</returns>
    /// <remarks>本文の編集は吹き出しの一部を置き換えるだけであり、位置・外観・しっぽは変えない。</remarks>
    public AnnotationDocument WithText(string bubbleId, RichText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (Find(bubbleId) is not BubbleAnnotation bubble)
        {
            return this;
        }

        return bubble.Text.Runs.SequenceEqual(text.Runs) ? this : Replace(bubble with { Text = text });
    }

    /// <summary>指定された識別子の位置（重なり順）を取得する。</summary>
    /// <param name="id">注釈の識別子。</param>
    /// <returns>位置（先頭が 0）。 無い場合は -1。</returns>
    public int IndexOf(string id)
    {
        for (var i = 0; i < Items.Length; i++)
        {
            if (string.Equals(Items[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>指定された注釈を最前面へ移した新しい文書を返す。</summary>
    /// <param name="id">対象の注釈の識別子。</param>
    /// <returns>移動後の文書（見つからない・既に最前面の場合は同じ実体）。</returns>
    public AnnotationDocument BringToFront(string id)
    {
        var index = IndexOf(id);
        return index < 0 || index == Items.Length - 1
            ? this
            : this with { Items = Items.RemoveAt(index).Add(Items[index]) };
    }

    /// <summary>指定された注釈を最背面へ移した新しい文書を返す。</summary>
    /// <param name="id">対象の注釈の識別子。</param>
    /// <returns>移動後の文書（見つからない・既に最背面の場合は同じ実体）。</returns>
    public AnnotationDocument SendToBack(string id)
    {
        var index = IndexOf(id);
        return index <= 0 ? this : this with { Items = Items.RemoveAt(index).Insert(0, Items[index]) };
    }

    /// <summary>しっぽを安全域へ補正して差し替えた文書を返す。</summary>
    /// <param name="bubble">対象の吹き出し。</param>
    /// <param name="tail">新しいしっぽ。</param>
    /// <returns>差し替えた文書（同じ値の場合は同じ実体）。</returns>
    private AnnotationDocument WithTailCore(BubbleAnnotation bubble, BubbleTail tail)
    {
        var clamped = ClampTail(tail);
        return bubble.Tail == clamped ? this : Replace(bubble with { Tail = clamped });
    }

    /// <summary>しっぽを安全域へ補正する（幅・付け根の比率・先端の位置）。</summary>
    /// <param name="tail">補正するしっぽ。</param>
    /// <returns>補正したしっぽ。</returns>
    private BubbleTail ClampTail(BubbleTail tail)
    {
        var normalized = tail.Normalized();
        return normalized with { Tip = AnnotationGeometry.ClampTailTip(normalized.Tip, ImageSize) };
    }
}
