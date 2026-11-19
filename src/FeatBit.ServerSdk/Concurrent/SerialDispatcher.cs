using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Concurrent;

/// <summary>
/// Runs a handler for posted items on the thread pool, one item at a time and in posting order,
/// without ever blocking the caller. Used to keep user callbacks off latency-sensitive threads.
/// </summary>
/// <remarks>
/// When a <c>coalesce</c> function is supplied, an item posted while another is still waiting is merged into
/// the waiting one instead of being queued, so at most one item is ever pending (plus the one being handled),
/// no matter how slow the handler is.
/// </remarks>
internal sealed class SerialDispatcher<T>
{
    private readonly Action<T> _handler;
    private readonly Func<T, T, T> _coalesce;
    private readonly ILogger _logger;
    private readonly LinkedList<T> _pending = new();
    private readonly object _lock = new();
    private bool _draining;

    public SerialDispatcher(Action<T> handler, ILogger logger, Func<T, T, T> coalesce = null)
    {
        _handler = handler;
        _logger = logger;
        _coalesce = coalesce;
    }

    public void Post(T item)
    {
        lock (_lock)
        {
            if (_coalesce != null && _pending.Count > 0)
            {
                // merge with the item that is still waiting; ordering is preserved because nothing is behind it
                _pending.Last.Value = _coalesce(_pending.Last.Value, item);
            }
            else
            {
                _pending.AddLast(item);
            }

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

                item = _pending.First.Value;
                _pending.RemoveFirst();
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
