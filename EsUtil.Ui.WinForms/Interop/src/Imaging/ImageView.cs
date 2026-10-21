using System.ComponentModel;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Ui.WinForms.Interop.Imaging;

/// <summary>コアの画像（<see cref="EsReadOnlyBitmap"/>）を親が割り当てた領域へ描画する表示ビュー（WinForms / GDI+ 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Adapter（<see cref="EsReadOnlyBitmap"/> を WinForms のコントロールとして適合させる）。<br/>
/// <b>用途</b>: 「割り当てられた領域いっぱいへ 1 枚の画像を敷く」表示に使う。 表示サイズは親のレイアウトに従う<br/>
/// （本クラス自身は要求サイズを持たない）。<br/>
/// <br/>
/// <b>配置方法</b>: <see cref="UniformDraw"/> が false（既定）の場合は領域全体へ引き伸ばし、<br/>
/// true の場合は縦横比を保って中央へ収める（余白は描画しない）。<br/>
/// <br/>
/// <b>画素の複製を保持する理由</b>: GDI+ はコアの画素を直接描けないため、<see cref="Source"/> の設定時に<br/>
/// 1 度だけ <see cref="EsBitmapDrawingInterop.ToDrawingBitmap"/> で複製し、破棄まで保持する<br/>
/// （描画のたびに全面を複製すると、描画の費用が複製の費用に支配される）。<br/>
/// <br/>
/// <b>透明の下地</b>: 本クラスは子コントロールを重ねられるよう、自身の描画の前に下地を塗らない<br/>
/// （親の描画をそのまま残す。 画像の無い部分は親が見える）。
/// </remarks>
public class ImageView : Control
{
    /// <summary>表示する画像（コアの画像）。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>表示に使う GDI+ のビットマップ（<see cref="Source"/> の設定時に 1 度だけ作る）。</summary>
    private Bitmap? _bitmap;

    /// <summary>表示ビューを初期化する。</summary>
    /// <remarks>
    /// <b>自前で描くためのスタイル設定</b>: <see cref="ControlStyles.UserPaint"/>（<see cref="OnPaint"/> が描く）と<br/>
    /// <see cref="ControlStyles.AllPaintingInWmPaint"/>（<c>WM_ERASEBKGND</c> で消さない）で前のフレームのちらつきを抑え、<br/>
    /// <see cref="ControlStyles.OptimizedDoubleBuffer"/> で描画面を 1 枚にまとめ、<br/>
    /// <see cref="ControlStyles.ResizeRedraw"/> でサイズ変更時に描き直す。
    /// </remarks>
    public ImageView()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
    }

    /// <summary>表示する画像を取得または設定する。</summary>
    /// <value>表示する画像。 null または破棄済みの場合は何も描画しない（下地のみ）。</value>
    /// <remarks>
    /// 設定すると GDI+ のビットマップへ複製し直し、直前の複製を破棄する（同じインスタンスの再設定では複製しない）。<br/>
    /// 画像の所有権はホストにある（本クラスは複製だけを所有し、引数の画像は破棄しない）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EsReadOnlyBitmap? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }

            _source = value;
            ReplaceBitmap(value is { IsDisposed: false } source ? EsBitmapDrawingInterop.ToDrawingBitmap(source) : null);
            Invalidate();
        }
    }

    /// <summary>縦横比を保って領域へ収めるかどうかを取得する。</summary>
    /// <value>true の場合は縦横比を保って中央へ収める。 false の場合は領域全体へ引き伸ばす。 既定値は false。</value>
    /// <remarks>派生クラスが固定値を与える（プロパティにすると、使い分けのない側へ不要な設定面が増える）。</remarks>
    protected virtual bool UniformDraw => false;

    /// <summary>画像を背景として描画する。</summary>
    /// <param name="pevent">描画のイベント引数（描画面を持つ）。</param>
    /// <remarks>
    /// <b>背景として描く理由</b>: 本コントロールの上へ透明な子（撮影オーバーレイ・選択矩形など）を重ねるため。<br/>
    /// WinForms の透明な子は「親の<b>背景</b>」を自分の下地として描く（親の <see cref="OnPaint"/> は呼ばない）。<br/>
    /// 画像を <see cref="OnPaint"/> で描くと、透明な子が動いた跡（残像）を親の背景で消せない。
    /// </remarks>
    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        ArgumentNullException.ThrowIfNull(pevent);

        if (_bitmap is null)
        {
            base.OnPaintBackground(pevent);
            return;
        }

        RenderTo(pevent.Graphics, ClientSize);
    }

    /// <summary>画像を描画する（背景として描くため本メソッドでは描かない）。</summary>
    /// <param name="e">描画のイベント引数（描画面を持つ）。</param>
    /// <remarks>
    /// 図形の描画は <see cref="ImageRenderer"/> が単一所有する（本メソッドは配置の判断を持たない）。<br/>
    /// 画像は <see cref="OnPaintBackground"/> が描く（透明な子が下地として参照できるようにするため）。
    /// </remarks>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);
    }

    /// <summary>画像を指定の描画面と領域へ描画する（描画の実体。 テストはウィンドウなしで本メソッドを呼ぶ）。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="targetSize">描画先の領域のサイズ（物理ピクセル）。</param>
    /// <remarks>
    /// 表示の描画手順を <see cref="OnPaint"/> から分離し、ウィンドウ（ハンドル）を生成せずに画素を検証できるようにする。
    /// </remarks>
    internal void RenderTo(Graphics graphics, Size targetSize)
        => ImageRenderer.Render(graphics, targetSize, _bitmap, UniformDraw ? EsImageDrawMode.Uniform : EsImageDrawMode.Fill);

    /// <summary>保持している資源を解放する。</summary>
    /// <param name="disposing">マネージドの資源も解放する場合は true。</param>
    /// <remarks>複製したビットマップは本クラスが所有するため破棄する（引数の画像は破棄しない）。</remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bitmap?.Dispose();
            _bitmap = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>表示に使うビットマップを差し替える（直前の複製を破棄する）。</summary>
    /// <param name="bitmap">新しいビットマップ。 null の場合は複製を破棄して描画しない。</param>
    private void ReplaceBitmap(Bitmap? bitmap)
    {
        var previous = _bitmap;
        _bitmap = bitmap;
        previous?.Dispose();
    }
}
