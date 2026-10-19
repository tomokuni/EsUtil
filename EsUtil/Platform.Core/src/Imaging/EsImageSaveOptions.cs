namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像をファイルへ保存するときのオプション（開発者が渡す調整値）。</summary>
/// <remarks>
/// <b>形式は既定では拡張子が決める</b>: どの形式で保存するかは保存先の拡張子が決める（<see cref="Format"/> を指定すると
/// 拡張子より優先される）。 本型は<b>その形式の中で調整できる値</b>だけを持ち、設定した項目が選ばれた形式で使えない場合は
/// <see cref="EsReadOnlyBitmap.Save(string, EsImageSaveOptions)"/> が例外にする（黙って無視しない）。<br/>
/// <b>出力を変える項目と変えない項目</b>: GDI+ のエンコーダーが実際に出力を変える値は限られる。
///
/// <list type="table">
/// <item>
///   <term>JPEG の品質が変わる</term>
///   <description><see cref="JpegQuality"/>（値によってファイル サイズが大きく変わる）。</description>
/// </item>
/// <item>
///   <term>減色で色数・画質・見た目が変わる</term>
///   <description><see cref="ColorCount"/>（実装は自前であり GDI+ のパラメーターではない。
///   8bpp のインデックス カラーとして書くため BMP / PNG / GIF で効く）、および<b>減色が行われる場合だけ</b>効く
///   <see cref="ColorQuality"/> と <see cref="Dither"/>。</description>
/// </item>
/// <item>
///   <term>値を変えても出力が完全に同一</term>
///   <description>BMP の色深度（8 / 16 / 24 / 32）と圧縮、PNG の圧縮レベル、GIF の走査方式（インターレース）と版。
///   GDI+ は値を受け取って<b>無視する</b>。 載せると「設定したのに効かない」ことになる。</description>
/// </item>
/// </list>
///
/// <b>既定（<c>default</c>）は GDI+ の既定と一致させない</b>: 明示した値だけをエンコーダーへ渡すため、
/// 省略した場合と結果が変わる。
/// 調整しない場合は <see cref="EsReadOnlyBitmap.Save(string)"/>（オプションを渡さない版）を使う。
/// </remarks>
public readonly record struct EsImageSaveOptions
{
    /// <summary>保存する形式（<c>null</c> の場合は保存先の拡張子が決める）。</summary>
    /// <value>指定した場合、拡張子より優先される。 保存先に拡張子が無ければ<b>この形式の拡張子を補完</b>する。</value>
    /// <remarks>
    /// <b>拡張子と食い違う場合</b>: 既定では<b>例外</b>にする（どちらが意図か読めないため）。<br/>
    /// <see cref="IgnoreFormatMismatch"/> を true にすると本値が優先され、与えられたファイル名のまま保存する
    /// （拡張子と中身が食い違うファイルができる）。<br/><br/>
    /// <b>指定しても拡張子が有効な場合</b>: 拡張子が本値と同じ形式を指していれば、そのまま保存する。
    /// </remarks>
    public EsImageFileFormat? Format { get; init; }

    /// <summary>拡張子と <see cref="Format"/> の食い違いを無視するか。</summary>
    /// <value>true の場合は <see cref="Format"/> を優先して与えられたファイル名へ保存する。 既定は false（例外にする）。</value>
    /// <remarks>
    /// <b>使用例</b>: 利用者が拡張子を変えずに形式だけ切り替えた場合や、拡張子を持たない受け渡し（一時ファイル等）で
    /// 意図した形式を保証したい場合。 <br/>
    /// <b><see cref="Format"/> が未指定の場合は意味を持たない</b>（拡張子が形式を決めるため食い違いが起こらない）。<br/>
    /// <b>本フラグを立てると、拡張子と中身が食い違うファイルを作れる</b>（<c>photo.png</c> に JPEG の中身を書く等）。<br/>
    /// 他のツールが拡張子で判別する場合は表示できないことがある。
    /// </remarks>
    public bool IgnoreFormatMismatch { get; init; }

    /// <summary>JPEG の品質（0〜100。 大きいほど高画質・大サイズ）。</summary>
    /// <value>null（既定）の場合は GDI+ の既定の品質を使う。 0〜100 の範囲外は例外になる（上限へ丸めない）。</value>
    /// <remarks>
    /// <b>JPEG（<c>.jpg</c> / <c>.jpeg</c>）だけが使える</b>（他の拡張子へ設定すると例外になる）。<br/>
    /// <b>値はファイル サイズへ直に効く</b>: 同じ画像でも品質によってファイル サイズが大きく変わる
    /// （低いほど小さい）。<br/>
    /// <b>画素の変化が乏しい画像では効果がない</b>（どの品質でも同じように圧縮されるため）。
    /// </remarks>
    public int? JpegQuality { get; init; }

    /// <summary>減色（パレット化）して残す色数（2〜256）。</summary>
    /// <value>null（既定）の場合は減色しない（元の画素のまま保存する）。</value>
    /// <remarks>
    /// <b>不可逆である</b>: 元の画素は戻らない（ファイル サイズを下げる代わりに色を失う）。
    /// 減色の結果は <see cref="EsReadOnlyBitmap.ReduceColors(int, EsImageQuality, EsDitherMode)"/> と同じ計算である（同じ色数・品質・ディザを指定すれば同じ絵になる）。<br/><br/>
    /// <b>指定しなくても GIF は常に量子化される</b>（GIF はパレットが必須であり、GDI+ のエンコーダーは
    /// 透明色の番号をどの画素にも使わせないため、32bpp のまま渡すと透過が失われる）。<br/>
    /// <b>使えるのは BMP / PNG / GIF だけである</b>（これらの形式はパレットを持てるため、
    /// 8bpp のインデックス カラーとして書く）。 <b>JPEG と TIFF へ設定すると例外になる</b>
    /// （JPEG はパレットを持てない。 TIFF は未検証のため受け付けない）。<br/>
    /// <b>ファイル サイズは小さくなる</b>（32bpp のままの既定と比較。 減色したファイルは
    /// <see cref="EsReadOnlyBitmap.Load(string)"/> で読み戻せ、BMP は画素が完全に一致する）。
    /// BMP は無圧縮であるため画素あたりのバイト数がそのまま効き（4 バイトから 1 バイトになる）、
    /// PNG と GIF は同じ色の繰り返しが増えるため圧縮が効く。<br/>
    /// <b>BMP の RLE 圧縮は併用できない</b>（GDI+ の BMP エンコーダーは圧縮の指定を無視するため、
    /// 減色しても無圧縮のままである。 圧縮も欲しい場合は PNG を選ぶこと）。<br/>
    /// <b>透過は形式ごとに扱いが違う</b>: 減色はアルファも色の一部として扱う（4 段階に量子化する）。
    /// <b>PNG はパレットのアルファ（<c>tRNS</c>）を書くため透過がそのまま残り</b>、 <b>GIF は完全透明だけが残る</b>
    /// （1 ビットの透過しか持てないため、半透明は不透明になる）。 <b>BMP はアルファを持てない</b>（パレットのアルファも無視される）。
    /// <b>半透明は 4 段階へ量子化され、RGBA を平均するため元の値とわずかに違う</b>（透明と不透明は完全に一致する）。<br/>
    /// <b>GIF は本値を指定しなくても常に量子化される</b>（GIF はパレットが必須であり、GDI+ の GIF エンコーダーは
    /// 透明色の番号をどの画素にも使わせないため、32bpp のまま渡すと透過が失われる。
    /// 自前の量子化にすると透過が保たれ、ファイルも小さく画質も良くなる）。
    /// </remarks>
    public int? ColorCount { get; init; }

    /// <summary>減色の品質（速度と画質の釣り合い）。</summary>
    /// <value>null（既定）の場合は <see cref="EsImageQuality.Mid"/> を使う。 指定できるのは
    /// <see cref="ColorCount"/> を指定した場合、または形式が GIF（常に量子化される）の場合だけである。</value>
    /// <remarks>
    /// <b>段階の意味</b>は <see cref="EsImageQuality"/> と
    /// <see cref="EsReadOnlyBitmap.ReduceColors(int, EsImageQuality, EsDitherMode)"/> が単一所有する
    /// （本値は保存の入口から同じ計算へ渡すだけである）。<br/>
    /// <b>減色しない保存へ設定すると例外になる</b>（<see cref="ColorCount"/> が null で GIF 以外の場合。
    /// 黙って無視すると「設定したのに効かない」ことに気付かないため）。
    /// </remarks>
    public EsImageQuality? ColorQuality { get; init; }

    /// <summary>減色のディザ（誤差拡散）の指定。</summary>
    /// <value>null（既定）の場合は <see cref="EsDitherMode.None"/> を使う（ファイルが最も小さくなる）。
    /// 指定できるのは <see cref="ColorCount"/> を指定した場合、または形式が GIF（常に量子化される）の場合だけである。</value>
    /// <remarks>
    /// <b>何が変わるか</b>: <see cref="EsDitherMode.FloydSteinberg"/> は<b>色数が内容に対して足りないとき</b>に
    /// グラデーションの縞（バンディング）を消す代わりに<b>ファイルが大きくなる</b>（<b>平坦な色の画像は
    /// 変化しない</b>）。 256 色のように足りている場合の判断を含む詳細は <see cref="EsDitherMode"/> を参照する。<br/>
    /// <b>減色しない保存へ設定すると例外になる</b>（<see cref="ColorQuality"/> と同じ規則）。
    /// </remarks>
    public EsDitherMode? Dither { get; init; }
}
