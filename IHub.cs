namespace HubSockets
{
    public interface IHub
    {
        void OnConnectedAsync();
        void OnDisconnectedAsync();
    }
}