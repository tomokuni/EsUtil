using System.ComponentModel;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Windows.Graphics.Capture（WGC）で画面をキャプチャする <see cref="ICaptureService"/> の実装。</summary>
/// <remarks>
/// <b>GDI との違い</b>: GDI の <c>BitBlt</c> は画面へ描かれた結果をコピーするため、他のウィンドウに隠れた領域は写らない。
/// WGC は合成前の内容を取得できるため、<b>隠れていない状態の画面</b>を撮れる（実測 2026-11-14）。
/// HDR の色空間にも追従する。<br/>
/// <br/>
/// <b>必ず GDI へ退避できる</b>: WGC は GPU とセッションに依存するため、失敗する状況（リモート セッション・
/// 保護コンテンツ・複数モニターに跨る領域）がある。 本クラスは<b>撮影できないと判断したときは GDI の実装へ委譲</b>する
/// （利用者の操作を失敗させない）。<br/>
/// <br/>
/// <b>カーソルと枠</b>: <c>IsCursorCaptureEnabled</c>（1903）と <c>IsBorderRequired</c>（Windows 11）は
/// 対象フレームワーク 17763 の射影に無いため、<b>IID を直接問い合わせて</b>呼ぶ（射影の版に依存しない）。
/// 使えない環境では OS の既定（カーソルを含む・枠を描く）になる。 **カーソルを写さない設定で 1903 未満の場合は GDI へ委譲する**<br/>
/// （WGC ではカーソルを外せないため、そのまま撮ると設定に反する）。<br/>
/// <br/>
/// <b>Direct3D の宣言だけは CsWin32 を使わない</b>: <c>d3d11.dll</c> は Win32 メタデータに含まれないため、
/// 本クラスが <c>[LibraryImport]</c> で宣言する（[`.github/ビルド設定規約.md`](../../../../.github/ビルド設定規約.md) §7 の例外）。
/// </remarks>
public sealed unsafe partial class WinGraphicsCaptureService(bool includeCursor = true, bool hideBorder = true) : ICaptureService
{
    /// <summary><c>ID3D11Texture2D</c> の IID（撮影フレームの表面をテクスチャとして読む）。</summary>
    private static readonly Guid s_iidTexture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    /// <summary><c>IDXGIDevice</c> の IID（D3D11 のデバイスから WinRT のデバイスを作る）。</summary>
    private static readonly Guid s_iidDxgiDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");

    /// <summary><c>IGraphicsCaptureItemInterop</c> の IID（ファクトリからモニター・ウィンドウの項目を作る）。</summary>
    private static readonly Guid s_iidCaptureItemInterop = new("3628e81b-3cac-4c60-b7f4-23ce0e0c3356");

    /// <summary><c>IGraphicsCaptureSession2</c> の IID（<c>IsCursorCaptureEnabled</c> を持つ）。</summary>
    private static readonly Guid s_iidSession2 = new("2c39ae40-7d2e-5044-804e-8b6799d4cf9e");

    /// <summary><c>IGraphicsCaptureSession3</c> の IID（<c>IsBorderRequired</c> を持つ）。</summary>
    private static readonly Guid s_iidSession3 = new("f2cdd966-22ae-5ea1-9596-3a289344c3be");

    /// <summary><c>ID3D11Device::CreateTexture2D</c> の vtable 位置。</summary>
    private const int CreateTexture2DIndex = 5;

    /// <summary><c>ID3D11DeviceContext::Map</c> の vtable 位置。</summary>
    private const int MapIndex = 14;

    /// <summary><c>ID3D11DeviceContext::Unmap</c> の vtable 位置。</summary>
    private const int UnmapIndex = 15;

    /// <summary><c>ID3D11DeviceContext::CopyResource</c> の vtable 位置。</summary>
    private const int CopyResourceIndex = 47;

    /// <summary>1 枚の取得で待つ最大時間。</summary>
    private const int FrameTimeoutMilliseconds = 2000;

    /// <summary>Direct3D のデバイス（プロセスで 1 つを使い回す）。</summary>
    /// <remarks>
    /// <b>使い回す理由</b>: デバイスの作成と WinRT への変換が撮影 1 回の費用の大半を占める
    /// （実測 2026-11-14: 400x300 の 1 枚で 276ms。 2 回目以降は 30ms 程度になる）。
    /// D3D11 のデバイスはスレッドセーフ（<c>SINGLETHREADED</c> を付けない）ため、撮影のたびに作り直さない。
    /// </remarks>
    private static readonly Lock s_deviceLock = new();

    /// <summary>キャッシュした Direct3D のデバイス。</summary>
    private static IntPtr s_device;

    /// <summary>キャッシュした Direct3D のデバイス コンテキスト。</summary>
    private static IntPtr s_context;

    /// <summary>キャッシュした WinRT のデバイス（WGC が要求する型）。</summary>
    private static IDirect3DDevice? s_winrtDevice;

    /// <summary>撮影できないときに委譲する GDI の実装。</summary>
    private readonly WinCaptureService _fallback = new();

    /// <inheritdoc/>
    /// <remarks>
    /// 指定領域を包含するモニターが 1 つに定まるときだけ WGC を使う（複数モニターに跨る領域は
    /// モニター単位でしか撮れないため GDI へ委譲する）。
    /// </remarks>
    public EsBitmap CaptureArea(EsRectInt bounds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bounds.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bounds.Height);

        // CA1416 が理解できる形（単独条件の早期 return）でガードを置く。
        // WGC のフレーム プール（CreateFreeThreaded）は 1809 以降でのみ呼べる
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return _fallback.CaptureArea(bounds);
        }

        // WGC がカーソルを制御できない環境（1903 未満）で「カーソルを写さない」設定なら、
        // カーソルを合成しない GDI で撮る（WGC では常に写るため、設定を無視することになる）
        if (!includeCursor && !WindowsGraphicsCaptureSupport.IsCursorCaptureSupported)
        {
            return _fallback.CaptureArea(bounds);
        }
        // 版に加えて、GPU・セッションの状態も含めた対応を確かめる
        if (!WindowsGraphicsCaptureSupport.IsDisplayCaptureSupported)
        {
            return _fallback.CaptureArea(bounds);
        }

        try
        {
            if (TryCaptureMonitor(bounds, out var captured) && captured is not null)
            {
                return captured;
            }
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // WGC の失敗は利用者の操作を止めない（GDI で取り直す）
        }

        return _fallback.CaptureArea(bounds);
    }

    /// <inheritdoc/>
    /// <remarks>仮想スクリーンは複数モニターに跨るため GDI へ委譲する（WGC はモニター単位でしか撮れない）。</remarks>
    public EsBitmap? CaptureVirtualScreen(out EsPointInt origin) => _fallback.CaptureVirtualScreen(out origin);

    /// <summary>指定領域を包含するモニターを WGC で撮り、領域へ切り出す。</summary>
    /// <param name="bounds">撮影する領域（画面物理座標）。</param>
    /// <param name="result">切り出した画像。 撮影できない場合は null。</param>
    /// <returns>WGC で撮影できた場合は true。</returns>
    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
    private bool TryCaptureMonitor(EsRectInt bounds, out EsBitmap? result)
    {
        result = null;

        var monitor = PInvoke.MonitorFromPoint(
            new System.Drawing.Point(bounds.X + 1, bounds.Y + 1), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (monitor.IsNull)
        {
            return false;
        }

        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!PInvoke.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        var monitorBounds = new EsRectInt(
            info.rcMonitor.left, info.rcMonitor.top,
            info.rcMonitor.right - info.rcMonitor.left,
            info.rcMonitor.bottom - info.rcMonitor.top);

        // モニターの外へはみ出す領域は WGC では撮れない（モニター単位のため）。 GDI に任せる
        if (!Contains(monitorBounds, bounds))
        {
            return false;
        }

        using var full = CaptureMonitorImage(monitor, monitorBounds);
        if (full is null)
        {
            return false;
        }

        result = Crop(full, new EsRectInt(bounds.X - monitorBounds.X, bounds.Y - monitorBounds.Y, bounds.Width, bounds.Height));
        return true;
    }

    /// <summary>破棄できる場合だけ破棄する。</summary>
    /// <param name="value">破棄するオブジェクト。</param>
    /// <remarks>ヘルパーへ切り出すのは、生成前の例外経路（null のまま）を解析器へ正しく伝えるため。</remarks>
    private static void DisposeIfNotNull(IDisposable? value) => value?.Dispose();

    /// <summary>モニターの矩形が指定領域を完全に含むかを返す。</summary>
    /// <param name="outer">モニターの矩形。</param>
    /// <param name="inner">撮影する領域。</param>
    /// <returns>含む場合は true。</returns>
    private static bool Contains(EsRectInt outer, EsRectInt inner)
        => inner.X >= outer.X && inner.Y >= outer.Y
            && inner.X + inner.Width <= outer.X + outer.Width
            && inner.Y + inner.Height <= outer.Y + outer.Height;

    /// <summary>モニターを 1 枚撮り、コアの画像として返す。</summary>
    /// <param name="monitor">撮影するモニター。</param>
    /// <param name="monitorBounds">モニターの矩形（画像の大きさ）。</param>
    /// <returns>撮影した画像。 撮影できない場合は null。</returns>
    /// <exception cref="Win32Exception">Direct3D の初期化またはフレームの取得に失敗した場合。</exception>
    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
    private EsBitmap? CaptureMonitorImage(HMONITOR monitor, EsRectInt monitorBounds)
    {
        Direct3D11CaptureFramePool? framePool = null;
        GraphicsCaptureSession? session = null;
        try
        {
            var winrtDevice = GetSharedDevice();
            var item = CreateItem(monitor);

            framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                winrtDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                new Windows.Graphics.SizeInt32 { Width = monitorBounds.Width, Height = monitorBounds.Height });

            session = framePool.CreateCaptureSession(item);
            ApplySessionFlags(session);
            session.StartCapture();

            var frame = TryGetFrame(framePool);
            if (frame is null)
            {
                return null;
            }

            using (frame)
            {
                return CopyFrame(frame, monitorBounds);
            }
        }
        finally
        {
            DisposeIfNotNull(session);
            DisposeIfNotNull(framePool);
        }
    }

    /// <summary>フレームが届くまで待つ。</summary>
    /// <param name="framePool">フレーム プール。</param>
    /// <returns>届いたフレーム。 待ち時間を超えた場合は null。</returns>
    /// <remarks>
    /// <b>イベントは 1 回で解除する</b>: 同じフレームを 2 回処理しないよう、最初の 1 枚で購読を外す
    /// （解除しないと次の撮影で古いフレームを受け取る）。
    /// </remarks>
    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
    private static Direct3D11CaptureFrame? TryGetFrame(Direct3D11CaptureFramePool framePool)
    {
        using var arrived = new ManualResetEventSlim(false);
        Direct3D11CaptureFrame? received = null;

        void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            received ??= sender.TryGetNextFrame();
            arrived.Set();
        }

        framePool.FrameArrived += OnFrameArrived;
        try
        {
            return arrived.Wait(FrameTimeoutMilliseconds) ? received : null;
        }
        finally
        {
            framePool.FrameArrived -= OnFrameArrived;
        }
    }

    /// <summary>撮影フレームをコアの画像へコピーする（GPU の表面 → ステージング テクスチャ → CPU）。</summary>
    /// <param name="frame">撮影フレーム。</param>
    /// <param name="monitorBounds">モニターの矩形（画像の大きさ）。</param>
    /// <returns>コピーした画像。</returns>
    /// <exception cref="Win32Exception">テクスチャの作成または読み戻しに失敗した場合。</exception>
    /// <remarks>
    /// <b>GPU の表面は直接読めない</b>: CPU から読める形式（<c>D3D11_USAGE_STAGING</c>）のテクスチャへ
    /// <c>CopyResource</c> し、<c>Map</c> で読み取る。 行の幅（<c>RowPitch</c>）は画像の幅と一致しないことがあるため、
    /// 行ごとにコピーする。
    /// </remarks>
    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
    private static EsBitmap CopyFrame(Direct3D11CaptureFrame frame, EsRectInt monitorBounds)
    {
        var surface = frame.Surface;
        var inspectable = WinRT.MarshalInterface<IDirect3DSurface>.FromManaged(surface);
        try
        {
            var hr = Marshal.QueryInterface(inspectable, in s_iidTexture2D, out var texture);
            if (hr < 0)
            {
                throw new Win32Exception(hr, "撮影した表面をテクスチャとして取得できませんでした。");
            }

            var staging = IntPtr.Zero;
            try
            {
                staging = CreateStagingTexture(monitorBounds.Width, monitorBounds.Height);
                CopyResource(staging, texture);

                var target = EsBitmap.Create(monitorBounds.Width, monitorBounds.Height);
                if (!MapAndCopy(staging, target))
                {
                    target.Dispose();
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "撮影した画像を読み出せませんでした。");
                }

                return target;
            }
            finally
            {
                if (staging != IntPtr.Zero)
                {
                    Marshal.Release(staging);
                }

                Marshal.Release(texture);
            }
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    /// <summary>CPU から読めるステージング テクスチャを作る。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <returns>作成したテクスチャ。</returns>
    /// <exception cref="Win32Exception">作成に失敗した場合。</exception>
    private static IntPtr CreateStagingTexture(int width, int height)
    {
        var description = new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = FormatBgra8,
            SampleCount = 1,
            SampleQuality = 0,
            Usage = UsageStaging,
            BindFlags = 0,
            CpuAccessFlags = CpuAccessRead,
            MiscFlags = 0,
        };

        var device = GetSharedDeviceHandle();
        var vtable = *(IntPtr**)device;
        var create = (delegate* unmanaged[Stdcall]<IntPtr, Texture2DDescription*, IntPtr, IntPtr*, int>)vtable[CreateTexture2DIndex];
        IntPtr texture;
        var hr = create(device, &description, IntPtr.Zero, &texture);
        if (hr < 0 || texture == IntPtr.Zero)
        {
            throw new Win32Exception(hr, "読み戻し用のテクスチャを作成できませんでした。");
        }

        return texture;
    }

    /// <summary>テクスチャの内容を別のテクスチャへコピーする。</summary>
    /// <param name="destination">コピー先。</param>
    /// <param name="source">コピー元。</param>
    private static void CopyResource(IntPtr destination, IntPtr source)
    {
        var context = GetSharedContextHandle();
        var vtable = *(IntPtr**)context;
        var copy = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, void>)vtable[CopyResourceIndex];
        copy(context, destination, source);
    }

    /// <summary>ステージング テクスチャを読み取り、コアの画像へ写す。</summary>
    /// <param name="texture">ステージング テクスチャ。</param>
    /// <param name="target">コピー先の画像。</param>
    /// <returns>成功した場合は true。</returns>
    /// <remarks>
    /// <b>行の幅を尊重する</b>: <c>RowPitch</c> は 256 バイト境界へ揃えられるため、画像の幅 × 4 バイトとは一致しない。
    /// 行ごとにコピーし、アルファを不透明へ揃える（撮影した画面はアルファを持たない）。
    /// BGRA の並びはコアの画像と同じである。
    /// </remarks>
    private static bool MapAndCopy(IntPtr texture, EsBitmap target)
    {
        var context = GetSharedContextHandle();
        var vtable = *(IntPtr**)context;
        var map = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, uint, uint, MappedSubresource*, int>)vtable[MapIndex];
        var unmap = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, void>)vtable[UnmapIndex];

        MappedSubresource mapped;
        var hr = map(context, texture, 0, MapRead, 0, &mapped);
        if (hr < 0 || mapped.Data is null)
        {
            return false;
        }

        try
        {
            var destination = target.Pixels;
            var rowBytes = target.Width * EsReadOnlyBitmap.BytesPerPixel;
            if (rowBytes > (int)mapped.RowPitch)
            {
                return false;
            }

            for (var row = 0; row < target.Height; row++)
            {
                var source = new ReadOnlySpan<byte>(
                    (byte*)mapped.Data + (row * (int)mapped.RowPitch), rowBytes);
                var offset = row * rowBytes;
                source.CopyTo(destination.Slice(offset, rowBytes));

                for (var alpha = offset + EsReadOnlyBitmap.BytesPerPixel - 1;
                    alpha < offset + rowBytes;
                    alpha += EsReadOnlyBitmap.BytesPerPixel)
                {
                    destination[alpha] = byte.MaxValue;
                }
            }

            return true;
        }
        finally
        {
            unmap(context, texture, 0);
        }
    }

    /// <summary>撮影した画像から指定の矩形を切り出す。</summary>
    /// <param name="source">切り出し元の画像。</param>
    /// <param name="region">切り出す矩形（元の画像の座標）。</param>
    /// <returns>切り出した画像。</returns>
    /// <remarks>切り出し元は呼び出し側が破棄する（本メソッドは新しい画像を返す）。</remarks>
    private static EsBitmap Crop(EsBitmap source, EsRectInt region)
    {
        var target = EsBitmap.Create(region.Width, region.Height);
        try
        {
            var sourcePixels = source.Pixels;
            var targetPixels = target.Pixels;
            var sourceStride = source.Width * EsReadOnlyBitmap.BytesPerPixel;
            var targetStride = target.Width * EsReadOnlyBitmap.BytesPerPixel;

            for (var row = 0; row < region.Height; row++)
            {
                var offset = ((region.Y + row) * sourceStride) + (region.X * EsReadOnlyBitmap.BytesPerPixel);
                sourcePixels.Slice(offset, targetStride).CopyTo(targetPixels.Slice(row * targetStride, targetStride));
            }

            return target;
        }
        catch
        {
            // 切り出しに失敗したら途中まで確保した画像を解放して呼び出し元へ伝える
            // （本層はログを出力しないため、記録は呼び出し側の層が行う）
            target.Dispose();
            throw;
        }
    }

    /// <summary>使い回す WinRT の Direct3D デバイスを取得する（無ければ作る）。</summary>
    /// <returns>WinRT のデバイス。</returns>
    /// <exception cref="Win32Exception">作成に失敗した場合。</exception>
    /// <remarks>
    /// <b>プロセスで 1 つだけ作る</b>: デバイスの作成と WinRT への変換が撮影 1 回の費用の大半を占めるため、
    /// 2 回目以降は使い回す（実測 2026-11-14: 276ms → 30ms 程度）。
    /// D3D11 のデバイスはスレッドセーフ（<c>SINGLETHREADED</c> を付けない）なので、撮影スレッドをまたいで共有できる。
    /// </remarks>
    private static IDirect3DDevice GetSharedDevice()
    {
        lock (s_deviceLock)
        {
            if (s_winrtDevice is null)
            {
                s_device = CreateD3D11Device(out s_context);
                s_winrtDevice = CreateWinRTDevice(s_device);
            }

            return s_winrtDevice;
        }
    }

    /// <summary>使い回している D3D11 のデバイスを取得する。</summary>
    /// <returns>デバイス。</returns>
    private static IntPtr GetSharedDeviceHandle()
    {
        lock (s_deviceLock)
        {
            if (s_device == IntPtr.Zero)
            {
                s_device = CreateD3D11Device(out s_context);
                s_winrtDevice = CreateWinRTDevice(s_device);
            }

            return s_device;
        }
    }

    /// <summary>使い回している D3D11 のデバイス コンテキストを取得する。</summary>
    /// <returns>デバイス コンテキスト。</returns>
    private static IntPtr GetSharedContextHandle()
    {
        _ = GetSharedDeviceHandle();
        return s_context;
    }
    /// <summary>D3D11 のデバイスとコンテキストを作る。</summary>
    /// <param name="context">作成したデバイス コンテキスト。</param>
    /// <returns>作成したデバイス。</returns>
    /// <exception cref="Win32Exception">作成に失敗した場合。</exception>
    /// <remarks>ハードウェア（GPU）を要求する。 失敗した場合は呼び出し側が GDI へ退避する。</remarks>
    private static IntPtr CreateD3D11Device(out IntPtr context)
    {
        var hr = D3D11CreateDevice(
            IntPtr.Zero, DriverTypeHardware, IntPtr.Zero, 0, IntPtr.Zero, 0, 7,
            out var device, out _, out context);
        if (hr < 0 || device == IntPtr.Zero)
        {
            throw new Win32Exception(hr, "Direct3D のデバイスを作成できませんでした。");
        }

        return device;
    }

    /// <summary>D3D11 のデバイスから WinRT の <see cref="IDirect3DDevice"/> を作る。</summary>
    /// <param name="device">D3D11 のデバイス。</param>
    /// <returns>WinRT のデバイス。</returns>
    /// <exception cref="Win32Exception">作成に失敗した場合。</exception>
    private static IDirect3DDevice CreateWinRTDevice(IntPtr device)
    {
        var hr = Marshal.QueryInterface(device, in s_iidDxgiDevice, out var dxgi);
        if (hr < 0)
        {
            throw new Win32Exception(hr, "Direct3D のデバイスを DXGI として取得できませんでした。");
        }

        try
        {
            hr = CreateDirect3D11DeviceFromDXGIDevice(dxgi, out var inspectable);
            if (hr < 0 || inspectable == IntPtr.Zero)
            {
                throw new Win32Exception(hr, "WinRT の Direct3D デバイスを作成できませんでした。");
            }

            try
            {
                return WinRT.MarshalInspectable<IDirect3DDevice>.FromAbi(inspectable);
            }
            finally
            {
                Marshal.Release(inspectable);
            }
        }
        finally
        {
            Marshal.Release(dxgi);
        }
    }

    /// <summary>モニターの撮影項目を作る。</summary>
    /// <param name="monitor">撮影するモニター。</param>
    /// <returns>撮影項目。</returns>
    /// <exception cref="Win32Exception">作成に失敗した場合。</exception>
    /// <remarks>
    /// <c>GraphicsCaptureItem</c> のファクトリは公開されていないため、<c>IGraphicsCaptureItemInterop</c>
    /// （COM の内部インターフェース）を IID で問い合わせて使う。
    /// </remarks>
    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
    private static GraphicsCaptureItem CreateItem(HMONITOR monitor)
    {
        var factory = WinRT.ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        var inspectable = WinRT.MarshalInspectable<object>.FromManaged(factory);
        try
        {
            var hr = Marshal.QueryInterface(inspectable, in s_iidCaptureItemInterop, out var interop);
            if (hr < 0)
            {
                throw new Win32Exception(hr, "撮影項目のファクトリを取得できませんでした。");
            }

            try
            {
                var vtable = *(IntPtr**)interop;
                var createForMonitor = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int>)vtable[4];
                var iidItem = new Guid("79c3f95b-31f7-4ec2-a464-632ef5d30760");
                IntPtr item;
                hr = createForMonitor(interop, (IntPtr)monitor.Value, &iidItem, &item);
                if (hr < 0 || item == IntPtr.Zero)
                {
                    throw new Win32Exception(hr, "撮影項目を作成できませんでした。");
                }

                try
                {
                    return WinRT.MarshalInspectable<GraphicsCaptureItem>.FromAbi(item);
                }
                finally
                {
                    Marshal.Release(item);
                }
            }
            finally
            {
                Marshal.Release(interop);
            }
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    /// <summary>セッションのカーソルと枠の設定を、使える場合だけ適用する。</summary>
    /// <param name="session">撮影セッション。</param>
    /// <remarks>
    /// <b>射影に無いプロパティを IID で呼ぶ</b>: 対象フレームワーク 17763 の射影には
    /// <c>IsCursorCaptureEnabled</c>（1903）と <c>IsBorderRequired</c>（Windows 11）が無い。
    /// 使えない環境では何もしない（OS の既定になる）。
    /// </remarks>
    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
    private void ApplySessionFlags(GraphicsCaptureSession session)
    {
        var inspectable = WinRT.MarshalInspectable<object>.FromManaged(session);
        try
        {
            if (WindowsGraphicsCaptureSupport.IsCursorCaptureSupported)
            {
                SetBoolean(inspectable, s_iidSession2, propertyIndex: 6, includeCursor);
            }

            if (hideBorder && WindowsGraphicsCaptureSupport.IsBorderControlSupported)
            {
                SetBoolean(inspectable, s_iidSession3, propertyIndex: 6, value: false);
            }
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    /// <summary>IID でインターフェースを問い合わせ、真偽値のプロパティを設定する。</summary>
    /// <param name="inspectable">対象のオブジェクト。</param>
    /// <param name="iid">問い合わせるインターフェース。</param>
    /// <param name="propertyIndex">vtable の位置（<c>put_</c> の位置）。</param>
    /// <param name="value">設定する値。</param>
    /// <remarks>
    /// <c>IGraphicsCaptureSession2</c> / <c>3</c> の vtable は
    /// <c>QueryInterface</c>・<c>AddRef</c>・<c>Release</c>・<c>get_</c>・<c>put_</c> の順であり、
    /// <c>put_</c> は 6 番目（0 起点）にある。 設定に失敗しても撮影は続ける（診断ではなく見た目の設定のため）。
    /// </remarks>
    private static void SetBoolean(IntPtr inspectable, in Guid iid, int propertyIndex, bool value)
    {
        var hr = Marshal.QueryInterface(inspectable, in iid, out var target);
        if (hr < 0 || target == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var vtable = *(IntPtr**)target;
            var put = (delegate* unmanaged[Stdcall]<IntPtr, byte, int>)vtable[propertyIndex];
            _ = put(target, value ? (byte)1 : (byte)0);
        }
        finally
        {
            Marshal.Release(target);
        }
    }

    /// <summary>BGRA8 の画素形式（DXGI の値）。</summary>
    private const uint FormatBgra8 = 87;

    /// <summary>ステージング（CPU から読める）用途。</summary>
    private const uint UsageStaging = 3;

    /// <summary>CPU からの読み取りを許可する。</summary>
    private const uint CpuAccessRead = 0x20000;

    /// <summary>読み取り用のマップ。</summary>
    private const uint MapRead = 1;

    /// <summary>ハードウェア（GPU）のドライバー種別。</summary>
    private const int DriverTypeHardware = 1;

    /// <summary><c>D3D11_TEXTURE2D_DESC</c>（44 バイト）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Texture2DDescription
    {
        /// <summary>幅。</summary>
        public uint Width;

        /// <summary>高さ。</summary>
        public uint Height;

        /// <summary>ミップ レベルの数。</summary>
        public uint MipLevels;

        /// <summary>配列の要素数。</summary>
        public uint ArraySize;

        /// <summary>画素形式。</summary>
        public uint Format;

        /// <summary>マルチサンプルの数。</summary>
        public uint SampleCount;

        /// <summary>マルチサンプルの品質。</summary>
        public uint SampleQuality;

        /// <summary>用途。</summary>
        public uint Usage;

        /// <summary>バインド フラグ。</summary>
        public uint BindFlags;

        /// <summary>CPU アクセスのフラグ。</summary>
        public uint CpuAccessFlags;

        /// <summary>その他のフラグ。</summary>
        public uint MiscFlags;
    }

    /// <summary><c>D3D11_MAPPED_SUBRESOURCE</c>。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MappedSubresource
    {
        /// <summary>データへのポインター。</summary>
        public void* Data;

        /// <summary>1 行のバイト数。</summary>
        public uint RowPitch;

        /// <summary>1 面のバイト数。</summary>
        public uint DepthPitch;
    }

    /// <summary><c>D3D11CreateDevice</c>（<c>d3d11.dll</c>）。</summary>
    /// <param name="adapter">アダプター（null で既定）。</param>
    /// <param name="driverType">ドライバーの種別。</param>
    /// <param name="software">ソフトウェア ラスタライザー。</param>
    /// <param name="flags">作成のフラグ。</param>
    /// <param name="featureLevels">要求する機能レベル。</param>
    /// <param name="featureLevelCount">機能レベルの数。</param>
    /// <param name="sdkVersion">SDK の版（7 固定）。</param>
    /// <param name="device">作成したデバイス。</param>
    /// <param name="featureLevel">選ばれた機能レベル。</param>
    /// <param name="context">作成したデバイス コンテキスト。</param>
    /// <returns>HRESULT。</returns>
    [LibraryImport("d3d11.dll")]
    private static partial int D3D11CreateDevice(
        IntPtr adapter, int driverType, IntPtr software, uint flags, IntPtr featureLevels, uint featureLevelCount,
        uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr context);

    /// <summary><c>CreateDirect3D11DeviceFromDXGIDevice</c>（<c>d3d11.dll</c>）。</summary>
    /// <param name="dxgiDevice">DXGI のデバイス。</param>
    /// <param name="graphicsDevice">WinRT のデバイス（<c>IInspectable</c>）。</param>
    /// <returns>HRESULT。</returns>
    [LibraryImport("d3d11.dll")]
    private static partial int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
}
