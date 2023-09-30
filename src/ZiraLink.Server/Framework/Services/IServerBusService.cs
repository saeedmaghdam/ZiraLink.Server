namespace ZiraLink.Server.Framework.Services
{
    public interface IServerBusService
    {
        void InitializeConsumer(CancellationToken cancellationToken);
    }
}
