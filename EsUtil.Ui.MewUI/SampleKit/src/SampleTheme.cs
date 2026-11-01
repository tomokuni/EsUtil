using Aprillz.MewUI;

namespace EsUtil.Ui.MewUI.SampleKit;

/// <summary>サンプルの表示テーマ（ライト / ダーク）をアプリ全体へ適用する。</summary>
/// <remarks>
/// <b>なぜサンプルが持つか</b>: テーマの適用方法は UI ごとに異なる（MewUI は <c>Application.SetThemeMode</c>）。<br/>
/// サンプルは「表示テーマ」の 1 項目だけを持ち、適用の詳細は本クラスへ閉じる（各サンプルへ書き分けない）。<br/>
/// <b>現在のテーマ</b>: OS / アプリの設定（<c>Application.Theme.IsDark</c>）から読む（利用者に選ばせない）。
/// </remarks>
public static class SampleTheme
{
    /// <summary>表示テーマの選択肢（設定パネルの並び）。</summary>
    public static IReadOnlyList<string> Choices { get; } = ["ライト", "ダーク"];

    /// <summary>現在のテーマがダークかどうかを取得する。</summary>
    /// <value>ダークの場合は true（<c>Application.Theme.IsDark</c>）。 アプリの初期化前は false。</value>
    /// <remarks>
    /// <b>初期化前は例外になる</b>: MewUI の <c>Application.Current</c> は <c>Application.Run</c> まで実行されないと
    /// <c>InvalidOperationException</c> を送出する（ウィンドウの生成時はまだ初期化されていない。 実測 2026-11-01）。<br/>
    /// サンプルはウィンドウを先に作るため、初期化前はライトとして扱う。
    /// </remarks>
    public static bool IsDark => Application.IsRunning && Application.Current.Theme.IsDark;

    /// <summary>表示テーマを適用する。</summary>
    /// <param name="isDark">ダークにする場合は true。</param>
    /// <remarks>
    /// アプリ全体（<c>Application.SetThemeMode</c>）へ適用する（サンプルの見た目が揃う）。<br/>
    /// 初期化前（<c>Application.Run</c> より前）は適用しない（<see cref="IsDark"/> と同じ理由）。
    /// </remarks>
    public static void Apply(bool isDark)
    {
        if (Application.IsRunning)
        {
            Application.Current.SetThemeMode(isDark ? ThemeVariant.Dark : ThemeVariant.Light);
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
