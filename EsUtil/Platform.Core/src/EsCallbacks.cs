using EsUtil.Platform.Core.Common;

namespace EsUtil.Platform.Core;

/// <summary>購読（値型・<see cref="EventArgs"/> 非派生の通知）の窓口。</summary>
/// <remarks>
/// 機能の型は <see cref="EsUtil.Platform.Core.Common"/>（<see cref="CallbackList{T}"/>）に置く。<br/>
/// 本クラスは<b>利用側が <c>using EsUtil.Platform.Core;</c> だけで購読の一覧を用意できるようにする</b>ための窓口であり、<br/>
/// <b>実装を持たない</b>（通知の手順は <see cref="CallbackList{T}"/> が単一所有する）。
/// </remarks>
public static class EsCallbacks
{
    /// <summary>購読の一覧を生成する。</summary>
    /// <typeparam name="T">通知する値の型。</typeparam>
    /// <returns>生成した購読の一覧。</returns>
    public static CallbackList<T> Create<T>() => new();
}
