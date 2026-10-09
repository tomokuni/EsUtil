using Xunit;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Tests;

/// <summary>実 Skia の描画（<c>RenderTargetBitmap</c>・<c>Bitmap</c> の生成）を伴うテストを直列化するグループ。</summary>
/// <remarks>
/// 実 Skia（<c>UseHeadlessDrawing = false</c>）の描画は共有の状態を持ち、並列に実行すると
/// 後始末（テスト ケースのクリーンアップ）で競合して失敗することがある（実測）。<br/>
/// 描画を伴うテストクラスへ本グループ（<c>[Collection]</c>）を付けて直列化する
/// （<c>DisableParallelization</c> により他のグループとも並列にしない）。
/// </remarks>
[CollectionDefinition("AvaloniaSkia", DisableParallelization = true)]
public class AvaloniaSkiaGroup;
