namespace EsUtil.Platform.Core.Geometry;

/// <summary>矩形の内側に取る 4 辺（上・下・左・右）の矩形を保持する値オブジェクト。</summary>
/// <remarks>
/// <see cref="EsRect.ResolveInnerEdges(double)"/> の結果を<b>ヒープ確保なし</b>で受け渡すための固定長コンテナである<br/>
/// （4 辺で 128 バイトであり、スタック上で返せる）。<br/>
/// <c>IReadOnlyList&lt;T&gt;</c> を返す実装は 1 回あたり 4 要素の配列（約 160 バイト）を確保するため、<br/>
/// 毎フレーム呼ばれる描画計画の解決では本型を使う。<br/>
/// <b>列挙順</b>: <see cref="Top"/> → <see cref="Bottom"/> → <see cref="Left"/> → <see cref="Right"/>。<br/>
/// <b>注意</b>: <c>IReadOnlyList&lt;T&gt;</c> を実装させないこと（値型をインターフェース経由で扱うとボックス化が発生する）。
/// </remarks>
/// <remarks>4 辺の矩形を指定して初期化する。</remarks>
/// <param name="Top">上辺の矩形（矩形の上端に置く、高さが太さ分の矩形）。</param>
/// <param name="Bottom">下辺の矩形（矩形の下端に置く、高さが太さ分の矩形）。</param>
/// <param name="Left">左辺の矩形（上下の辺の内側へ収まる、幅が太さ分の矩形）。</param>
/// <param name="Right">右辺の矩形（上下の辺の内側へ収まる、幅が太さ分の矩形）。</param>
public readonly record struct EsRectEdges(EsRect Top, EsRect Bottom, EsRect Left, EsRect Right)
{
    /// <summary>辺の本数（常に 4）。</summary>
    /// <value>4。</value>
    public const int Count = 4;

    /// <summary>列挙順（上・下・左・右）で辺の矩形を取得する。</summary>
    /// <param name="index">0〜3 の位置（0 = 上 / 1 = 下 / 2 = 左 / 3 = 右）。</param>
    /// <returns>指定位置の矩形。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> が 0〜3 の範囲外の場合。</exception>
    /// <remarks>配列を確保しないため、描画側の <c>for</c> ループから使う。</remarks>
    public EsRect this[int index] => index switch
    {
        0 => Top,
        1 => Bottom,
        2 => Left,
        3 => Right,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "辺の位置は 0〜3 である必要があります。"),
    };

    /// <summary>配列へ変換する（配列を必要とする既存 API へ渡す場合に使う）。</summary>
    /// <returns>上・下・左・右の順の矩形を格納した新しい配列。</returns>
    public EsRect[] ToArray() => [Top, Bottom, Left, Right];
}
