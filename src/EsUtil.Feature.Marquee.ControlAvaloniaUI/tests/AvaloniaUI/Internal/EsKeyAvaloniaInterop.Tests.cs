using AvaloniaKey = Avalonia.Input.Key;
using EsUtil.Feature.Marquee.AvaloniaUI.Internal;
using EsUtil.Platform.Core;
using Xunit;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Tests.Internal;

/// <summary><see cref="EsKeyAvaloniaInterop"/> の変換テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// キーの対応は名前ベースのため、ウィンドウ・バックエンドの初期化は不要（<c>[Fact]</c> で並列実行できる）。<br/>
/// <b>本テストは「<see cref="EsKey"/> が <see cref="AvaloniaKey"/> の名前を鏡映している」という契約を固定する</b>:<br/>
/// 集合がずれた場合（Avalonia の更新・<see cref="EsKey"/> の改名）に失敗し、ビルドで気付ける。<br/>
/// 集合の一致は実行時の型情報（<see cref="Enum.GetNames{T}"/>）で比較するため、件数の写し間違いが起きない。<br/>
/// <b>別名</b>: Avalonia 側には同じ値を持つ別名が 29 組ある（実測: 223 名に対し異なる値は 194）ため、<c>EsKey</c> → Avalonia → <c>EsKey</c> は<br/>
/// 代表名（例: <c>Enter</c> → <c>Return</c>）へ正規化される。 <b>キーの値は保たれる</b>ため、これを契約として検証する。
/// </remarks>
public class EsKeyAvaloniaInteropTests
{
    // ====================================================================
    // 名前集合の一致（本テストの中心。 規約をビルドで守らせる）
    // ====================================================================

    [Fact]
    internal void コアの全メンバー名がAvaloniaに存在する()
    {
        var avaloniaNames = Enum.GetNames<AvaloniaKey>().ToHashSet(StringComparer.Ordinal);

        var missing = Enum.GetNames<EsKey>().Where(name => !avaloniaNames.Contains(name)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    internal void Avaloniaの全メンバー名がコアに存在する()
    {
        var coreNames = Enum.GetNames<EsKey>().ToHashSet(StringComparer.Ordinal);

        var missing = Enum.GetNames<AvaloniaKey>().Where(name => !coreNames.Contains(name)).ToList();

        Assert.Empty(missing);
    }

    // ====================================================================
    // 往復変換（キーの値が保たれる）
    // ====================================================================

    [Fact]
    internal void Avaloniaの全キーはコアへ変換して値が保たれる()
    {
        foreach (var key in Enum.GetValues<AvaloniaKey>())
        {
            Assert.Equal(key, key.ToEsKey().ToAvaloniaKey());
        }
    }

    [Fact]
    internal void コアの全キーはAvaloniaの同じキーへ変換される()
    {
        foreach (var name in Enum.GetNames<EsKey>())
        {
            var key = Enum.Parse<EsKey>(name);

            Assert.Equal(Enum.Parse<AvaloniaKey>(name), key.ToAvaloniaKey());
        }
    }

    [Fact]
    internal void コアからAvaloniaへの変換は別名へ正規化された後は安定する()
    {
        foreach (var key in Enum.GetValues<EsKey>())
        {
            var once = key.ToAvaloniaKey();

            Assert.Equal(once, once.ToEsKey().ToAvaloniaKey());
        }
    }

    // ====================================================================
    // 別名（同じ値を持つ組。 実測 29 組）
    // ====================================================================

    [Theory]
    [InlineData(EsKey.Return, EsKey.Enter)]
    [InlineData(EsKey.CapsLock, EsKey.Capital)]
    [InlineData(EsKey.PageUp, EsKey.Prior)]
    [InlineData(EsKey.Snapshot, EsKey.PrintScreen)]
    [InlineData(EsKey.OemTilde, EsKey.Oem3)]
    [InlineData(EsKey.OemBackslash, EsKey.Oem102)]
    internal void 別名のキーは同じAvaloniaキーへ変換される(EsKey left, EsKey right)
        => Assert.Equal(left.ToAvaloniaKey(), right.ToAvaloniaKey());

    // ====================================================================
    // 未定義値（数値文字列の誤受理を防ぐ）
    // ====================================================================

    [Fact]
    internal void 未定義値はNoneへ変換される()
    {
        // `Enum.TryParse` は数値文字列を受け付けるため、名前解決の成否だけでは未定義値を検出できない。
        // `Enum.IsDefined` で検査しているため、列挙に無い数値は None になる。
        Assert.Equal(AvaloniaKey.None, ((EsKey)9999).ToAvaloniaKey());
        Assert.Equal(EsKey.None, ((AvaloniaKey)9999).ToEsKey());
    }

    // ====================================================================
    // 代表的なキー（変換の向きが逆になっていないことの確認）
    // ====================================================================

    [Theory]
    [InlineData(EsKey.Escape, AvaloniaKey.Escape)]
    [InlineData(EsKey.A, AvaloniaKey.A)]
    [InlineData(EsKey.D0, AvaloniaKey.D0)]
    [InlineData(EsKey.F12, AvaloniaKey.F12)]
    [InlineData(EsKey.LeftCtrl, AvaloniaKey.LeftCtrl)]
    [InlineData(EsKey.OemTilde, AvaloniaKey.Oem3)]
    [InlineData(EsKey.VolumeMute, AvaloniaKey.VolumeMute)]
    [InlineData(EsKey.ImeConvert, AvaloniaKey.ImeConvert)]
    internal void 代表的なキーは対応するAvaloniaキーへ変換される(EsKey key, AvaloniaKey expected)
    {
        Assert.Equal(expected, key.ToAvaloniaKey());

        // 逆方向は別名の代表名へ正規化される（例: OemTilde → Oem3）ため、キーの値が保たれることを検証する
        Assert.Equal(expected, expected.ToEsKey().ToAvaloniaKey());
    }
}
