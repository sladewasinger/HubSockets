using Microsoft.AspNetCore.Connections;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HubSockets
{
    public class HubSocket : IDisposable
    {
        private bool _disposed = false;

        public Guid ID { get; set; }
        public event EventHandler DataReceived;

        private const int BufferSize = 16 * 1024;
        private WebSocket _socket;
        private readonly JsonSerializerSettings camelCaseJsonSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.None
        };

        protected virtual void OnDataReceived(HubSocketEventArgs e)
        {
            DataReceived?.Invoke(this, e);
        }

        public HubSocket(Guid id, WebSocket socket)
        {
            ID = id;
            _socket = socket;
        }

        public async Task ListenLoop()
        {
            string fullMessage = string.Empty;
            while (!_disposed)
            {
                var buffer = new byte[BufferSize];
                var seg = new ArraySegment<byte>(buffer);
                WebSocketReceiveResult result;

                try
                {
                    result = await _socket.ReceiveAsync(seg, CancellationToken.None);
                }
                catch
                {
                    break;
                }

                if (_socket.CloseStatus.HasValue || _socket.State != WebSocketState.Open)
                    break;

                string bufferString = Encoding.UTF8.GetString(seg);
                fullMessage += bufferString;
                if (result.EndOfMessage)
                {
                    OnDataReceived(new HubSocketEventArgs() { Data = fullMessage, HubSocket = this });
                    fullMessage = string.Empty;
                }
            }
        }

        public async Task SendData(object data)
        {
            if (_socket.State == WebSocketState.Open)
            {
                string jsonData = JsonConvert.SerializeObject(data, camelCaseJsonSerializerSettings);

                byte[] outgoingBytes = Encoding.UTF8.GetBytes(jsonData);
                var outgoing = new ArraySegment<byte>(outgoingBytes, 0, outgoingBytes.Length);

                await _socket.SendAsync(outgoing, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _socket.Dispose();
        }
    }

    public class HubSocketEqualityComparer : IEqualityComparer<HubSocket>
    {
        public bool Equals([AllowNull] HubSocket x, [AllowNull] HubSocket y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (x is null || y is null)
                return false;

            return x.ID == y.ID;
        }

        public int GetHashCode([DisallowNull] HubSocket obj)
        {
            return obj.ID.GetHashCode();
        }
    }
}
