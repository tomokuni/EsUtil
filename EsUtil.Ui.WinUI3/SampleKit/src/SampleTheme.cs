using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace EsUtil.Ui.WinUI3.SampleKit;

/// <summary>サンプルの表示テーマ（ライト / ダーク）をルート要素へ適用する。</summary>
/// <remarks>
/// <b>なぜサンプルが持つか</b>: テーマの適用方法は UI ごとに異なる（WinUI 3 はルート要素の <c>RequestedTheme</c>）。<br/>
/// サンプルは「表示テーマ」の 1 項目だけを持ち、適用の詳細は本クラスへ閉じる（各サンプルへ書き分けない）。<br/>
/// <b>現在のテーマ</b>: アプリの <see cref="Application.RequestedTheme"/>（起動時に決まる OS の設定）から読む。<br/>
/// <b>適用先はウィンドウではなくルート要素</b>: アプリ全体の <c>RequestedTheme</c> は起動後に変更できず、<br/>
/// <see cref="Window.Content"/> は組み立ての途中では未設定のため、生成したルート要素へ直接適用する。
/// </remarks>
public static class SampleTheme
{
    /// <summary>表示テーマの選択肢（設定パネルの並び）。</summary>
    public static IReadOnlyList<string> Choices { get; } = new[] { "ライト", "ダーク" };

    /// <summary>現在のテーマがダークかどうかを取得する。</summary>
    /// <value>ダークの場合は true（アプリの <c>RequestedTheme</c> がダークのとき）。</value>
    public static bool IsDark => Application.Current?.RequestedTheme == ApplicationTheme.Dark;

    /// <summary>表示テーマをルート要素へ適用する。</summary>
    /// <param name="root">サンプルのルート要素。</param>
    /// <param name="isDark">ダークにする場合は true。</param>
    /// <remarks>
    /// ルート要素へ明示的に指定する（その配下の要素へ継承される）。<br/>
    /// ルート要素の地色も併せて切り替える（地色を固定したまま要素のテーマだけ変えると、<br/>
    /// 文字がダーク向けの白になり、白い地色の上で読めなくなる）。
    /// </remarks>
    public static void Apply(FrameworkElement root, bool isDark)
    {
        root.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        if (root is Panel panel)
        {
            // 地色もテーマへ合わせる（地色はサンプルの見た目であり、テーマの資源からは取れない）
            panel.Background = new SolidColorBrush(isDark
                ? Color.FromArgb(255, 32, 32, 32)
                : Color.FromArgb(255, 255, 255, 255));
        }
    }

    /// <summary>選択肢のインデックスをテーマへ写す。</summary>
    /// <param name="index">選択肢のインデックス（0 = ライト / 1 = ダーク）。</param>
    /// <returns>ダークの場合は true。</returns>
    public static bool IsDarkAt(int index) => index == 1;

    /// <summary>現在のテーマに対応する選択肢のインデックスを取得する。</summary>
    /// <returns>0（ライト）または 1（ダーク）。</returns>
    public static int CurrentIndex() => IsDark ? 1 : 0;
}
