using System.Threading.Tasks;

namespace FeatBit.Sdk.Server.Events
{
    internal interface IEventSender
    {
        Task<DeliveryStatus> SendAsync(byte[] payload);
    }
}