using System.Collections.Immutable;
using System.Text.Json.Serialization;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>画像に重ねる注釈の集合（JSON ファイルの内容）。</summary>
/// <remarks>
/// <b>並び順が重なり順</b>である（先頭が最背面、末尾が最前面）。<br/>
/// <b>画像の寸法を持つ</b>: 注釈の座標は元画像ピクセルであり、別の大きさの画像へ読み込むと位置がずれる。
/// 読み込み時に寸法を照合し、違えば警告する（<see cref="AnnotationJson"/>）。<br/>
/// <b>画像そのものは持たない</b>（注釈は画像に焼き込まない。 保存時に合成した画像を別ファイルへ書く）。
/// </remarks>
public sealed class AnnotationDocument
{
    /// <summary>現在の JSON スキーマの版を取得する。</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>スキーマの版を取得または設定する。</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    public int ImageWidth { get; set; }

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    public int ImageHeight { get; set; }

    /// <summary>元画像のファイル名を取得または設定する（同じフォルダからの再編集の手掛かり）。</summary>
    public string? ImageFileName { get; set; }

    /// <summary>注釈の並び（先頭が最背面、末尾が最前面）を取得または設定する。</summary>
    public ImmutableArray<AnnotationItem> Items { get; set; } = [];

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
    /// <returns>追加後の文書（元の文書は変更しない）。</returns>
    public AnnotationDocument Add(AnnotationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return WithItems(Items.Add(item));
    }

    /// <summary>指定された識別子の注釈を差し替えた新しい文書を返す。</summary>
    /// <param name="item">差し替える注釈（<see cref="AnnotationItem.Id"/> で位置を決める）。</param>
    /// <returns>差し替え後の文書（見つからない場合は元の文書と同じ内容）。</returns>
    public AnnotationDocument Replace(AnnotationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = IndexOf(item.Id);
        return index < 0 ? this : WithItems(Items.SetItem(index, item));
    }

    /// <summary>指定された識別子の注釈を除いた新しい文書を返す。</summary>
    /// <param name="id">除く注釈の識別子。</param>
    /// <returns>削除後の文書（見つからない場合は元の文書と同じ内容）。</returns>
    public AnnotationDocument Remove(string id)
    {
        var index = IndexOf(id);
        return index < 0 ? this : WithItems(Items.RemoveAt(index));
    }

    /// <summary>指定された識別子の注釈を取得する。</summary>
    /// <param name="id">注釈の識別子。</param>
    /// <returns>見つかった注釈。 無い場合は <see langword="null"/>。</returns>
    public AnnotationItem? Find(string? id)
        => string.IsNullOrEmpty(id) ? null : Items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));

    /// <summary>指定された識別子の注釈の矩形を、画像の内側へ収めたうえで変更する。</summary>
    /// <param name="id">注釈の識別子。</param>
    /// <param name="bounds">新しい矩形（元画像ピクセル。 画像の外へ出た分は収める）。</param>
    /// <returns>変更した場合は <see langword="true"/>（見つからない・同じ値の場合は <see langword="false"/>）。</returns>
    /// <remarks>
    /// <b>矩形を変更できる唯一の入口</b>である（<see cref="AnnotationItem.Bounds"/> の設定子は Core の内部）。
    /// 収める規則は <see cref="AnnotationGeometry.ClampRect"/> が単一所有する。
    /// </remarks>
    public bool SetBounds(string id, EsRect bounds)
    {
        if (Find(id) is not { } item)
        {
            return false;
        }

        var clamped = AnnotationGeometry.ClampRect(bounds, ImageSize);
        if (item.Bounds.NearlyEquals(clamped))
        {
            return false;
        }

        var previous = item.Bounds;
        item.Bounds = clamped;

        // **しっぽは本体に付いて動く**: 本体だけ動かすと、しっぽが元の位置に取り残されて
        // 本体から離れた細い三角形になる（実測 2026-11-21）。 しっぽの規則（収める）は
        // SetTail が単一所有するため、移動量を足してから同じ入口へ渡す。
        if (item is BubbleAnnotation bubble)
        {
            var dx = clamped.X - previous.X;
            var dy = clamped.Y - previous.Y;
            if (dx != 0 || dy != 0)
            {
                var tip = new EsPoint(bubble.Tail.Tip.X + dx, bubble.Tail.Tip.Y + dy);
                SetTail(bubble.Id, bubble.Tail with { Tip = tip });
            }
        }

        return true;
    }

    /// <summary>指定された吹き出しのしっぽの先端を、画像の内側へ収めたうえで変更する。</summary>
    /// <param name="id">吹き出しの識別子。</param>
    /// <param name="tip">新しい先端（元画像ピクセル。 画像の外へ出た分は収める）。</param>
    /// <returns>変更した場合は <see langword="true"/>（見つからない・吹き出しでない・同じ値の場合は <see langword="false"/>）。</returns>
    /// <remarks>
    /// <b>しっぽの先端を変更できる唯一の入口</b>である（<see cref="BubbleAnnotation.Tail"/> の設定子は Core の内部）。
    /// 収める規則は <see cref="AnnotationGeometry.ClampTailTip"/> が単一所有する。
    /// </remarks>
    public bool SetTailTip(string id, EsPoint tip)
    {
        if (Find(id) is not BubbleAnnotation bubble)
        {
            return false;
        }

        return SetTail(id, bubble.Tail with { Tip = tip });
    }

    /// <summary>指定された吹き出しのしっぽを、先端を画像の内側へ収めたうえで変更する。</summary>
    /// <param name="id">吹き出しの識別子。</param>
    /// <param name="tail">新しいしっぽ（先端は画像の外へ出た分を収める）。</param>
    /// <returns>変更した場合は <see langword="true"/>（見つからない・吹き出しでない・同じ値の場合は <see langword="false"/>）。</returns>
    /// <remarks>
    /// <b>しっぽを変更できる唯一の入口</b>である（幅・辺・根本の位置を変える場合もここを通す）。
    /// </remarks>
    public bool SetTail(string id, BubbleTail tail)
    {
        if (Find(id) is not BubbleAnnotation bubble)
        {
            return false;
        }

        var clamped = tail with { Tip = AnnotationGeometry.ClampTailTip(tail.Tip, ImageSize) };
        if (bubble.Tail == clamped)
        {
            return false;
        }

        bubble.Tail = clamped;
        return true;
    }

    /// <summary>本文の書式を指定された範囲で差し替えた新しい文書を返す（<see cref="BubbleAnnotation"/> だけが対象）。</summary>
    /// <param name="bubble">対象の吹き出し（現在の文書に含まれるもの）。</param>
    /// <param name="text">差し替える本文。</param>
    /// <returns>差し替え後の文書。</returns>
    /// <remarks>本文の編集は吹き出しの一部を置き換えるだけであり、位置・外観・しっぽは変えない。</remarks>
    public AnnotationDocument WithText(BubbleAnnotation bubble, RichText text)
    {
        ArgumentNullException.ThrowIfNull(bubble);
        return Replace(new BubbleAnnotation
        {
            Id = bubble.Id,
            Bounds = bubble.Bounds,
            Appearance = bubble.Appearance,
            BodyPadding = bubble.BodyPadding,
            Tail = bubble.Tail,
            Text = text,
        });
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
    /// <returns>移動後の文書（見つからない場合は元の文書と同じ内容）。</returns>
    public AnnotationDocument BringToFront(string id)
    {
        var index = IndexOf(id);
        return index < 0 || index == Items.Length - 1
            ? this
            : WithItems(Items.RemoveAt(index).Add(Items[index]));
    }

    /// <summary>指定された注釈を最背面へ移した新しい文書を返す。</summary>
    /// <param name="id">対象の注釈の識別子。</param>
    /// <returns>移動後の文書（見つからない場合は元の文書と同じ内容）。</returns>
    public AnnotationDocument SendToBack(string id)
    {
        var index = IndexOf(id);
        return index <= 0 ? this : WithItems(Items.RemoveAt(index).Insert(0, Items[index]));
    }

    /// <summary>注釈の並びだけを差し替えた複製を返す。</summary>
    /// <param name="items">新しい並び。</param>
    /// <returns>複製。</returns>
    private AnnotationDocument WithItems(ImmutableArray<AnnotationItem> items) => new(ImageWidth, ImageHeight, ImageFileName)
    {
        SchemaVersion = SchemaVersion,
        Items = items,
    };

    /// <summary>同じ内容の複製を作る（注釈も複製する）。</summary>
    /// <returns>複製。 変更履歴の記録に使う。</returns>
    public AnnotationDocument Clone()
        => new(ImageWidth, ImageHeight, ImageFileName)
        {
            SchemaVersion = SchemaVersion,
            Items = [.. Items.Select(item => item.Clone())],
        };
}
