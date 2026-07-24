using Huddle.Core.Services.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace Huddle.Core.Services
{
    internal class IpAddressRetrievalService : IIpAddressRetrievalService
    {
        public string? GetIpAddress()
        {
            try
            {
                string? localIP = null;
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    if (socket.LocalEndPoint is IPEndPoint endPoint)
                    {
                        localIP = endPoint.Address.ToString();
                    }
                }
                return localIP;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
