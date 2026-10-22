using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.WinUI3.Interop.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
/// <remarks>
/// 画像はコアの型（<see cref="EsReadOnlyBitmap"/>）で作る。 描画の検証は画素ではなく<br/>
/// 描画リスト（<c>DrawList</c>）で行うため、描画先（キャンバス）の用意は不要である。
/// </remarks>
internal static class TestHelper
{
    /// <summary>指定サイズの単色画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    /// <remarks>
    /// 生成した画像は書き込まず読むだけ（表示・描画の入力）であるため、凍結して 0 コピーで
    /// 読み取り専用の面へ渡す。
    /// </remarks>
    public static EsReadOnlyBitmap CreateBitmap(int width, int height, DrawingColor color)
    {
        using var created = EsBitmap.Create(width, height, color);
        created.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(created);
    }
}
