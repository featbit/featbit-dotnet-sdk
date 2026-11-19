using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Concurrent;

/// <summary>
/// Runs a handler for posted items on the thread pool, one item at a time and in posting order,
/// without ever blocking the caller. Used to keep user callbacks off latency-sensitive threads.
/// </summary>
internal sealed class SerialDispatcher<T>
{
    private readonly Action<T> _handler;
    private readonly ILogger _logger;
    private readonly Queue<T> _pending = new();
    private readonly object _lock = new();
    private bool _draining;

    public SerialDispatcher(Action<T> handler, ILogger logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public void Post(T item)
    {
        lock (_lock)
        {
            _pending.Enqueue(item);
            if (_draining)
            {
                return;
            }

            _draining = true;
        }

        Task.Run(Drain);
    }

    private void Drain()
    {
        while (true)
        {
            T item;
            lock (_lock)
            {
                if (_pending.Count == 0)
                {
                    _draining = false;
                    return;
                }

                item = _pending.Dequeue();
            }

            try
            {
                _handler(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while dispatching a notification.");
            }
        }
    }
}
