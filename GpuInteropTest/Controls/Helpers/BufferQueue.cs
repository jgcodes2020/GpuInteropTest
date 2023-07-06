using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace GpuInteropTest.Controls.Helpers;

public abstract class BufferQueue<TImage> : IAsyncDisposable where TImage : class, IQueuableImage
{
    private TImage? _currentBuffer;
    private LinkedList<TImage> _pendingBuffers;
    private BlockingCollection<(TImage, ICompositionImportedGpuImage)> _importingBuffers;
    private SemaphoreSlim _takeEvent;

    protected ICompositionGpuInterop Interop { get; }

    protected BufferQueue(ICompositionGpuInterop interop)
    {
        _currentBuffer = null;
        _pendingBuffers = new LinkedList<TImage>();
        _importingBuffers = new BlockingCollection<(TImage, ICompositionImportedGpuImage)>(10);
        _takeEvent = new SemaphoreSlim(0, 1);
        Interop = interop;
    }

    protected abstract TImage InitBuffer(PixelSize size);

    private TImage? SearchPending(PixelSize size)
    {
        lock (_pendingBuffers)
        {
            var node = _pendingBuffers.First;
            LinkedListNode<TImage>? firstNode = null;
            bool foundMultiple = false;
            while (node != null)
            {
                var nextNode = node.Next;

                if (node.Value.Size != size)
                    _pendingBuffers.Remove(node);
                else
                {
                    if (firstNode == null)
                        firstNode = node;
                    else
                        foundMultiple = true;
                }

                node = nextNode;
            }

            if (!foundMultiple)
                return null;

            if (firstNode != null)
                _pendingBuffers.Remove(firstNode);

            return firstNode?.Value;
        }
    }

    public void SwapBuffers(PixelSize size)
    {
        // move current buffer to import queue
        if (_currentBuffer != null)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ICompositionImportedGpuImage imported = _currentBuffer.Import(Interop);
                // This does not need to be synchronized, it's going to run in the UI thread anyways
                _importingBuffers.Add((_currentBuffer, imported));
            });
            _currentBuffer = null;
        }

        _currentBuffer = SearchPending(size) ?? InitBuffer(size);
        _currentBuffer.Setup();
    }

    public TImage CurrentBuffer
    {
        get
        {
            if (_currentBuffer == null)
                throw new InvalidOperationException(
                    $"{nameof(SwapBuffers)}() must be called before accessing {nameof(CurrentBuffer)}");
            return _currentBuffer;
        }
    }

    public async Task DisplayNext(CompositionDrawingSurface surface)
    {
        Console.WriteLine($"Frames pending: {_importingBuffers.Count}");
        if (!Dispatcher.UIThread.CheckAccess())
            throw new ApplicationException($"{nameof(DisplayNext)} must be called from the UI thread");
        if (_importingBuffers.Count < 1)
            return;
        var (buffer, imported) = _importingBuffers.Take();
        await surface.UpdateAsync(imported);
        buffer.Reset();
        lock (_pendingBuffers)
        {
            _pendingBuffers.AddLast(buffer);
        }
    }

    public async ValueTask DisposeAsync()
    {
        var subDisposers = new List<Task>();
        _importingBuffers.CompleteAdding();
        while (!_importingBuffers.IsCompleted)
        {
            var (image, import) = _importingBuffers.Take();
            subDisposers.AddRange(new[] { image.DisposeAsync().AsTask(), import.DisposeAsync().AsTask() });
        }

        lock (_pendingBuffers)
        {
            subDisposers.AddRange(_pendingBuffers.Select(buffer => buffer.DisposeAsync().AsTask()));
        }

        await Task.WhenAll(subDisposers);
    }
}
/*
-! Image is bad
    - delete it
-! Image is not bad
    -! Image is not ready
        - skip it
    -! Image is ready
        -! Size matches
            - use it
        -! Size doesn't match
            - delete it
*/

public interface IQueuableImage : IAsyncDisposable
{
    PixelSize Size { get; }

    void Setup()
    {
    }

    ICompositionImportedGpuImage Import(ICompositionGpuInterop interop);

    void Reset()
    {
    }
}