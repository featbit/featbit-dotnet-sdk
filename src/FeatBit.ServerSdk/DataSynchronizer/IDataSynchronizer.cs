using System;
using System.Threading.Tasks;

namespace FeatBit.Sdk.Server.DataSynchronizer
{
    public interface IDataSynchronizer
    {
        /// <summary>
        /// Indicates whether the data synchronizer has finished initializing.
        /// </summary>
        public bool Initialized { get; }

        /// <summary>
        /// The current status of the data synchronizer.
        /// </summary>
        public DataSynchronizerStatus Status { get; }

        /// <summary>An event for receiving notifications of status changes.</summary>
        /// <remarks>
        /// <para>
        /// Any handlers attached to this event will be notified whenever any property of the status has changed.
        /// See <see cref="T:FeatBit.Sdk.Server.DataSynchronizer.DataSynchronizerStatus" /> for an explanation of the meaning of each property and what could cause it
        /// to change.
        /// </para>
        /// <para>
        /// Notifications will be dispatched on a background task. It is the listener's responsibility to return
        /// as soon as possible so as not to block subsequent notifications.
        /// </para>
        /// </remarks>
        event Action<DataSynchronizerStatus> StatusChanged;

        /// <summary>
        /// Starts the data synchronizer. This is called once from the <see cref="FbClient"/> constructor.
        /// </summary>
        /// <returns>a <c>Task</c> which is completed once the data synchronizer has finished starting up</returns>
        Task<bool> StartAsync();

        /// <summary>
        /// Stop the data synchronizer and dispose all resources.
        /// </summary>
        /// <returns>The <c>Task</c></returns>
        Task StopAsync();
    }
}