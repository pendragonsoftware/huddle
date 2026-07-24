using System.Net;

namespace Huddle.Server.Models;

public record RequestContext(IServiceProvider ServiceProvider, string SourceHost, RequestInformation Request);
public record RequestInformation(string? Body, Dictionary<string, string> QueryString, Dictionary<string, string> Headers);

public record ResponseInformation(HttpStatusCode StatusCode, string Response, string ResponseContentType);
