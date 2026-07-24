using Huddle.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Network;

namespace Huddle.Core.Platforms.iOS
{
    internal class NWConnectionMessagingService(ILogger<NWConnectionMessagingService> logger) : NWListenerConnectionService(logger), IMessagingService
    {
        protected override void SetupUdpParams(NWParameters parameters)
        {
            parameters.IncludePeerToPeer = true;
        }
    }
}
