using iVault.Api.Events;

namespace iVault.Api.Interfaces;

public interface IMessageProducer
{
    Task PublishRecordIngestedAsync(RecordIngestedEvent @event);
}