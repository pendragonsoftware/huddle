using System.Net;
using System.Text.Json;

namespace Huddle.Server.Models;

public static class Results
{
    public static ResponseInformation Ok() => new(HttpStatusCode.OK, string.Empty, string.Empty);
    public static ResponseInformation Ok(string message) => new(HttpStatusCode.OK, message, "text/plain");
    public static ResponseInformation Ok<T>(T response) => new(HttpStatusCode.OK, JsonSerializer.Serialize(response), "application/json");
    public static ResponseInformation BadRequest() => new(HttpStatusCode.BadRequest, string.Empty, string.Empty);

    /// <summary>
    /// Streams <paramref name="body"/> to the client without buffering it. Position the stream
    /// where the response should start (e.g. to resume a download); Huddle disposes it.
    /// </summary>
    public static ResponseInformation Stream(Stream body, string contentType = "application/octet-stream", long? contentLength = null) =>
        new(HttpStatusCode.OK, string.Empty, contentType) { BodyStream = body, ContentLength = contentLength };

    /// <summary>Streams a file to the client, optionally from <paramref name="offset"/> bytes in.</summary>
    public static ResponseInformation File(string path, string contentType = "application/octet-stream", long offset = 0)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        stream.Position = Math.Clamp(offset, 0, stream.Length);
        return Stream(stream, contentType);
    }
}
