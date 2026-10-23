using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Avalonia.Media;

namespace EsUtil.Ui.AvaloniaUI.ColorPicker;

/// <summary>色文字列（色名 or 16進 + 不透明度%）の解析・整形・正規化を提供するユーティリティ。</summary>
/// <remarks>
/// 色と不透明度を統合した文字列のうち、**保存値**と**画面表示**の 2 形式を定義する:<br/>
/// - 保存値（<see cref="ToArgbHex(Color)"/> の出力）: <c>#AARRGGBB</c>（アルファ先頭 8 桁。 例: "#80FF0000"）<br/>
/// - 画面表示（<see cref="ToDisplayText"/> / <see cref="Format"/> の出力）: <c>{色}+{不透明度%}</c><br/>
///   - 色名が <see cref="Colors"/> に定義されている色: 色名で表す（例: "Red"）<br/>
///   - 色名が未定義の色: <c>#RRGGBB</c> で表す（例: "#336699"）<br/>
///   - 不透明度が 100% の場合: <c>{色}</c>（例: "Red", "#336699"）<br/>
///   - 不透明度が 100% 以外の場合: <c>{色}+{不透明度%}</c>（例: "Red+50%", "#336699+75%"）<br/>
/// 入力は両形式（色名 / #RRGGBB / #AARRGGBB / +% 形式）を <see cref="TryParse"/> で受け付ける。<br/>
/// 本形式は <see cref="ColorPickerControl.SelectedColor"/> の文字列仕様であり、<br/>
/// ホストアプリ（設定の保存・反映）も本ユーティリティで解析できるよう public とする。<br/>
/// <br/>
/// **適用パターン**: Flyweight（色 → 色名 の逆引き辞書を事前構築し、検索を O(1) 化）。
/// </remarks>
public static class ColorString
{
    /// <summary>色名（英名）の辞書（<see cref="Colors"/> の全色名 → 色）。</summary>
    /// <remarks><see cref="Colors"/> の公開静的プロパティをリフレクションで列挙する（140 色）。</remarks>
    private static readonly IReadOnlyDictionary<string, Color> s_colorNames = BuildColorNames();

    /// <summary>色（ARGB 完全一致）→ 色名 / RGB 一致（アルファ無視）→ 色名 の逆引き辞書。</summary>
    /// <remarks>
    /// <see cref="FindColorName"/> の O(1) 検索用（RGB 一致は同 RGB の場合に不透明 A=255 を優先登録する）。<br/>
    /// 宣言時に一度だけ構築する（静的コンストラクターを持たない = beforefieldinit を保つ）。
    /// </remarks>
    private static readonly (Dictionary<Color, string> Exact, Dictionary<(byte R, byte G, byte B), string> Rgb) s_reverseLookups =
        BuildReverseLookups(s_colorNames);

    /// <summary><see cref="TryParse"/> の解析結果キャッシュ（入力文字列 → 解析結果）。</summary>
    /// <remarks>
    /// ComboBox のテキスト入力はキーストローク毎に <see cref="TryParse"/> が複数回呼ばれるため、<br/>
    /// 同一文字列の解析結果を共有して再解析を排除する（スレッドセーフ）。
    /// </remarks>
    private static readonly ConcurrentDictionary<string, (bool Success, Color Color, double Opacity)> s_parseCache = new(StringComparer.Ordinal);

    /// <summary>解析結果キャッシュの最大容量（超過時は全クリアして無制限の増加を防ぐ）。</summary>
    /// <remarks>テスト（EsUtil.Ui.AvaloniaUI.ColorPicker.Tests）が容量超過の分岐を検証するため internal 公開する。</remarks>
    internal const int ParseCacheCapacity = 512;

    /// <summary><see cref="ToDisplayText"/> の表示テキストキャッシュ（入力文字列 → 表示テキスト）。</summary>
    /// <remarks>
    /// ComboBox の表示バインドはキーストローク毎に <see cref="ToDisplayText"/> を呼ぶため、<br/>
    /// 整形結果（色名検索＋文字列補間）を共有して毎回の文字列生成を排除する（スレッドセーフ）。
    /// </remarks>
    private static readonly ConcurrentDictionary<string, string> s_displayTextCache = new(StringComparer.Ordinal);

    /// <summary>表示テキストキャッシュの最大容量（超過時は全クリアして無制限の増加を防ぐ）。</summary>
    /// <remarks>テスト（EsUtil.Ui.AvaloniaUI.ColorPicker.Tests）が容量超過の分岐を検証するため internal 公開する。</remarks>
    internal const int DisplayTextCacheCapacity = 512;

    /// <summary><see cref="ToArgbHex(string?)"/> の正規化文字列キャッシュ（入力文字列 → #AARRGGBB）。</summary>
    /// <remarks>
    /// ComboBox の ConvertBack（キーストローク毎のバインド更新）が <see cref="ToArgbHex(string?)"/> を呼ぶため、<br/>
    /// 正規化結果（16進補間）を共有して毎回の文字列生成を排除する（スレッドセーフ）。
    /// </remarks>
    private static readonly ConcurrentDictionary<string, string> s_normalizedCache = new(StringComparer.Ordinal);

    /// <summary>正規化文字列キャッシュの最大容量（超過時は全クリアして無制限の増加を防ぐ）。</summary>
    /// <remarks>テスト（EsUtil.Ui.AvaloniaUI.ColorPicker.Tests）が容量超過の分岐を検証するため internal 公開する。</remarks>
    internal const int NormalizedCacheCapacity = 512;

    /// <summary>色名辞書を構築する（<see cref="Colors"/> の公開静的プロパティをリフレクションで列挙する）。</summary>
    /// <returns>色名 → 色 の辞書（キーは大文字小文字を無視）。</returns>
    /// <remarks>トリマーに <see cref="Colors"/> の静的プロパティが使用されることを伝える（<c>DynamicDependency</c>）。</remarks>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Colors))]
    private static IReadOnlyDictionary<string, Color> BuildColorNames()
        // Colors の公開静的プロパティのうち Color 型のみを列挙する
        // （`is` 判定の防御分岐を作らず、PropertyType の完全一致でフィルタする）
        => typeof(Colors)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Color))
            .ToDictionary(p => p.Name, p => (Color)p.GetValue(null)!, StringComparer.OrdinalIgnoreCase);

    /// <summary>色名辞書から色 → 色名 の逆引き辞書を構築する。</summary>
    /// <param name="names">色名 → 色 の辞書。</param>
    /// <returns>ARGB 完全一致の辞書と RGB 一致（アルファ無視）の辞書の組。</returns>
    /// <remarks>
    /// 純粋関数として分離し、テストから任意データで全分岐を検証できるようにする。<br/>
    /// RGB 一致辞書は「不透明（A=255）を優先、両方不透明なら先勝ち、両方半透明なら先勝ち」で登録する。<br/>
    /// テスト（EsUtil.Ui.AvaloniaUI.ColorPicker.Tests）が全分岐（新規 RGB / 不透明優先 / 先勝ち）を検証するため internal 公開する。
    /// </remarks>
    internal static (Dictionary<Color, string> Exact, Dictionary<(byte R, byte G, byte B), string> Rgb) BuildReverseLookups(
        IReadOnlyDictionary<string, Color> names)
    {
        var exact = new Dictionary<Color, string>(names.Count);
        var rgb = new Dictionary<(byte R, byte G, byte B), string>(names.Count);

        foreach (var (name, color) in names)
        {
            // ARGB 完全一致は最初に見つかった色名を保持する（TryAdd で重複を安全に無視）
            exact.TryAdd(color, name);

            var key = (color.R, color.G, color.B);
            if (rgb.TryGetValue(key, out var existingName))
            {
                var existingIsOpaque = names[existingName].A == 255;
                if (existingIsOpaque || color.A != 255)
                {
                    // 既存が不透明（先勝ち）または新規が半透明（既存を維持）の場合は登録しない
                    continue;
                }
            }
            rgb[key] = name;
        }

        return (exact, rgb);
    }

    /// <summary>色名（英名）の辞書（<see cref="Colors"/> の全色名 → 色）。</summary>
    /// <remarks>
    /// <see cref="ColorEntry"/> が同一の色一覧を再利用するための内部公開（リフレクション走査の重複排除）。<br/>
    /// キーは大文字小文字を無視する比較（OrdinalIgnoreCase）で保持される。
    /// </remarks>
    internal static IReadOnlyDictionary<string, Color> ColorNames => s_colorNames;

    /// <summary>指定された色名が <see cref="Colors"/> に定義されているか判定する。</summary>
    /// <param name="name">色名（例: "Red"）。 大文字小文字は無視。</param>
    /// <returns>定義されている場合は <see langword="true"/>。</returns>
    public static bool IsDefinedColorName(string name)
        => s_colorNames.ContainsKey(name);

    /// <summary>色文字列（色名 or 16進 + 不透明度% の形式）を解析して色と不透明度を取得する。</summary>
    /// <param name="text">解析対象の文字列（例: "Red", "#336699", "Red+50%", "#336699+75%"）。</param>
    /// <param name="color">解析結果の色（アルファは <paramref name="opacity"/> に応じて 0〜255 に合成される）。</param>
    /// <param name="opacity">解析結果の不透明度（0.0〜1.0）。</param>
    /// <returns>解析に成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 不透明度部分（<c>+{n}%</c>）が省略された場合は不透明度 1.0 として扱う。<br/>
    /// 色は <see cref="Color.TryParse(string, out Color)"/> で解釈する（色名・16進・アルファ付き16進）。<br/>
    /// <c>+{n}%</c> が指定された場合、色のアルファを不透明度へ合成する（例: "Red+50%" → <c>#80FF0000</c>）。
    /// </remarks>
    public static bool TryParse(string? text, out Color color, out double opacity)
    {
        color = default;
        opacity = 1.0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        // 解析結果キャッシュ（キーストローク毎の同一文字列の再解析を排除）
        if (s_parseCache.TryGetValue(text, out var cached))
        {
            color = cached.Color;
            opacity = cached.Opacity;
            return cached.Success;
        }

        var success = TryParseCore(text, out color, out opacity);
        AddWithCapacityLimit(s_parseCache, ParseCacheCapacity, text, (success, color, opacity));
        return success;
    }

    /// <summary>色文字列を実際に解析する（キャッシュなしの解析本体）。</summary>
    /// <param name="text">解析対象の文字列（null / 空白は呼び出し元で除外済み）。</param>
    /// <param name="color">解析結果の色（アルファは <paramref name="opacity"/> に応じて 0〜255 に合成される）。</param>
    /// <param name="opacity">解析結果の不透明度（0.0〜1.0）。</param>
    /// <returns>解析に成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <see cref="TryParse"/> からキャッシュを経由して呼ばれる純粋な解析処理。<br/>
    /// 不透明度部分（<c>+{n}%</c>）が省略された場合は不透明度 1.0 として扱う。<br/>
    /// 色は <see cref="Color.TryParse(string, out Color)"/> で解釈する（色名・16進・アルファ付き16進）。<br/>
    /// <c>+{n}%</c> が指定された場合、色のアルファを不透明度へ合成する（例: "Red+50%" → <c>#80FF0000</c>）。
    /// </remarks>
    private static bool TryParseCore(string text, out Color color, out double opacity)
    {
        color = default;
        opacity = 1.0;

        // "+{n}%" の不透明度部分を分離する
        var plusIndex = text.LastIndexOf('+');
        if (plusIndex > 0 && text.EndsWith('%'))
        {
            var colorPart = text[..plusIndex];
            var opacityPart = text[(plusIndex + 1)..^1]; // "%" を除く
            if (int.TryParse(opacityPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pct)
                && pct is >= 0 and <= 100
                && Color.TryParse(colorPart, out var parsed))
            {
                opacity = pct / 100.0;
                // 不透明度をアルファへ合成する（例: "Red+50%" → #80FF0000）
                // pct * 2.55 は浮動小数誤差（例: 50 * 2.55 = 127.4999...）を生むため
                // 整数倍してから除算し、50% → 128（#80）が正確に得られるようにする
                color = WithAlpha(parsed, (byte)Math.Round(pct * 255.0 / 100.0));
                return true;
            }
            return false;
        }

        // 不透明度なし（100%）→ 色のみ
        return Color.TryParse(text, out color);
    }

    /// <summary>色と不透明度を「{色}+{不透明度%}」形式の文字列へ整形する。</summary>
    /// <param name="color">色（RGB。 アルファは無視される）。</param>
    /// <param name="opacity">不透明度（0.0〜1.0）。 1.0 の場合は不透明度部分を省略する。</param>
    /// <returns>整形結果の文字列（例: "Red", "Red+50%", "#336699+75%"）。</returns>
    /// <remarks>
    /// 色名が <see cref="Colors"/> に定義されている場合は色名で、未定義の場合は <c>#RRGGBB</c> で表す。<br/>
    /// 不透明度は 0〜100% の整数に丸め、1.0（100%）の場合は省略する。
    /// </remarks>
    public static string Format(Color color, double opacity)
    {
        var colorPart = FindColorName(color) ?? ToHex(color);
        var pct = (int)Math.Round(Math.Clamp(opacity, 0.0, 1.0) * 100);
        return pct >= 100 ? colorPart : $"{colorPart}+{pct}%";
    }

    /// <summary>保存値（色名 / #RRGGBB / #AARRGGBB / +% 形式）を画面表示用テキスト（{色}+{不透明度%}）へ変換する。</summary>
    /// <param name="text">保存値（例: "#80FF0000", "Red", "Red+50%", "#336699+75%"）。</param>
    /// <returns>表示用テキスト（例: "Red+50%", "Red", "#336699+75%"）。 解析できない場合は入力をそのまま返す。</returns>
    /// <remarks>
    /// ComboBox などの画面表示に使用する。 不透明度は <c>+{n}%</c> 形式で表し、1.0（100%）の場合は省略する。<br/>
    /// <c>+{n}%</c> 形式を含まない入力（#AARRGGBB 等）はアルファ成分を不透明度として解釈する。<br/>
    /// 処理フロー:<br/>
    /// 1. 表示テキストキャッシュを引き、ヒットした場合は即座に返す（整形の毎回実行を排除）。<br/>
    /// 2. <see cref="TryParse"/> で解析し、失敗時は入力をそのまま返す（キャッシュしない）。<br/>
    /// 3. 不透明度を確定して <see cref="Format"/> で整形し、キャッシュへ登録して返す。<br/>
    /// 容量超過時はキャッシュを全クリアしてから登録する。
    /// </remarks>
    public static string ToDisplayText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;

        // 表示テキストキャッシュ（バインド更新毎の整形を排除）
        if (s_displayTextCache.TryGetValue(text, out var cached))
        {
            return cached;
        }

        if (!TryParse(text, out var color, out var opacity)) return text;

        // 不透明度が明示指定されていない場合（+{n}% なし）は、アルファ成分を不透明度として扱う
        if (!text.Contains('+', StringComparison.Ordinal))
        {
            opacity = color.A / 255.0;
        }

        var displayText = Format(color, opacity);
        AddWithCapacityLimit(s_displayTextCache, DisplayTextCacheCapacity, text, displayText);
        return displayText;
    }

    /// <summary>任意の色文字列（色名 / #RRGGBB / #AARRGGBB / +% 形式）を <c>#AARRGGBB</c>（アルファ先頭16進）へ正規化する。</summary>
    /// <param name="text">入力文字列（例: "Red", "Red+50%", "#80FF0000"）。</param>
    /// <returns><c>#AARRGGBB</c> 形式の保存値（例: "#80FF0000"）。 解析できない場合は入力をそのまま返す。</returns>
    /// <remarks>
    /// Settings などの保存値は Avalonia 標準の <c>#AARRGGBB</c>（アルファ先頭）とする。<br/>
    /// 色名・#RRGGBB はアルファ 255（不透明）へ、<c>+{n}%</c> は指定された不透明度へ正規化する。<br/>
    /// 処理フロー:<br/>
    /// 1. 正規化文字列キャッシュを引き、ヒットした場合は即座に返す（16進補間の毎回実行を排除）。<br/>
    /// 2. <see cref="TryParse"/> で解析し、失敗時は入力をそのまま返す（キャッシュしない）。<br/>
    /// 3. <see cref="ToArgbHex(Color)"/> で正規化し、キャッシュへ登録して返す。<br/>
    /// 容量超過時はキャッシュを全クリアしてから登録する。
    /// </remarks>
    public static string ToArgbHex(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;

        // 正規化文字列キャッシュ（ConvertBack のキーストローク毎の補間を排除）
        if (s_normalizedCache.TryGetValue(text, out var cached))
        {
            return cached;
        }

        if (!TryParse(text, out var color, out _)) return text;

        var normalized = ToArgbHex(color);
        AddWithCapacityLimit(s_normalizedCache, NormalizedCacheCapacity, text, normalized);
        return normalized;
    }

    /// <summary>容量制限付きキャッシュへ値を登録する（超過時は全クリアしてから登録）。</summary>
    /// <param name="cache">対象キャッシュ。</param>
    /// <param name="capacity">キャッシュの最大容量。</param>
    /// <param name="key">登録キー。</param>
    /// <param name="value">登録値。</param>
    /// <remarks>
    /// 容量超過時は全クリアして無制限の増加を防ぐ（同一文字列は直後に再キャッシュされる）。<br/>
    /// <see cref="ColorString"/> の 3 キャッシュ（解析 / 表示テキスト / 正規化）と
    /// <see cref="StringToBrushConverter"/> のブラシキャッシュで共有する（容量管理の単一所有・DRY）。
    /// </remarks>
    internal static void AddWithCapacityLimit<T>(
        ConcurrentDictionary<string, T> cache, int capacity, string key, T value)
    {
        if (cache.Count >= capacity)
        {
            cache.Clear();
        }
        cache[key] = value;
    }

    /// <summary>色を <c>#AARRGGBB</c>（アルファ先頭・常時アルファ含む）形式へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns><c>#AARRGGBB</c> 形式の文字列（例: "#80FF0000"）。</returns>
    /// <remarks>
    /// 保存値（<see cref="ColorPickerControl.SelectedColor"/> / Settings）の形式は Avalonia 標準の
    /// <c>#AARRGGBB</c> とする（<see cref="Color.Parse(string)"/> と整合。 不透明度はアルファ成分として統合される）。<br/>
    /// カラーピッカー（ColorView）の OK 確定時など、色オブジェクトを直接保存値へ変換する場合に使用する。
    /// </remarks>
    public static string ToArgbHex(Color color)
        => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>指定された色に対応する色名（<see cref="Colors"/>）を検索する。</summary>
    /// <param name="color">色。</param>
    /// <returns>一致する色名。 未定義の場合は null。</returns>
    /// <remarks>
    /// アルファも含めて完全一致する色名を最優先する（<see cref="s_reverseLookups"/> の O(1) 検索）。<br/>
    /// 完全一致が無い場合は RGB のみで一致する色名を返す（同 RGB で複数ある場合は
    /// 不透明（A=255）の色名が優先登録されている。 例: White と Transparent は同 RGB のため、
    /// 半透明の白は "White" と表示される）。
    /// </remarks>
    private static string? FindColorName(Color color)
    {
        if (s_reverseLookups.Exact.TryGetValue(color, out var exact)) return exact;
        return s_reverseLookups.Rgb.TryGetValue((color.R, color.G, color.B), out var rgb) ? rgb : null;
    }

    /// <summary>色を <c>#RRGGBB</c>（アルファなし16進）へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns><c>#RRGGBB</c> 形式の文字列。</returns>
    private static string ToHex(Color color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>基準色の RGB を保ったまま不透明度だけを差し替える。</summary>
    /// <param name="baseColor">基準色（名前付き色等）。</param>
    /// <param name="alpha">不透明度（0〜255。 255 = 完全不透明）。</param>
    /// <returns>不透明度を適用した色。</returns>
    /// <remarks>
    /// <c>Color.FromArgb(alpha, r, g, b)</c> を直接書くと「何色の何 % か」が読み取りにくいため、
    /// 名前付き色を基準に生成する入口を本クラスが持つ（UI 層は変換ブリッジを参照できない。 `.github/依存関係.md` §3）。
    /// </remarks>
    private static Color WithAlpha(Color baseColor, byte alpha) => new(alpha, baseColor.R, baseColor.G, baseColor.B);
}