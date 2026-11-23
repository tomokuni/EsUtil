using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.MewUI.Annotation.Tests;

/// <summary>テストで共用するユーティリティ（バックエンドの登録とテスト画像の生成）。</summary>
internal static class TestHelper
{
    /// <summary>バックエンドを登録済みかどうか。</summary>
    private static bool s_registered;

    /// <summary>テストの実行前にバックエンドを登録する（テキストの実寸測定もバックエンドを必要とするため）。</summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize() => _ = Factory;

    /// <summary>グラフィックス ファクトリを取得する（初回に GDI バックエンドを登録する）。</summary>
    /// <remarks>画素を読み戻せるのは GDI の CPU 表面だけである（合成画像の検証に必要）。</remarks>
    public static IGraphicsFactory Factory
    {
        get
        {
            if (!s_registered)
            {
                GdiBackend.Register();
                s_registered = true;
            }

            return Application.DefaultGraphicsFactory;
        }
    }

    /// <summary>指定サイズの単色画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="color">塗りつぶす色。</param>
    /// <returns>生成した画像（読み取り専用の面）。</returns>
    public static EsReadOnlyBitmap CreateBitmap(int width, int height, DrawingColor color)
    {
        using var created = EsBitmap.Create(width, height, color);
        created.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(created);
    }
}
