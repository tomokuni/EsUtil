namespace EsUtil.Feature.Annotation.Core;

/// <summary>注釈レイヤーが受け取った、プレビュー（画像の表示）へ渡す操作。</summary>
/// <remarks>
/// <b>入力の所有者は注釈レイヤー</b>である: 注釈の無い場所の操作は、レイヤーが本値でホストへ知らせ、
/// ホストがプレビュー（ズーム・パン・自動切り出し）へ渡す。 入力を「どの要素が受け取るか」で切り替えると、
/// 受け取らなかった側が次の操作を受け取れず、選択の解除や再選択ができなくなる（実測 2026-11-20）。
/// </remarks>
public enum AnnotationPreviewGesture
{
    /// <summary>ポインターの押下（ボタンの種類を問わない）。 メニューの外側クリックを閉じるために使う。</summary>
    Press,

    /// <summary>ドラッグの開始（左ボタンの押下）。</summary>
    DragStart,

    /// <summary>ドラッグ中（左ボタンを押したままの移動）。</summary>
    DragMove,

    /// <summary>ドラッグの終了（左ボタンの解放）。</summary>
    DragEnd,

    /// <summary>ホイール（拡大・縮小・スクロール）。</summary>
    Wheel,

    /// <summary>ダブルクリック（自動切り出しの確定に使う）。</summary>
    DoubleClick,

    /// <summary>ポインタの移動（押していない）。 ホバーの表示に使う。</summary>
    Hover,

    /// <summary>ポインタが領域から外れた。</summary>
    Leave,
}
