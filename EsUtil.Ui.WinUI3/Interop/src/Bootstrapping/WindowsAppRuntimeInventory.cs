using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core;
using SdkRuntimeVersion = Microsoft.WindowsAppSDK.Runtime.Version;
using SdkPackages = Microsoft.WindowsAppSDK.Runtime.Packages;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.System;

namespace EsUtil.Ui.WinUI3.Interop.Bootstrapping;

/// <summary>インストール済みの Windows App Runtime の版を調べる窓口。</summary>
/// <remarks>
/// <b>何のためか</b>: ランタイムを導入する前に「入っているか」「どの版か」をアプリが確かめられるようにする。<br/>
/// アプリは本クラスで状態を調べ、**利用者へ確認してから** <see cref="WindowsAppRuntimeBootstrap.TryInstall"/> で導入する<br/>
/// （確認なしにインストールを始めない。 導入の入口は <see cref="WindowsAppRuntimeBootstrap"/> が単一所有する）。<br/>
/// <br/>
/// <b>何を「インストール済み」とするか</b>: Windows App Runtime のフレームワーク パッケージのうち、
/// <b>このプロセスのアーキテクチャで動くもの</b>だけを返す（x64 のアプリは x64 と中立のパッケージを見る）。
/// 同じ版が x86 と x64 に入っていても 1 件にしか数えない。<br/>
/// <br/>
/// <b>必要とする版</b>: <see cref="RequiredVersion"/> は参照している <c>Microsoft.WindowsAppSDK</c> のパッケージが決める
/// （ライブラリ側に版を書かない）。 EsUtil の WinUI 3 版は <b>Windows App Runtime 2.6 以降</b>で動作し、
/// このビルドが要求する下限はそのうち <b>パッケージの版（2.6.4）</b>である
/// （Bootstrapper の最小版と同じ値であるため、本クラスの判定と <see cref="WindowsAppRuntimeBootstrap.TryInitialize(out int)"/> の結果は一致する）。<br/>
/// <br/>
/// <b>自己完結の配置</b>（<c>WindowsAppSDKSelfContained=true</c>）と <b>MSIX</b> はランタイムを同封しているため、
/// 本クラスの判定が false でも動作する（初期化が不要かどうかは <see cref="WindowsAppRuntimeBootstrap.IsInitialized"/> が表す）。<br/>
/// <br/>
/// <b>調べられない場合</b>: ポリシーや COM の初期化失敗でパッケージを列挙できないときは<b>空</b>を返す
/// （例外にしない。 調べるだけの操作でアプリを止めない）。 呼び出し側は「入っていない」として扱える。
/// </remarks>
public static class WindowsAppRuntimeInventory
{
    /// <summary>このビルドが必要とする Windows App Runtime の版を取得する。</summary>
    /// <value>必要とする下限の版（参照している <c>Microsoft.WindowsAppSDK</c> のランタイム版。 例: <c>2.6.4.0</c>）。</value>
    /// <remarks>
    /// <b>定義</b>: Windows App Runtime 2.6 以降のうち、EsUtil の WinUI 3 版が動作する版である。<br/>
    /// 値は <see cref="WindowsAppRuntimeBootstrap"/> が Bootstrapper へ渡す最小版と同じであり、
    /// パッケージの版を上げれば両方が追随する。
    /// </remarks>
    public static Version RequiredVersion { get; } = new(
        SdkRuntimeVersion.Major, SdkRuntimeVersion.Minor, SdkRuntimeVersion.Build, SdkRuntimeVersion.Revision);

    /// <summary>インストール済みの版を返す（新しい順）。</summary>
    /// <returns>このプロセスのアーキテクチャで動く版。 1 件も無い（または調べられない）場合は空。</returns>
    /// <remarks>
    /// 列挙は <see cref="PackageManager.FindPackagesForUser(string, string)"/> を使う（パッケージ ID を持たない
    /// アプリからも実行できる。 実測 2026-11-05）。 版は同じ版が複数あっても 1 件にまとめる。<br/>
    /// 調べるたびに OS へ問い合わせる（アプリの起動時に 1 回呼ぶ用途を想定し、結果を保持しない）。
    /// </remarks>
    public static IReadOnlyList<Version> GetInstalledVersions()
    {
        var versions = new List<Version>();
        try
        {
            var manager = new PackageManager();
            foreach (var package in manager.FindPackagesForUser(string.Empty, SdkPackages.Framework.PackageFamilyName))
            {
                if (!IsCompatible(package.Id.Architecture))
                {
                    continue;
                }

                var version = ToVersion(package.Id.Version);
                if (!versions.Contains(version))
                {
                    versions.Add(version);
                }
            }
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 列挙できない（ポリシーで禁止・COM の初期化失敗）ときは「入っていない」と同じ扱いにする。
            // 例外にすると、起動時の確認だけでアプリが止まる（導入できるかは別の判断である）
            return [];
        }

        // 新しい順に並べる（呼び出し側が先頭を最新として使えるようにする）
        versions.Sort();
        versions.Reverse();
        return versions;
    }

    /// <summary>インストール済みの最新の版を返す。</summary>
    /// <param name="version">最新の版。 1 件も無い（または調べられない）場合は null。</param>
    /// <returns>1 件以上あれば true。</returns>
    /// <remarks>版を 1 つだけ表示する用途の入口である（<see cref="GetInstalledVersions"/> の先頭と同じ値）。</remarks>
    public static bool TryGetInstalledVersion([NotNullWhen(true)] out Version? version)
    {
        var versions = GetInstalledVersions();
        version = versions.Count > 0 ? versions[0] : null;
        return version is not null;
    }

    /// <summary>指定の版以上がインストールされているかを返す。</summary>
    /// <param name="minimumVersion">下限の版。 null 不可。</param>
    /// <returns>下限以上の版が 1 件でもあれば true。 調べられない場合は false。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="minimumVersion"/> が null の場合。</exception>
    public static bool IsVersionInstalled(Version minimumVersion)
    {
        ArgumentNullException.ThrowIfNull(minimumVersion);

        foreach (var installed in GetInstalledVersions())
        {
            if (installed >= minimumVersion)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>このビルドが必要とする版（<see cref="RequiredVersion"/>）がインストールされているかを返す。</summary>
    /// <returns>必要とする版以上が入っていれば true。 調べられない場合は false。</returns>
    /// <remarks>
    /// <b>使い方</b>: 起動の早い段階で本メソッドを呼び、false のときだけ利用者へ確認してから
    /// <see cref="WindowsAppRuntimeBootstrap.TryEnsureAvailable"/>（または <see cref="WindowsAppRuntimeBootstrap.TryInstall"/>）
    /// を呼ぶ。 自己完結の配置と MSIX では必要ないため、本メソッドの結果だけで動作可否を判断しない。
    /// </remarks>
    public static bool IsRequiredVersionInstalled() => IsVersionInstalled(RequiredVersion);

    /// <summary>パッケージのアーキテクチャがこのプロセスで動くかを返す。</summary>
    /// <param name="architecture">パッケージのアーキテクチャ。</param>
    /// <returns>動く場合は true。</returns>
    /// <remarks>中立のパッケージはどのアーキテクチャでも動く（ランタイムのフレームワーク パッケージは通常は中立ではない）。</remarks>
    private static bool IsCompatible(ProcessorArchitecture architecture) => architecture switch
    {
        ProcessorArchitecture.Neutral => true,
        ProcessorArchitecture.X86 => RuntimeInformation.ProcessArchitecture == Architecture.X86,
        ProcessorArchitecture.X64 => RuntimeInformation.ProcessArchitecture == Architecture.X64,
        ProcessorArchitecture.Arm => RuntimeInformation.ProcessArchitecture == Architecture.Arm,
        ProcessorArchitecture.Arm64 => RuntimeInformation.ProcessArchitecture == Architecture.Arm64,
        // X86OnArm64 は ARM64 上の x86 プロセスで動く（このプロセスのアーキテクチャが x86 のときだけ該当する）
        ProcessorArchitecture.X86OnArm64 => RuntimeInformation.ProcessArchitecture == Architecture.X86,
        _ => false,
    };

    /// <summary>パッケージの版を <see cref="Version"/> へ変換する。</summary>
    /// <param name="version">パッケージの版。</param>
    /// <returns>変換した版。</returns>
    private static Version ToVersion(PackageVersion version)
        => new(version.Major, version.Minor, version.Build, version.Revision);
}
