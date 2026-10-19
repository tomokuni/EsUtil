namespace EsUtil.Platform.Core;

/// <summary>境界（アプリのイベント ハンドラー・外部 I/O・描画・UI コールバック）での例外捕捉の可否を単一所有する。</summary>
/// <remarks>
/// <b>使い方</b>: <c>catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))</c> の形で <c>when</c> フィルターに渡す。<br/>
/// <b>目的</b>: 「<c>catch (Exception)</c> は境界に限る」という規則（[`例外処理規約.md`] §4）を機械的に検証できるようにする。<br/>
/// 明示的に呼び出しているため、境界での包括捕捉は <c>git grep BoundaryCatch</c> で一覧でき、<br/>
/// 境界以外の包括捕捉はアナライザー <c>CA1031</c> がビルドで検出する（<c>when</c> フィルターが無い捕捉が該当する）。<br/>
/// <para/>
/// <b>判断の所有者</b>: 「どの例外を継続対象にするか」の判断は本クラスだけが持つ。 呼び出し側の <c>catch</c> は<br/>
/// 「ここは境界である」ことだけを表し、例外の種類で分岐しない（将来、特定の例外だけ再スローしたくなった場合は<br/>
/// 本メソッドだけを変えればすべての境界へ反映される）。<br/>
/// <para/>
/// <b>現時点の判断</b>: 境界では例外の種類で続行の可否を判断しない（すべて継続する）。 捕捉した例外の記録と<br/>
/// 利用者への通知は呼び出し側の責務であり、記録の無い捕捉を作らないことは [`例外処理規約.md`] §4 が定める。
/// </remarks>
public static class BoundaryCatch
{
    /// <summary>境界で捕捉して継続してよいかどうかを判定する。</summary>
    /// <param name="exception">捕捉した例外。</param>
    /// <returns>継続してよい場合は <see langword="true"/>（現時点では常に <see langword="true"/>）。</returns>
    public static bool IsRecoverable(Exception exception) => exception is not null;
}
