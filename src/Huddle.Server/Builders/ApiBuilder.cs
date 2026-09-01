using Huddle.Server.Models;

namespace Huddle.Server.Builders;

public interface IHttpApiBuilder
{
    IServerBuilder Server { get; }
    IHttpApiBuilder WithPort(int port);

    /// <summary>
    /// Sets a server-wide maximum request body size in bytes, applied to every route that
    /// does not specify its own <see cref="HttpEndpointOptions.MaxBodyBytes"/>. Requests
    /// with larger bodies are rejected with 413 Payload Too Large.
    /// </summary>
    IHttpApiBuilder WithMaxRequestBodyBytes(long maxBodyBytes);

    IHttpApiBuilder MapGet(string path, Func<RequestContext, Task<ResponseInformation>> handler);
    IHttpApiBuilder MapPost(string path, Func<RequestContext, Task<ResponseInformation>> handler);
    IHttpApiBuilder MapPut(string path, Func<RequestContext, Task<ResponseInformation>> handler);
    IHttpApiBuilder MapDelete(string path, Func<RequestContext, Task<ResponseInformation>> handler);

    /// <summary>
    /// Maps a POST endpoint with per-route options, e.g. <see cref="HttpBodyMode.Streamed"/>
    /// to receive large request bodies via <see cref="RequestInformation.BodyStream"/>
    /// without buffering them in memory.
    /// </summary>
    IHttpApiBuilder MapPost(string path, HttpEndpointOptions options, Func<RequestContext, Task<ResponseInformation>> handler);

    /// <summary>
    /// Maps a PUT endpoint with per-route options, e.g. <see cref="HttpBodyMode.Streamed"/>
    /// to receive large request bodies via <see cref="RequestInformation.BodyStream"/>
    /// without buffering them in memory.
    /// </summary>
    IHttpApiBuilder MapPut(string path, HttpEndpointOptions options, Func<RequestContext, Task<ResponseInformation>> handler);
}

internal class HttpApiBuilder(ServerBuilder serverBuilder) : IHttpApiBuilder
{
    private static readonly HttpEndpointOptions DefaultOptions = new();

    private int _port;
    private long? _maxRequestBodyBytes;
    private List<(string Path, string HttpMethod, HttpEndpointOptions Options, Func<RequestContext, Task<ResponseInformation>> Handler)> _endpoints = [];

    internal int Port => _port;

    /// <summary>
    /// The mapped endpoints, with the server-wide max body size folded into any route that
    /// did not set its own (this is read at Build time, after all fluent calls have run).
    /// </summary>
    internal List<(string Path, string HttpMethod, HttpEndpointOptions Options, Func<RequestContext, Task<ResponseInformation>> Handler)> Endpoints =>
        _endpoints
            .Select(endpoint => (
                endpoint.Path,
                endpoint.HttpMethod,
                endpoint.Options.MaxBodyBytes == null && _maxRequestBodyBytes != null
                    ? new HttpEndpointOptions { BodyMode = endpoint.Options.BodyMode, MaxBodyBytes = _maxRequestBodyBytes }
                    : endpoint.Options,
                endpoint.Handler))
            .ToList();

    public IServerBuilder Server => serverBuilder;

    public IHttpApiBuilder WithPort(int port)
    {
        _port = port;
        return this;
    }

    public IHttpApiBuilder WithMaxRequestBodyBytes(long maxBodyBytes)
    {
        _maxRequestBodyBytes = maxBodyBytes;
        return this;
    }

    public IHttpApiBuilder MapGet(string path, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        MapEndpoint(path, "GET", DefaultOptions, handler);
        return this;
    }

    public IHttpApiBuilder MapPost(string path, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        MapEndpoint(path, "POST", DefaultOptions, handler);
        return this;
    }

    public IHttpApiBuilder MapPost(string path, HttpEndpointOptions options, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        MapEndpoint(path, "POST", options, handler);
        return this;
    }

    public IHttpApiBuilder MapPut(string path, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        MapEndpoint(path, "PUT", DefaultOptions, handler);
        return this;
    }

    public IHttpApiBuilder MapPut(string path, HttpEndpointOptions options, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        MapEndpoint(path, "PUT", options, handler);
        return this;
    }

    public IHttpApiBuilder MapDelete(string path, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        MapEndpoint(path, "DELETE", DefaultOptions, handler);
        return this;
    }

    private void MapEndpoint(string path, string httpMethod, HttpEndpointOptions options, Func<RequestContext, Task<ResponseInformation>> handler)
    {
        var exists = _endpoints.Any(x => x.Path == path && x.HttpMethod == httpMethod);
        if (exists)
        {
            throw new Exception($"An {httpMethod} endpoint has already been added for path: {path}");
        }

        _endpoints.Add((path, httpMethod, options, handler));
    }
}
