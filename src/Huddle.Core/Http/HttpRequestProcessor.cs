using Huddle.Server.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Specialized;
using System.Net;
using System.Text;

namespace Huddle.Core.Http;

/// <param name="StreamBody">When true the handler receives the raw body stream and no string body is materialized.</param>
/// <param name="MaxBodyBytes">Optional cap on the request body size; larger bodies are rejected with 413.</param>
/// <param name="Handler">The route handler.</param>
internal sealed record HttpEndpointRegistration(
    bool StreamBody,
    long? MaxBodyBytes,
    Func<HttpRequestData, Task<HttpResponseData>> Handler);

/// <param name="SourceHost">The connecting client's IP address, taken from the transport.</param>
/// <param name="Body">The fully-read body string; null for streamed routes.</param>
/// <param name="BodyStream">The raw body stream; null for buffered routes.</param>
/// <param name="ContentLength">The declared Content-Length; null when unknown (chunked bodies).</param>
/// <param name="QueryString">The parsed query string parameters.</param>
/// <param name="Headers">The request headers.</param>
/// <param name="Aborted">Fires when the server is stopping.</param>
internal sealed record HttpRequestData(
    string SourceHost,
    string? Body,
    Stream? BodyStream,
    long? ContentLength,
    Dictionary<string, string> QueryString,
    Dictionary<string, string> Headers,
    CancellationToken Aborted);

internal sealed record HttpResponseData(int StatusCode, string Body, string ContentType);

/// <summary>
/// The per-request HTTP pipeline shared by Huddle.Server: routes a request to its endpoint,
/// materializes or streams the body depending on how the route was mapped, invokes the
/// handler and writes the response. Lives in Huddle.Core so it can be exercised directly by
/// tests (Huddle.Server only targets MAUI platform frameworks).
/// </summary>
internal sealed class HttpRequestProcessor(ILogger logger)
{
    private readonly Dictionary<(string Path, string HttpMethod), HttpEndpointRegistration> _endpoints = [];

    public void Map(string path, string httpMethod, HttpEndpointRegistration registration)
    {
        _endpoints.Add((path, httpMethod), registration);
    }

    /// <summary>
    /// Handles one request end-to-end. Never throws - failures are logged and turned into
    /// an error response (or an aborted connection when the client is already gone).
    /// </summary>
    public async Task ProcessAsync(HttpListenerContext context, CancellationToken serverStopping)
    {
        try
        {
            await ProcessCoreAsync(context, serverStopping);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error processing request");
            try
            {
                context.Response.Abort();
            }
            catch
            {
            }
        }
    }

    private async Task ProcessCoreAsync(HttpListenerContext context, CancellationToken serverStopping)
    {
        var request = context.Request;
        var response = context.Response;

        var path = request.RawUrl?.Split('?')[0] ?? "/";
        var httpMethod = request.HttpMethod;
        // RemoteEndPoint, not UserHostName: the latter is the request's Host header - the
        // server's own address - which mislabelled every caller as the server itself.
        var sourceHost = request.RemoteEndPoint?.Address?.ToString() ?? string.Empty;

        if (!_endpoints.TryGetValue((path, httpMethod), out var endpoint))
        {
            logger.LogInformation("No endpoint mapped for {httpMethod}:{path} from {sourceHost}", httpMethod, path, sourceHost);
            await WriteResponseAsync(response, new HttpResponseData((int)HttpStatusCode.NotFound, string.Empty, string.Empty));
            return;
        }

        // -1 means the request did not declare a Content-Length (e.g. chunked transfer encoding).
        long? contentLength = request.ContentLength64 >= 0 ? request.ContentLength64 : null;

        if (endpoint.MaxBodyBytes.HasValue && contentLength.HasValue && contentLength.Value > endpoint.MaxBodyBytes.Value)
        {
            logger.LogInformation(
                "Rejecting {httpMethod}:{path} from {sourceHost}: declared body of {contentLength} bytes exceeds the {maxBodyBytes} byte limit",
                httpMethod, path, sourceHost, contentLength.Value, endpoint.MaxBodyBytes.Value);
            // The unread body would be misparsed as the next request on a reused connection.
            response.KeepAlive = false;
            await WriteResponseAsync(response, new HttpResponseData((int)HttpStatusCode.RequestEntityTooLarge, string.Empty, string.Empty));
            return;
        }

        var queryString = ToDictionary(request.QueryString);
        var headers = ToDictionary(request.Headers);

        HttpRequestData requestData;
        RequestBodyStream? bodyStream = null;

        if (endpoint.StreamBody)
        {
            logger.LogInformation(
                "Message handled {httpMethod}:{path} with streamed body ({contentLength} bytes) from {sourceHost}. {@queryString}, {@headers}",
                httpMethod, path, contentLength?.ToString() ?? "unknown", sourceHost, queryString, headers);

            bodyStream = new RequestBodyStream(request.InputStream, contentLength, endpoint.MaxBodyBytes);
            requestData = new HttpRequestData(sourceHost, null, bodyStream, contentLength, queryString, headers, serverStopping);
        }
        else
        {
            string? body;
            try
            {
                body = await ReadBodyAsStringAsync(request, contentLength, endpoint.MaxBodyBytes);
            }
            catch (RequestBodyTooLargeException)
            {
                response.KeepAlive = false;
                await WriteResponseAsync(response, new HttpResponseData((int)HttpStatusCode.RequestEntityTooLarge, string.Empty, string.Empty));
                return;
            }

            logger.LogInformation(
                "Message handled {httpMethod}:{path} with {body} from {sourceHost}. {@queryString}, {@headers}",
                httpMethod, path, body, sourceHost, queryString, headers);

            requestData = new HttpRequestData(sourceHost, body, null, contentLength, queryString, headers, serverStopping);
        }

        HttpResponseData responseData;
        try
        {
            responseData = await endpoint.Handler(requestData);
        }
        catch (RequestBodyTooLargeException)
        {
            response.KeepAlive = false;
            responseData = new HttpResponseData((int)HttpStatusCode.RequestEntityTooLarge, string.Empty, string.Empty);
        }
        catch (RequestAbortedException ex)
        {
            // The client is gone - there is no one to respond to.
            logger.LogInformation(ex, "Client aborted {httpMethod}:{path} mid-request", httpMethod, path);
            response.Abort();
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing {httpMethod}:{path}", httpMethod, path);
            responseData = new HttpResponseData((int)HttpStatusCode.InternalServerError, ex.Message, "text/plain");
        }

        // A handler that returned without consuming the whole streamed body leaves unread
        // bytes on the connection; close it rather than draining what may be gigabytes.
        if (bodyStream != null && !bodyStream.ReachedEnd)
        {
            response.KeepAlive = false;
        }

        await WriteResponseAsync(response, responseData);
    }

    private async Task<string?> ReadBodyAsStringAsync(HttpListenerRequest request, long? contentLength, long? maxBodyBytes)
    {
        try
        {
            using var body = maxBodyBytes.HasValue
                ? new RequestBodyStream(request.InputStream, contentLength, maxBodyBytes)
                : request.InputStream;
            using var reader = new StreamReader(body, request.ContentEncoding);
            return await reader.ReadToEndAsync();
        }
        catch (RequestBodyTooLargeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Error reading input stream for request");
            return null;
        }
    }

    private async Task WriteResponseAsync(HttpListenerResponse response, HttpResponseData responseData)
    {
        try
        {
            response.StatusCode = responseData.StatusCode;

            if (!string.IsNullOrEmpty(responseData.Body))
            {
                var buffer = Encoding.UTF8.GetBytes(responseData.Body);
                response.ContentLength64 = buffer.Length;
                response.ContentType = responseData.ContentType;

                await response.OutputStream.WriteAsync(buffer);
            }

            response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Could not write response - the client likely disconnected");
            try
            {
                response.Abort();
            }
            catch
            {
            }
        }
    }

    private static Dictionary<string, string> ToDictionary(NameValueCollection nameValueCollection)
    {
        var dictionary = new Dictionary<string, string>();
        foreach (var key in nameValueCollection.AllKeys)
        {
            if (key != null)
            {
                dictionary.Add(key, nameValueCollection[key] ?? string.Empty);
            }
        }
        return dictionary;
    }
}
