namespace FeatBit.Sdk.Server;

public enum FbClientStatus
{
    /// <summary>
    /// FbClient has not been initialized and cannot yet evaluate flags.
    /// </summary>
    NotReady,

    /// <summary>
    /// FbClient is ready to resolve flags.
    /// </summary>
    Ready,

    /// <summary>
    /// FbClient's cached data may not be up-to-date with the source of truth.
    /// </summary>
    Stale,

    /// <summary>
    /// FbClient has entered an irrecoverable error state.
    /// </summary>
    Fatal
}