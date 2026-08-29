using System.Net;

namespace Huddle.Server.Models;

/// <param name="ServiceProvider">The host app's service provider, for resolving services inside handlers.</param>
/// <param name="SourceHost">The connecting client's IP address, taken from the transport
/// (the remote endpoint of the HTTP connection), so it cannot be spoofed by headers.</param>
/// <param name="Request">The parsed body, query string and headers of the request.</param>
public record RequestContext(IServiceProvider ServiceProvider, string SourceHost, RequestInformation Request);
public record RequestInformation(string? Body, Dictionary<string, string> QueryString, Dictionary<string, string> Headers);

public record ResponseInformation(HttpStatusCode StatusCode, string Response, string ResponseContentType);
