using System;
using System.Collections.Generic;

namespace FeatBit.Sdk.Server.Concurrent;

public sealed class StatusManager<TStatus> where TStatus : Enum
{
    private TStatus _status;
    private readonly object _statusLock = new object();
    private readonly Action<TStatus> _onStatusChanged;

    public StatusManager(TStatus initialStatus, Action<TStatus> onStatusChanged = null)
    {
        _status = initialStatus;
        _onStatusChanged = onStatusChanged;
    }

    public TStatus Status
    {
        get
        {
            lock (_statusLock)
            {
                return _status;
            }
        }
    }

    public bool CompareAndSet(TStatus expected, TStatus newStatus)
    {
        lock (_statusLock)
        {
            if (!EqualityComparer<TStatus>.Default.Equals(_status, expected))
            {
                return false;
            }

            SetStatus(newStatus);
            return true;
        }
    }

    public void SetStatus(TStatus newStatus)
    {
        lock (_statusLock)
        {
            if (EqualityComparer<TStatus>.Default.Equals(_status, newStatus))
            {
                return;
            }

            _status = newStatus;
            _onStatusChanged?.Invoke(_status);
        }
    }
}