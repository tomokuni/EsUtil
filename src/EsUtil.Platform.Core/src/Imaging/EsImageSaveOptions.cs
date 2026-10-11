namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像をファイルへ保存するときのオプション（開発者が渡す調整値）。</summary>
/// <remarks>
/// <b>形式は既定では拡張子が決める</b>: どの形式で保存するかは保存先の拡張子が決める（<see cref="Format"/> を指定すると
/// 拡張子より優先される）。 本型は<b>その形式の中で調整できる値</b>だけを持ち、設定した項目が選ばれた形式で使えない場合は
/// <see cref="EsBitmapBase.Save(string, EsImageSaveOptions)"/> が例外にする（黙って無視しない）。<br/>
/// <b>項目が 1 つしかない理由（実測）</b>: GDI+ のエンコーダーが実際に出力を変える値は限られる。
/// 640x360 の画像を<b>全パラメーター × 全形式で総当たり</b>して確かめた結果:
///
/// <list type="table">
/// <item>
///   <term>品質・サイズが変わる</term>
///   <description>JPEG の品質（<see cref="JpegQuality"/>。 208 KB → 11 KB〜459 KB）。
///   JPEG の量子化テーブル（輝度・色差。 品質と同じつまみであり、品質を指定すると無視される）と、
///   TIFF の圧縮方式・色深度・CMYK 保存も値は変わるが、いずれも既定より大きくなるか画素を失う（下記）。</description>
/// </item>
/// <item>
///   <term>値を変えても出力が完全に同一</term>
///   <description>BMP の色深度（8 / 16 / 24 / 32）と圧縮、PNG の圧縮レベル、GIF の走査方式（インターレース）と版。
///   GDI+ は値を受け取って<b>無視する</b>。 載せると「設定したのに効かない」ことになる。</description>
/// </item>
/// <item>
///   <term>一部の形式だけ失敗する</term>
///   <description>TIFF の圧縮は 125 通り中 5 通り（LZW / RLE / 無圧縮 / CCITT3 / CCITT4）だけが成功し、
///   他は状態 2（<c>InvalidParameter</c>）で失敗する。 GIF の多重フレーム用のフラグは<b>保存後にファイルを開いたまま返す</b>。</description>
/// </item>
/// </list>
///
/// <b>既定（<c>default</c>）は GDI+ の既定と一致させない</b>: 明示した値だけをエンコーダーへ渡すため、
/// 省略した場合と結果が変わる（実測: 不規則な画像で既定 826 KB に対し品質 75 は 554 KB）。
/// 調整しない場合は <see cref="EsBitmapBase.Save(string)"/>（オプションを渡さない版）を使う。
/// </remarks>
public readonly record struct EsImageSaveOptions
{
    /// <summary>保存する形式（<c>null</c> の場合は保存先の拡張子が決める）。</summary>
    /// <value>指定した場合、拡張子より優先される。 保存先に拡張子が無ければ<b>この形式の拡張子を補完</b>する。</value>
    /// <remarks>
    /// <b>拡張子と食い違う場合</b>: 既定では<b>例外</b>にする（どちらが意図か読めないため）。
    /// <see cref="IgnoreFormatMismatch"/> を true にすると本値が優先され、与えられたファイル名のまま保存する
    /// （拡張子と中身が食い違うファイルができる）。<br/>
    /// <b>指定しても拡張子が有効な場合</b>: 拡張子が本値と同じ形式を指していれば、そのまま保存する。
    /// </remarks>
    public EsImageFileFormat? Format { get; init; }

    /// <summary>拡張子と <see cref="Format"/> の食い違いを無視するか。</summary>
    /// <value>true の場合は <see cref="Format"/> を優先して与えられたファイル名へ保存する。 既定は false（例外にする）。</value>
    /// <remarks>
    /// <b>使用例</b>: 利用者が拡張子を変えずに形式だけ切り替えた場合や、拡張子を持たない受け渡し（一時ファイル等）で
    /// 意図した形式を保証したい場合。 <b><see cref="Format"/> が未指定の場合は意味を持たない</b>
    /// （拡張子が形式を決めるため食い違いが起こらない）。<br/>
    /// <b>本フラグを立てると、拡張子と中身が食い違うファイルを作れる</b>（<c>photo.png</c> に JPEG の中身を書く等）。
    /// 他のツールが拡張子で判別する場合は表示できないことがある。
    /// </remarks>
    public bool IgnoreFormatMismatch { get; init; }

    /// <summary>JPEG の品質（0〜100。 大きいほど高画質・大サイズ）。</summary>
    /// <value>null（既定）の場合は GDI+ の既定の品質を使う。 0〜100 の範囲外は例外になる（上限へ丸めない）。</value>
    /// <remarks>
    /// <b>JPEG（<c>.jpg</c> / <c>.jpeg</c>）だけが使える</b>（他の拡張子へ設定すると例外になる）。<br/>
    /// <b>実測（1280x720・不規則な画像）</b>: 0 = 42,597 B / 50 = 378,312 B / 75 = 554,267 B / 90 = 791,497 B /
    /// 100 = 1,821,653 B（既定は 826,483 B で品質 90〜100 の間に相当する）。<br/>
    /// <b>実測（1280x720・なだらかな画像）</b>: 0 = 15,259 B / 75 = 30,947 B / 100 = 195,783 B
    /// （既定は 44,273 B）。<br/>
    /// <b>単色の画像では効果がない</b>（実測: 0 = 15,026 B / 100 = 15,031 B）。 画素の変化が乏しい画像は
    /// どの品質でも同じように圧縮される。
    /// </remarks>
    public int? JpegQuality { get; init; }
}
