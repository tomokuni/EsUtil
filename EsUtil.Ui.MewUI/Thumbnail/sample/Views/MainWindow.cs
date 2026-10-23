using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.MewUI.Thumbnail.Sample.Imaging;

namespace EsUtil.Ui.MewUI.Thumbnail.Sample.Views;

/// <summary>サムネイル一覧の動作確認ウィンドウ。</summary>
/// <remarks>
/// <b>確認できること</b>: 折り返し配置・可視範囲・選択・現在項目の強調・暗色テーマ・末尾へのスクロール。<br/>
/// 画像は <see cref="TestImageFactory"/> が生成する（外部ファイルを必要としない）。
/// </remarks>
internal static class MainWindow
{
    /// <summary>生成した画像（一覧が参照するため、静的で保持する）。</summary>
    private static readonly List<EsBitmap> s_images = [];

    /// <summary>追加した件数。</summary>
    private static int s_count;

    /// <summary>ウィンドウを組み立てる。</summary>
    /// <returns>生成したウィンドウ。</returns>
    public static Window Create()
    {
        var status = new TextBlock().Text("サムネイルを選択してください。");

        var thumbnails = new ThumbnailControl { ImageWidth = 160 };
        thumbnails.SelectionChanged += (_, _) =>
            status.Text = $"選択: {thumbnails.SelectedIndex}（{thumbnails.ItemCount} 件中）";
        thumbnails.ItemActivated += (_, e) => status.Text = $"確定: {e.Index}";

        var toolbar = new StackPanel().Horizontal().Spacing(8).Children(
            status,
            new Button().Content("追加").OnClick(() => AddItem(thumbnails, status)),
            new Button().Content("末尾へスクロール").OnClick(() => thumbnails.ScrollToIndex(thumbnails.ItemCount - 1)),
            new CheckBox().Content("暗色テーマ").OnCheckedChanged(value =>
            {
                thumbnails.IsDarkTheme = value;
                thumbnails.Refresh();
            }));

        var root = new DockPanel().Children(toolbar, thumbnails);
        DockPanel.SetDock(toolbar, Dock.Top);

        for (var i = 0; i < 12; i++)
        {
            AddItem(thumbnails, status);
        }

        thumbnails.SelectedIndex = 0;

        var window = new Window()
            .Title("ThumbnailControl サンプル（MewUI）")
            .Resizable(900, 600, minWidth: 640, minHeight: 400);
        window.Content(root);
        return window;
    }

    /// <summary>画像を 1 件生成して一覧へ追加する。</summary>
    /// <param name="thumbnails">追加先の一覧。</param>
    /// <param name="status">状態表示。</param>
    private static void AddItem(ThumbnailControl thumbnails, TextBlock status)
    {
        var image = TestImageFactory.Create(320, 200, s_count);
        s_images.Add(image);

        thumbnails.Add(EsReadOnlyBitmap.FromBitmap(image), $"capture-{s_count:D4}.png", $"生成した画像 {s_count}", isCurrent: s_count == 0);

        s_count++;
        status.Text = $"{s_count} 件を表示しています。";
    }
}