// Tiny static file server for previewing the birthday card locally.
// Usage: dotnet run serve.cs [port] [root]
// Requires the .NET 10 SDK (file-based apps).

using System.Net;
using System.Net.Sockets;
using System.Text;

var port = args.Length > 0 && int.TryParse(args[0], out var requestedPort)
    ? requestedPort
    : 8765;

var root = Path.GetFullPath(args.Length > 1 ? args[1] : Directory.GetCurrentDirectory());

var listener = new TcpListener(IPAddress.Loopback, port);

try
{
    listener.Start();
}
catch (SocketException ex)
{
    Console.Error.WriteLine($"Could not start on port {port}: {ex.Message}");
    Console.Error.WriteLine("Try another port, for example: dotnet run serve.cs -- 8080");
    return;
}

Console.WriteLine($"Birthday card preview: http://localhost:{port}");
Console.WriteLine($"Serving: {root}");
Console.WriteLine("Press Ctrl+C to stop.");

while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    _ = HandleClientAsync(client);
}

async Task HandleClientAsync(TcpClient client)
{
    using (client)
    {
        try
        {
            await using var stream = client.GetStream();

            var requestHead = await ReadRequestHeadAsync(stream);
            if (requestHead is null)
            {
                return;
            }

            var lines = requestHead.Split("\r\n");
            var parts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                await WriteResponseAsync(stream, "400 Bad Request", "text/plain; charset=utf-8",
                    Encoding.UTF8.GetBytes("Bad request"));
                return;
            }

            var method = parts[0].ToUpperInvariant();
            if (method is not ("GET" or "HEAD"))
            {
                await WriteResponseAsync(stream, "405 Method Not Allowed", "text/plain; charset=utf-8",
                    Encoding.UTF8.GetBytes("Only GET and HEAD are supported."));
                return;
            }

            var rawPath = parts[1].Split('?', '#')[0];
            var relativePath = Uri.UnescapeDataString(rawPath).TrimStart('/');
            if (string.IsNullOrEmpty(relativePath))
            {
                relativePath = "index.html";
            }

            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));

            if (!IsInsideRoot(fullPath))
            {
                await WriteResponseAsync(stream, "403 Forbidden", "text/plain; charset=utf-8",
                    Encoding.UTF8.GetBytes("Forbidden"));
                return;
            }

            if (Directory.Exists(fullPath))
            {
                fullPath = Path.Combine(fullPath, "index.html");
            }

            if (!File.Exists(fullPath))
            {
                await WriteResponseAsync(stream, "404 Not Found", "text/plain; charset=utf-8",
                    Encoding.UTF8.GetBytes("Not found"));
                return;
            }

            var body = await File.ReadAllBytesAsync(fullPath);
            var headOnly = method == "HEAD";
            await WriteResponseAsync(stream, "200 OK", GetContentType(fullPath), body, headOnly);
        }
        catch
        {
            // A browser may close a connection early; there is nothing to recover.
        }
    }
}

bool IsInsideRoot(string fullPath)
{
    var comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    if (string.Equals(fullPath, root, comparison))
    {
        return true;
    }

    var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
        ? root
        : root + Path.DirectorySeparatorChar;

    return fullPath.StartsWith(rootWithSeparator, comparison);
}

static async Task<string?> ReadRequestHeadAsync(NetworkStream stream)
{
    var buffer = new byte[4096];
    var request = new StringBuilder();

    while (request.Length <= 64 * 1024)
    {
        var read = await stream.ReadAsync(buffer);
        if (read == 0)
        {
            return null;
        }

        request.Append(Encoding.ASCII.GetString(buffer, 0, read));
        if (request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            return request.ToString();
        }
    }

    return null;
}

static async Task WriteResponseAsync(
    NetworkStream stream,
    string status,
    string contentType,
    byte[] body,
    bool headOnly = false)
{
    var header = string.Join("\r\n",
        $"HTTP/1.1 {status}",
        $"Content-Type: {contentType}",
        $"Content-Length: {body.Length}",
        "Cache-Control: no-store",
        "X-Content-Type-Options: nosniff",
        "Connection: close",
        "",
        "");

    await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
    if (!headOnly)
    {
        await stream.WriteAsync(body);
    }
}

static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
{
    ".html" or ".htm" => "text/html; charset=utf-8",
    ".css" => "text/css; charset=utf-8",
    ".js" or ".mjs" => "text/javascript; charset=utf-8",
    ".json" => "application/json; charset=utf-8",
    ".svg" => "image/svg+xml",
    ".png" => "image/png",
    ".jpg" or ".jpeg" => "image/jpeg",
    ".webp" => "image/webp",
    ".gif" => "image/gif",
    ".ico" => "image/x-icon",
    ".woff" => "font/woff",
    ".woff2" => "font/woff2",
    ".wav" => "audio/wav",
    ".mp3" => "audio/mpeg",
    ".ogg" => "audio/ogg",
    ".m4a" => "audio/mp4",
    ".txt" => "text/plain; charset=utf-8",
    ".md" => "text/markdown; charset=utf-8",
    _ => "application/octet-stream",
};
