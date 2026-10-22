using EsUtil.Feature.Marquee.Core.Contract;

namespace EsUtil.Ui.WinForms.Marquee.Tests;

/// <summary>コアが要求する効果を記録するだけのテスト用実装。</summary>
/// <remarks>
/// 提示層（<see cref="MarqueePresenter"/>）を UI なしで動かすために使う。<br/>
/// 実際の UI 操作は行わず、要求された回数だけを数える（描画計画の解決が効果の要求に依存しないことを確かめる）。
/// </remarks>
internal sealed class EffectsSpy : MarqueeEffectsBase
{
    /// <summary>フォーカスを要求された回数。</summary>
    public int FocusRequestCount { get; private set; }

    /// <summary>再描画を要求された回数。</summary>
    public int RedrawRequestCount { get; private set; }

    /// <summary>設定されたカーソル形状の履歴。</summary>
    public List<MarqueeCursorKind> CursorShapes { get; } = [];

    /// <summary>要求されたポインター キャプチャの履歴。</summary>
    public List<bool> Captures { get; } = [];

    /// <summary>採用した画面座標（<see cref="ToScreenPoint"/> が返す値）。</summary>
    public EsPointInt ScreenPoint { get; set; }

    /// <inheritdoc />
    public override void RequestFocus() => FocusRequestCount++;

    /// <inheritdoc />
    public override void RequestRedraw() => RedrawRequestCount++;

    /// <inheritdoc />
    public override void SetCursorShape(MarqueeCursorKind kind) => CursorShapes.Add(kind);

    /// <inheritdoc />
    public override void SetPointerCapture(bool capture) => Captures.Add(capture);

    /// <inheritdoc />
    public override EsPointInt ToScreenPoint(EsPoint sourcePoint) => ScreenPoint;
}
