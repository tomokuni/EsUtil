using Microsoft.UI.Xaml;

namespace EsUtil.Ui.WinUI3.SampleKit;

/// <summary>設定パネルの 1 項目（行の要素と、値を外から更新する手段を持つ）。</summary>
/// <remarks>
/// <b>値を外から更新する理由</b>: 対象コントロールの状態が利用者の操作で変わったとき（一覧の選択など）、<br/>
/// 設定パネルの表示も追随させる必要がある。 行だけを返すと入力 UI を掴めないため、更新手段を併せて返す。<br/>
/// <see cref="SetValue"/> は入力の変更イベントを発火しない（更新の往復で対象を再設定しない）。
/// </remarks>
public sealed class SampleField
{
    /// <summary>行の要素を取得する。</summary>
    /// <value>項目名と編集 UI を並べた要素。</value>
    public required UIElement Element { get; init; }

    /// <summary>表示する値を更新する。</summary>
    /// <value>値を設定する関数（入力の変更イベントは発火しない）。</value>
    public required Action<double> SetValue { get; init; }
}