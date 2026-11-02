namespace EsUtil.Feature.Marquee.Core.Internal;

/// <summary>設定値の変更検出（同値なら何もしない）を単一所有する。</summary>
/// <remarks>
/// コアと提示層の両方が使う（1 つのアセンブリに統合したため、同じ実装を層ごとに持たない）。<br/>
/// 変更前の値も返すため、変更を検出した側（設定の変更ログ）が前後を比較できる。
/// </remarks>
internal static class ChangeTracking
{
    /// <summary>新しい値を保持する（同値なら何もしない）。</summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="field">保持先のフィールド。</param>
    /// <param name="value">設定する値。</param>
    /// <param name="previous">変更前の値（同値の場合は現在値）。</param>
    /// <returns>値を変更した場合は true（同値だった場合は false）。</returns>
    /// <remarks>
    /// 「同値なら何もしない」規則を 1 箇所に閉じる<br/>
    /// （各プロパティが同じ数行を書くと、片方だけ同値判定が抜けて無駄なログ・再評価が溢れる）。
    /// </remarks>
    internal static bool SetIfChanged<T>(ref T field, T value, out T previous)
    {
        previous = field;
        if (EqualityComparer<T>.Default.Equals(previous, value))
        {
            return false;
        }

        field = value;
        return true;
    }
}
