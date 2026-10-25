using System.Runtime.CompilerServices;

namespace EsUtil.Feature.MultiColumn.Internal;

/// <summary><b>幅と高さを表すサイズ情報を格納するレコード構造体</b></summary>
/// <remarks>
/// 【制約】<br/>
/// Width と Height は正の値。<br/>
/// 【最適化手法】<br/>
/// • AggressiveInlining でメソッド最適化<br/>
/// </remarks>
internal readonly record struct Size(double Width, double Height)
{
    /// <summary><b>IReadOnlyList から 配列に変換します</b></summary>
    /// <remarks>
    /// 【処理フロー】<br/>
    /// 1. Size 配列を新規作成<br/>
    /// 2. 各タプルを Size に変換して代入<br/>
    /// <br/>
    /// 【注意点】<br/>
    /// 入力が null の場合、例外が発生します。<br/>
    /// </remarks>
    /// <param name="items">（幅, 高さ）タプルのリスト</param>
    /// <returns>Size 配列</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Size[] ToSizeArray(IReadOnlyList<(double Width, double Height)> items)
    {
        // Size 配列を新規作成する
        var sizes = new Size[items.Count];
        // 各タプルを Size に変換して代入する
        for (int i = 0; i < items.Count; i++)
        {
            var (Width, Height) = items[i];
            sizes[i] = new(Width, Height);
        }
        return sizes;
    }

    /// <summary><b>ReadOnlySpan から 配列に変換します</b></summary>
    /// <remarks>
    /// 【処理フロー】<br/>
    /// 1. Size 配列を新規作成<br/>
    /// 2. 各タプルを Size に変換して代入<br/>
    /// <br/>
    /// 【注意点】<br/>
    /// ReadOnlySpan を使用して範囲アクセス。<br/>
    /// </remarks>
    /// <param name="items">（幅, 高さ）タプルのリスト</param>
    /// <returns>Size 配列</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Size[] ToSizeArray(ReadOnlySpan<(double Width, double Height)> items)
    {
        // Size 配列を新規作成する
        var sizes = new Size[items.Length];
        // 各タプルを Size に変換して代入する
        for (int i = 0; i < items.Length; i++)
        {
            var (Width, Height) = items[i];
            sizes[i] = new(Width, Height);
        }
        return sizes;
    }
}
