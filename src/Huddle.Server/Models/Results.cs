using System.Net;
using System.Text.Json;

namespace Huddle.Server.Models;

public static class Results
{
    public static ResponseInformation Ok() => new(HttpStatusCode.OK, string.Empty, string.Empty);
    public static ResponseInformation Ok(string message) => new(HttpStatusCode.OK, message, "text/plain");
    public static ResponseInformation Ok<T>(T response) => new(HttpStatusCode.OK, JsonSerializer.Serialize(response), "application/json");
    public static ResponseInformation BadRequest() => new(HttpStatusCode.BadRequest, string.Empty, string.Empty);
}
