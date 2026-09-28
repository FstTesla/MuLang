using System.Text;
using System.Text.Json;

namespace MuLang.LanguageServer;

internal sealed class LspMessageWriter
{
    private readonly SemaphoreSlim gate = new (1, 1);
    private readonly Stream stream;

    public LspMessageWriter(Stream stream)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public Task WriteErrorAsync(
        JsonElement id,
        int code,
        string message,
        CancellationToken cancellationToken
    )
    {
        return WriteMessageAsync(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("jsonrpc", "2.0");
                writer.WritePropertyName("id");
                id.WriteTo(writer);
                writer.WriteStartObject("error");
                writer.WriteNumber("code", code);
                writer.WriteString("message", message);
                writer.WriteEndObject();
                writer.WriteEndObject();
            },
            cancellationToken
        );
    }

    public Task WriteNotificationAsync(
        string method,
        Action<Utf8JsonWriter> writeParameters,
        CancellationToken cancellationToken
    )
    {
        return WriteMessageAsync(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("jsonrpc", "2.0");
                writer.WriteString("method", method);
                writer.WritePropertyName("params");
                writeParameters(writer);
                writer.WriteEndObject();
            },
            cancellationToken
        );
    }

    public Task WriteResponseAsync(
        JsonElement id,
        Action<Utf8JsonWriter> writeResult,
        CancellationToken cancellationToken
    )
    {
        return WriteMessageAsync(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("jsonrpc", "2.0");
                writer.WritePropertyName("id");
                id.WriteTo(writer);
                writer.WritePropertyName("result");
                writeResult(writer);
                writer.WriteEndObject();
            },
            cancellationToken
        );
    }

    private async Task WriteMessageAsync(
        Action<Utf8JsonWriter> writeMessage,
        CancellationToken cancellationToken
    )
    {
        using MemoryStream payloadStream = new ();

        await using (Utf8JsonWriter writer = new (payloadStream))
        {
            writeMessage(writer);
        }

        byte[] payload = payloadStream.ToArray();
        byte[] header = Encoding.ASCII.GetBytes(
            $"Content-Length: {payload.Length}\r\n\r\n"
        );

        await gate.WaitAsync(cancellationToken);

        try
        {
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
