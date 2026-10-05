using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;

namespace EsUtil.Feature.Marquee.Tests;

/// <summary>テストで使用するカーソルサービスのスパイ（呼び出しを記録する）。</summary>
/// <remarks>位置の取得・移動は未使用のため記録のみ行い、表示切替の呼び出し履歴を検証に使用する。</remarks>
internal sealed class CursorServiceSpy : ICursorService
{
    /// <summary><see cref="ShowCursor"/> に渡された値の履歴（呼び出し順）。</summary>
    public List<bool> ShowCalls { get; } = [];

    /// <summary>呼び出しの履歴（種類と値。 呼び出し順の検証に使用する）。</summary>
    public List<string> Calls { get; } = [];

    /// <summary><see cref="ICursorService.GetCursorScreenPoint"/> の戻り値。</summary>
    public EsPointInt CursorPoint { get; set; }

    /// <inheritdoc/>
    public EsPointInt GetCursorScreenPoint() => CursorPoint;

    /// <inheritdoc/>
    public void SetCursorScreenPoint(EsPointInt point)
    {
        CursorPoint = point;
        Calls.Add($"move:{point.X},{point.Y}");
    }

    /// <inheritdoc/>
    public void ShowCursor(bool show)
    {
        ShowCalls.Add(show);
        Calls.Add($"show:{show}");
    }
}
