using System;
using System.Threading.Tasks;

namespace FeatBit.Sdk.Server.Events
{
    /// <summary>
    /// Represents a processor that can process events.
    /// </summary>
    internal interface IEventProcessor
    {
        /// <summary>
        /// Records an <see cref="IEvent"/>.
        /// </summary>
        /// <param name="event">The event to be recorded.</param>
        /// <returns>A boolean value indicating whether the operation succeeded or not.</returns>
        bool Record(IEvent @event);

        /// <summary>
        /// Triggers an asynchronous event flush.
        /// </summary>
        void Flush();

        /// <summary>
        /// Blocking version of <see cref="Flush"/>.
        /// </summary>
        /// <param name="timeout">maximum time to wait; zero or negative timeout means indefinitely</param>
        /// <returns>true if completed, false if timed out</returns>
        bool FlushAndWait(TimeSpan timeout);

        /// <summary>
        /// Asynchronous version of <see cref="FlushAndWait"/>.
        /// </summary>
        /// <remarks>
        /// The difference between this and <see cref="Flush"/> is that you can await the task to simulate
        /// blocking behavior.
        /// </remarks>
        /// <param name="timeout">maximum time to wait; zero or negative timeout means indefinitely</param>
        /// <returns>a task that resolves to true if completed, false if timed out</returns>
        Task<bool> FlushAndWaitAsync(TimeSpan timeout);

        /// <summary>
        /// Flush all events and close this processor.
        /// </summary>
        /// <param name="timeout">maximum time to wait; zero or negative timeout means indefinitely</param>
        /// <returns>true if completed, false if timed out</returns>
        void FlushAndClose(TimeSpan timeout);
    }
}