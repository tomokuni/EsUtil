using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.WinUI3.Thumbnail.Sample.Imaging;
using Microsoft.UI.Xaml;

namespace EsUtil.Ui.WinUI3.Thumbnail.Sample.Views;

/// <summary>サムネイル一覧の動作確認ウィンドウ。</summary>
/// <remarks>
/// <b>確認できること</b>: 折り返し配置・可視範囲・選択・現在項目の強調・暗色テーマ・末尾へのスクロール。<br/>
/// 画像は <see cref="TestImageFactory"/> が生成する（外部ファイルを必要としない）。
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>生成した画像（一覧が参照するため保持する）。</summary>
    private readonly List<EsBitmap> _images = [];

    /// <summary>追加した件数。</summary>
    private int _count;

    /// <summary>ウィンドウを初期化して項目を追加する。</summary>
    public MainWindow()
    {
        InitializeComponent();

        Thumbnails.ImageWidth = 160;
        Thumbnails.SelectionChanged += (_, _) =>
            StatusText.Text = $"選択: {Thumbnails.SelectedIndex}（{Thumbnails.ItemCount} 件中）";
        Thumbnails.ItemActivated += (_, e) => StatusText.Text = $"確定: {e.Index}";

        AddButton.Click += (_, _) => AddItem();
        ScrollButton.Click += (_, _) => Thumbnails.ScrollToIndex(Thumbnails.ItemCount - 1);
        DarkThemeCheck.Checked += (_, _) => ApplyDarkTheme(true);
        DarkThemeCheck.Unchecked += (_, _) => ApplyDarkTheme(false);

        for (var i = 0; i < 12; i++)
        {
            AddItem();
        }

        Thumbnails.SelectedIndex = 0;
    }

    /// <summary>画像を 1 件生成して一覧へ追加する。</summary>
    private void AddItem()
    {
        var image = TestImageFactory.Create(320, 200, _count);
        _images.Add(image);

        Thumbnails.Add(
            EsReadOnlyBitmap.FromBitmap(image),
            $"capture-{_count:D4}.png",
            $"生成した画像 {_count}",
            isCurrent: _count == 0);

        _count++;
        StatusText.Text = $"{_count} 件を表示しています。";
    }

    /// <summary>暗色テーマを切り替える。</summary>
    /// <param name="isDark">暗色テーマにする場合は true。</param>
    private void ApplyDarkTheme(bool isDark)
    {
        Thumbnails.IsDarkTheme = isDark;
        Thumbnails.Refresh();
    }
}