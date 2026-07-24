namespace Huddle.Client.Platforms.Windows
{
    internal static partial class DnssdConstants
    {
        public const string DOMAIN = "local";
        public const string PROTOCOL_GUID = "{4526e8c1-8aac-4153-9b16-55e86ada0e54}";
        public const string NETWORK_PROTOCOL = "_udp";

        public const string HOSTNAME_PROPERTY = "System.Devices.Dnssd.HostName";
        public const string SERVICENAME_PROPERTY = "System.Devices.Dnssd.ServiceName";
        public const string INSTANCENAME_PROPERTY = "System.Devices.Dnssd.InstanceName";
        public const string IPADDRESS_PROPERTY = "System.Devices.IpAddress";
        public const string PORTNUMBER_PROPERTY = "System.Devices.Dnssd.PortNumber";
        public const string TEXTATTRIBUTES_PROPERTY = "System.Devices.Dnssd.TextAttributes";

        public static string[] PropertyKeys =
        [
            HOSTNAME_PROPERTY,
            SERVICENAME_PROPERTY,
            INSTANCENAME_PROPERTY,
            IPADDRESS_PROPERTY,
            PORTNUMBER_PROPERTY,
            TEXTATTRIBUTES_PROPERTY
        ];
        public static string GetAqsQueryString(string serviceName) => $"System.Devices.AepService.ProtocolId:={PROTOCOL_GUID} AND " +
            $"System.Devices.Dnssd.Domain:=\"{DOMAIN}\" AND System.Devices.Dnssd.ServiceName:=\"_{serviceName}.{NETWORK_PROTOCOL}\"";
    }
}