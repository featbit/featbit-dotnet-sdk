using System;
using System.Threading.Tasks;

namespace FeatBit.Sdk.Server.DataSynchronizer
{
    internal interface IDataSynchronizer
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
        /// The listener should return as soon as possible so as not to block subsequent notifications.
        /// </para>
        /// </remarks>
        event Action<DataSynchronizerStatus> StatusChanged;

        /// <summary>
        /// An event for receiving notifications after data has changed locally.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Any handlers attached to this event will be notified after a full synchronization replaces
        /// data or a patch changes locally stored data. See
        /// <see cref="DataChangeEventArgs"/> for information about the synchronization operation
        /// and the types of data that changed.
        /// </para>
        /// <para>
        /// The listener should return as soon as possible so as not to block subsequent data synchronization.
        /// </para>
        /// </remarks>
        event EventHandler<DataChangeEventArgs> DataChanged;

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
