using EsUtil.Platform.Core.Common;

namespace EsUtil.Platform.Core;

/// <summary>例外の扱い（境界での捕捉可否）の窓口。</summary>
/// <remarks>
/// 機能の型は <see cref="EsUtil.Platform.Core.Common"/>（<see cref="BoundaryCatch"/>）に置く。<br/>
/// 本クラスは<b>利用側が <c>using EsUtil.Platform.Core;</c> だけで境界の判定を行えるようにする</b>ための窓口であり、<br/>
/// <b>実装を持たない</b>（どの例外を継続対象にするかの判断は <see cref="BoundaryCatch"/> が単一所有する）。
/// </remarks>
public static class EsErrors
{
    /// <summary>境界で捕捉して継続してよいかどうかを判定する。</summary>
    /// <param name="exception">捕捉した例外。</param>
    /// <returns>継続してよい場合は <see langword="true"/>。</returns>
    /// <remarks><c>catch (Exception ex) when (EsErrors.IsRecoverable(ex))</c> の形で <c>when</c> フィルターへ渡す。</remarks>
    public static bool IsRecoverable(Exception exception) => BoundaryCatch.IsRecoverable(exception);
}
