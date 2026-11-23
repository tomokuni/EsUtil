namespace EsUtil.Feature.Annotation.Core.Contract;

/// <summary>注釈レイヤーがプレビューへ渡す操作の内容。</summary>
/// <remarks>位置は注釈レイヤーのローカル DIP（プレビューと同じ座標系）である。</remarks>
public sealed class AnnotationPreviewGestureEventArgs(
    AnnotationPreviewGesture gesture,
    double x,
    double y,
    int clickCount = 1,
    int wheelDelta = 0,
    bool isShiftDown = false,
    bool isControlDown = false) : EventArgs
{
    /// <summary>操作の種類を取得する。</summary>
    public AnnotationPreviewGesture Gesture { get; } = gesture;

    /// <summary>位置（ローカル DIP）を取得する。</summary>
    public double X { get; } = x;

    /// <summary>位置（ローカル DIP）を取得する。</summary>
    public double Y { get; } = y;

    /// <summary>クリック回数を取得する。</summary>
    public int ClickCount { get; } = clickCount;

    /// <summary>ホイールの移動量を取得する（正 = 奥へ回す）。</summary>
    public int WheelDelta { get; } = wheelDelta;

    /// <summary>Shift キーが押されているかどうかを取得する。</summary>
    public bool IsShiftDown { get; } = isShiftDown;

    /// <summary>Ctrl キーが押されているかどうかを取得する。</summary>
    public bool IsControlDown { get; } = isControlDown;
}
