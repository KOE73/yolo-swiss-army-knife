using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YoloHelperApp.Services;

/// <summary>
/// Process-wide thumbnail cache. Bitmaps are decoded downscaled off the UI thread and
/// held via WeakReference: while a virtualized Image control shows the bitmap it stays
/// alive; once scrolled away the GC may reclaim it and it will be re-decoded on demand.
/// </summary>
public static class ThumbnailCache
{
    private static readonly ConcurrentDictionary<string, WeakReference<Bitmap>> Cache = new();
    // Limit concurrent decodes so a 10k-image folder doesn't saturate CPU/IO
    private static readonly SemaphoreSlim Gate = new(2);

    private static string Key(string path, int width) => $"{width}|{path}";

    public static bool TryGet(string path, int width, out Bitmap? bitmap)
    {
        bitmap = null;
        if (Cache.TryGetValue(Key(path, width), out var weak) && weak.TryGetTarget(out var bmp))
        {
            bitmap = bmp;
            return true;
        }
        return false;
    }

    public static async Task<Bitmap?> LoadAsync(string path, int width)
    {
        if (TryGet(path, width, out var cached)) return cached;

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (TryGet(path, width, out cached)) return cached;
            if (!File.Exists(path)) return null;

            using var stream = File.OpenRead(path);
            var bmp = Bitmap.DecodeToWidth(stream, width);
            Cache[Key(path, width)] = new WeakReference<Bitmap>(bmp);
            return bmp;
        }
        catch
        {
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }
}

/// <summary>
/// List item wrapping an image path. The Thumbnail property lazily triggers an async
/// decode on first bind (i.e. when the virtualized container becomes visible).
/// </summary>
public sealed partial class ThumbnailItem : ObservableObject
{
    public string Path { get; }
    public string FileName { get; }

    // Selection state for grid-style pickers where rows themselves are not selectable
    [ObservableProperty] private bool _isSelected;

    private readonly int _decodeWidth;
    private int _loading; // 0/1 interlocked flag

    public ThumbnailItem(string path, int decodeWidth = 160)
    {
        Path = path;
        FileName = System.IO.Path.GetFileName(path);
        _decodeWidth = decodeWidth;
    }

    public Bitmap? Thumbnail
    {
        get
        {
            if (ThumbnailCache.TryGet(Path, _decodeWidth, out var bmp)) return bmp;
            BeginLoad();
            return null;
        }
    }

    private void BeginLoad()
    {
        if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0) return;

        _ = Task.Run(async () =>
        {
            var bmp = await ThumbnailCache.LoadAsync(Path, _decodeWidth).ConfigureAwait(false);
            Interlocked.Exchange(ref _loading, 0);
            if (bmp != null)
            {
                Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Thumbnail)));
            }
        });
    }

    public override string ToString() => Path;
}
