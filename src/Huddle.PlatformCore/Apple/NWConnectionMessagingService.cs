// Apple's Network framework, which iOS and Mac Catalyst share. Outside Platforms/ because MAUI compiles only the
// current platform's folder there, and Platforms/iOS is not Mac Catalyst's.
#if IOS || MACCATALYST
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
#endif
