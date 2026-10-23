using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.WinForms.Interop.Imaging;
using EsUtil.Ui.WinForms.Thumbnail.Sample.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;

namespace EsUtil.Ui.WinForms.Thumbnail.Sample.Views;

/// <summary>サムネイル一覧の動作確認フォーム。</summary>
/// <remarks>
/// <b>確認できること</b>: 折り返し配置・可視範囲・選択・現在項目の強調・暗色テーマ・末尾へのスクロール。<br/>
/// 画像は <see cref="TestImageFactory"/> が生成する（外部ファイルを必要としない）。
/// </remarks>
internal sealed class MainForm : Form
{
    /// <summary>生成した画像（一覧が参照するため保持する）。</summary>
    private readonly List<DrawingBitmap> _images = [];

    /// <summary>サムネイル一覧。</summary>
    private readonly ThumbnailControl _thumbnails = new() { Dock = DockStyle.Fill, ImageWidth = 160 };

    /// <summary>状態表示。</summary>
    private readonly Label _status = new() { AutoSize = true, Text = "サムネイルを選択してください。", Padding = new Padding(8) };

    /// <summary>追加した件数。</summary>
    private int _count;

    /// <summary>フォームを組み立てる。</summary>
    public MainForm()
    {
        Text = "ThumbnailControl サンプル（WinForms）";
        ClientSize = new Size(900, 600);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        var add = new Button { Text = "追加", AutoSize = true };
        var scroll = new Button { Text = "末尾へスクロール", AutoSize = true };
        var darkTheme = new CheckBox { Text = "暗色テーマ", AutoSize = true };

        add.Click += (_, _) => AddItem();
        scroll.Click += (_, _) => _thumbnails.ScrollToIndex(_thumbnails.ItemCount - 1);
        darkTheme.CheckedChanged += (_, _) =>
        {
            _thumbnails.IsDarkTheme = darkTheme.Checked;
            _thumbnails.Refresh();
        };

        _thumbnails.SelectionChanged += (_, _) =>
            _status.Text = $"選択: {_thumbnails.SelectedIndex}（{_thumbnails.ItemCount} 件中）";
        _thumbnails.ItemActivated += (_, e) => _status.Text = $"確定: {e.Index}";

        toolbar.Controls.AddRange([_status, add, scroll, darkTheme]);
        Controls.Add(_thumbnails);
        Controls.Add(toolbar);

        for (var i = 0; i < 12; i++)
        {
            AddItem();
        }

        _thumbnails.SelectedIndex = 0;
    }

    /// <summary>画像を 1 件生成して一覧へ追加する。</summary>
    private void AddItem()
    {
        var image = TestImageFactory.Create(320, 200, _count);
        _images.Add(EsBitmapDrawingInterop.ToDrawingBitmap(EsReadOnlyBitmap.FromBitmap(image)));

        _thumbnails.Add(_images[^1], $"capture-{_count:D4}.png", $"生成した画像 {_count}", isCurrent: _count == 0);

        _count++;
        _status.Text = $"{_count} 件を表示しています。";
    }
}