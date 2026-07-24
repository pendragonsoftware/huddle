using Huddle.Server.Models;

namespace Huddle.Server.Builders;

public interface IHttpApiBuilder
{
    IServerBuilder Server { get; }
    IHttpApiBuilder WithPort(int port);
    IHttpApiBuilder MapGet(string path, Func<RequestContext, Task<ResponseInformation>> handler);
    IHttpApiBuilder MapPost(string path, Func<RequestContext, Task<ResponseInformation>> handler);
    IHttpApiBuilder MapPut(string path, Func<RequestContext, Task<ResponseInformation>> handler);
    IHttpApiBuilder MapDelete(string path, Func<RequestContext, Task<ResponseInformation>> handler);
}

internal class HttpApiBuilder(ServerBuilder serverBuilder) : IHttpApiBuilder
{
    private int _port;
    private List<(string Path, string HttpMethod, Func<RequestContext, Task<ResponseInformation>> Handler)> _endpoints = [];

    internal int Port => _port;
    internal List<(string Path, string HttpMethod, Func<RequestContext, Task<ResponseInformation>> Handler)> Endpoints => _endpoints;

    public IServerBuilder Server => serverBuilder;

    public IHttpApiBuilder WithPort(int port)
    {
        _port = port;
        return this;
    }

    public IHttpApiBuilder MapGet(string path, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        MapEndpoint(path, "GET", handler);
        return this;
    }

    public IHttpApiBuilder MapPost(string path, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        MapEndpoint(path, "POST", handler);
        return this;
    }

    public IHttpApiBuilder MapPut(string path, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        MapEndpoint(path, "PUT", handler);
        return this;
    }

    public IHttpApiBuilder MapDelete(string path, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        MapEndpoint(path, "DELETE", handler);
        return this;
    }

    private void MapEndpoint(string path, string httpMethod, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        var exists = _endpoints.Any(x => x.Path == path && x.HttpMethod == httpMethod);
        if (exists)
        {
            throw new Exception($"An {httpMethod} endpoint has already been added for path: {path}");
        }

        _endpoints.Add((path, httpMethod, handler));
    }
}
