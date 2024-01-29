﻿using Avalonia.Media.Imaging;
using ImageMagick;
using PicView.Avalonia.Models;
using PicView.Avalonia.Services;
using PicView.Core.FileHandling;
using PicView.Core.ImageDecoding;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PicView.Avalonia.Navigation;

internal class PreLoader
{
    private static bool _isRunning;

    internal class PreLoadValue
    {
        internal ImageModel? ImageModel { get; set; }

        internal bool IsLoading = true;

        internal PreLoadValue(ImageModel? imageModel)
        {
            ImageModel = imageModel;
        }
    }

    private readonly ConcurrentDictionary<int, PreLoadValue> _preLoadList = new();
    private const int PositiveIterations = 8;
    private const int NegativeIterations = 4;
    internal const int MaxCount = PositiveIterations + NegativeIterations + 2;

#if DEBUG

    // ReSharper disable once ConvertToConstant.Local
    private static readonly bool ShowAddRemove = true;

#endif

    internal async Task<bool> AddAsync(int index, List<string> list, ImageModel? imageModel = null)
    {
        if (list == null)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(AddAsync)} list null \n{index}");
#endif
            return false;
        }
        if (index < 0 || index >= list.Count)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(AddAsync)} invalid index: \n{index}");
#endif
            return false;
        }

        var preLoadValue = new PreLoadValue(imageModel);
        try
        {
            var add = _preLoadList.TryAdd(index, preLoadValue);
            if (add)
            {
                imageModel ??= new ImageModel();
                imageModel.FileInfo ??= new FileInfo(list[index]);
                preLoadValue.ImageModel = imageModel;
                if (imageModel.Image is null)
                {
                    preLoadValue.IsLoading = true;
                    await ImageModel.LoadImageAsync(imageModel).ConfigureAwait(false);
                }

                if (imageModel.EXIFOrientation is null || imageModel is { EXIFOrientation: EXIFHelper.EXIFOrientation.None, Image: not null })
                {
                    using var magickImage = new MagickImage(imageModel.FileInfo);
                    preLoadValue.ImageModel.EXIFOrientation = EXIFHelper.GetImageOrientation(magickImage);
                }
                else
                {
                    preLoadValue.ImageModel.EXIFOrientation = EXIFHelper.EXIFOrientation.None;
                }
#if DEBUG
                if (ShowAddRemove)
                    Trace.WriteLine($"{imageModel.FileInfo.Name} added at {index}");
#endif
                return true;
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(AddAsync)} exception: \n{ex}");
#endif
        }
        finally
        {
            preLoadValue.IsLoading = false;
        }
        return false;
    }

    internal async Task<bool> RefreshFileInfo(int index, List<string> list)
    {
        if (list == null)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(RefreshFileInfo)} list null \n{index}");
#endif
            return false;
        }
        if (index < 0 || index >= list.Count)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(RefreshFileInfo)} invalid index: \n{index}");
#endif
            return false;
        }

        var removed = _preLoadList.TryRemove(index, out var preLoadValue);
        if (preLoadValue is not null)
        {
            preLoadValue.ImageModel.FileInfo = null;
        }

        await AddAsync(index, list, preLoadValue.ImageModel).ConfigureAwait(false);
        return removed;
    }

    /// <summary>
    /// Removes all keys from the cache.
    /// </summary>
    internal void Clear()
    {
        _preLoadList.Clear();
    }

    internal PreLoadValue? Get(int key, List<string> list)
    {
        if (list == null)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Get)} list null \n{key}");
#endif
            return null;
        }
        if (key < 0 || key >= list.Count)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Get)} invalid key: \n{key}");
#endif
            return null;
        }

        return !Contains(key, list) ? null : _preLoadList[key];
    }

    internal bool Contains(int key, List<string> list)
    {
        if (list == null)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Get)} list null \n{key}");
#endif
            return false;
        }
        if (key < 0 || key >= list.Count)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Contains)} invalid key: \n{key}");
#endif
            return false;
        }

        return !_preLoadList.IsEmpty && _preLoadList.ContainsKey(key);
    }

    internal bool Remove(int key, List<string> list)
    {
        if (list == null)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Get)} list null \n{key}");
#endif
            return false;
        }

        if (key < 0 || key >= list.Count)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Remove)} invalid key: \n{key}");
#endif
            return false;
        }

        if (!Contains(key, list))
        {
            return false;
        }

        try
        {
            _ = _preLoadList[key];
            var remove = _preLoadList.TryRemove(key, out _);
#if DEBUG
            if (remove && ShowAddRemove)
                Trace.WriteLine($"{list[key]} removed at {list.IndexOf(list[key])}");
#endif
            return remove;
        }
        catch (Exception e)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(Remove)} exception:\n{e.Message}");
#endif
            return false;
        }
    }

    internal async Task PreLoadAsync(int currentIndex, int count, bool parallel, bool reverse, List<string> list)
    {
        if (list == null)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Get)} list null \n{currentIndex}");
#endif
            return;
        }
        if (_isRunning)
        {
            return;
        }
        _isRunning = true;

        int nextStartingIndex, prevStartingIndex;
        if (reverse)
        {
            nextStartingIndex = (currentIndex - 1 + count) % count;
            prevStartingIndex = currentIndex + 1;
        }
        else
        {
            nextStartingIndex = (currentIndex + 1) % count;
            prevStartingIndex = currentIndex - 1;
        }
        var array = new int[MaxCount];

#if DEBUG
        if (ShowAddRemove)
            Trace.WriteLine($"\nPreLoading started at {currentIndex}\n");
#endif

        var options = parallel
            ? new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount - 2 < 1 ? 1 : Environment.ProcessorCount - 2
            }
            : null;

        try
        {
            if (reverse)
            {
                await NegativeLoop(options);
                await PositiveLoop(options);
            }
            else
            {
                await PositiveLoop(options);
                await NegativeLoop(options);
            }
        }
        catch (Exception exception)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoadAsync)} exception:\n{exception.Message}");
#endif
        }
        finally
        {
            _isRunning = false;
        }

        RemoveLoop();

        return;

        async Task PositiveLoop(ParallelOptions parallelOptions)
        {
            if (parallel)
            {
                await Parallel.ForAsync(0, PositiveIterations, parallelOptions, async (i, _) =>
                {
                    if (list.Count == 0 || count != list.Count)
                    {
                        Clear();
                        return;
                    }
                    var index = (nextStartingIndex + i) % list.Count;
                    var isAdded = await AddAsync(index, list);
                    if (isAdded)
                    {
                        array[i] = index;
                    }
                });
            }
            else
            {
                for (var i = 0; i < PositiveIterations; i++)
                {
                    if (list.Count == 0 || count != list.Count)
                    {
                        Clear();
                        return;
                    }
                    var index = (nextStartingIndex + i) % list.Count;
                    _ = AddAsync(index, list);
                    array[i] = index;
                }
            }
        }

        async Task NegativeLoop(ParallelOptions parallelOptions)
        {
            if (parallel)
            {
                await Parallel.ForAsync(0, NegativeIterations, parallelOptions, async (i, _) =>
                {
                    if (list.Count == 0 || count != list.Count)
                    {
                        Clear();
                        return;
                    }
                    var index = (prevStartingIndex - i + list.Count) % list.Count;
                    var isAdded = await AddAsync(index, list);
                    if (isAdded)
                    {
                        array[i] = index;
                    }
                });
            }
            else
            {
                for (var i = 0; i < NegativeIterations; i++)
                {
                    if (list.Count == 0 || count != list.Count)
                    {
                        Clear();
                        return;
                    }
                    var index = (prevStartingIndex - i + list.Count) % list.Count;
                    _ = AddAsync(index, list);
                    array[i] = index;
                }
            }
        }

        void RemoveLoop()
        {
            if (list.Count <= MaxCount + NegativeIterations || _preLoadList.Count <= MaxCount)
            {
                return;
            }
            var deleteCount = _preLoadList.Count - MaxCount < MaxCount ? MaxCount : _preLoadList.Count - MaxCount;
            for (var i = 0; i < deleteCount; i++)
            {
                var removeIndex = reverse ? _preLoadList.Keys.Max() : _preLoadList.Keys.Min();
                if (i >= array.Length)
                {
                    return;
                }

                if (array[i] == removeIndex || removeIndex == currentIndex)
                {
                    continue;
                }

                if (removeIndex > currentIndex + 2 || removeIndex < currentIndex - 2)
                {
                    Remove(removeIndex, list);
                }
            }
        }
    }
}