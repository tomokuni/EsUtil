using System.Reflection;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.Skia;
using SkiaSharp;


namespace EsUtil.Feature.Loupe.MewUI.Tests;

/// <summary>テストで共用するユーティリティ。</summary>
internal static class TestHelper
{
    /// <summary><c>MewObject.SetValue</c> のジェネリック定義（protected のため 1 度だけ解決して再利用する）。</summary>
    /// <remarks>名称は <c>nameof</c> で参照できない（protected のためアクセス違反になる）。</remarks>
    private static readonly MethodInfo s_setValue = typeof(MewObject)
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .First(method => string.Equals(method.Name, "SetValue", StringComparison.Ordinal) && method.IsGenericMethodDefinition && method.GetParameters().Length == 2);

    /// <summary>プロパティの setter を経由せずに値を設定する（MewUI のバインディング基盤と同じ経路の検証用）。</summary>
    /// <typeparam name="T">プロパティの値型。</typeparam>
    /// <param name="target">設定先のオブジェクト。</param>
    /// <param name="property">設定するプロパティ。</param>
    /// <param name="value">設定する値。</param>
    /// <remarks><c>SetValue</c> は protected のためリフレクションで呼ぶ（バインディングが setter を通らないことの再現）。</remarks>
    public static void SetValueWithoutSetter<T>(MewObject target, MewProperty<T> property, T value)
        => s_setValue.MakeGenericMethod(typeof(T)).Invoke(target, [property, value]);

    /// <summary>指定サイズの画像を生成する（不透明の単色で塗りつぶし）。</summary>
    /// <param name="size">一辺のサイズ（ピクセル）。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像。 所有権は呼び出し側。</returns>
    public static EsBitmap CreateBitmap(int size, SKColor color)
    {
        // 所有権は FromSKBitmap へ移るため using を付けない
        var bitmap = new SKBitmap(size, size);
        bitmap.Erase(color);

        return bitmap.ToEsBitmap();
    }

    /// <summary>元画像の中央を区別できるテスト画像を生成する（中心 1 画素のみ別色）。</summary>
    /// <param name="size">一辺のサイズ（ピクセル）。 奇数を推奨。</param>
    /// <param name="background">背景色。</param>
    /// <param name="center">中心画素の色。</param>
    /// <returns>生成した画像。 所有権は呼び出し側。</returns>
    public static EsBitmap CreateBitmapWithCenter(int size, SKColor background, SKColor center)
    {
        // 所有権は FromSKBitmap へ移るため using を付けない
        var bitmap = new SKBitmap(size, size);
        bitmap.Erase(background);
        bitmap.SetPixel(size / 2, size / 2, center);

        return bitmap.ToEsBitmap();
    }
}

