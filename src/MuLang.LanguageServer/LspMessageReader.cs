using System.Text;
using System.Text.Json;

namespace MuLang.LanguageServer;

internal static class LspMessageReader
{
    private static readonly byte[] HeaderTerminator = [ .. "\r\n\r\n"u8 ];

    public static async Task<JsonDocument?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        IList<byte> headerBytes = new List<byte>();
        byte[] buffer = new byte[1];
        int terminatorPosition = 0;

        while (headerBytes.Count < 8192)
        {
            int count = await stream.ReadAsync(buffer, cancellationToken);

            if (count == 0)
            {
                return headerBytes.Count == 0
                    ? null
                    : throw new InvalidDataException("Unexpected end of LSP headers.");
            }

            byte value = buffer[0];
            headerBytes.Add(value);

            if (value == HeaderTerminator[terminatorPosition])
            {
                terminatorPosition++;

                if (terminatorPosition == HeaderTerminator.Length)
                {
                    break;
                }
            }
            else
            {
                terminatorPosition = value == HeaderTerminator[0] ? 1 : 0;
            }
        }

        if (terminatorPosition != HeaderTerminator.Length)
        {
            throw new InvalidDataException("LSP headers exceed the supported size.");
        }

        string headers = Encoding.ASCII.GetString(
            [ .. headerBytes.Take(headerBytes.Count - HeaderTerminator.Length) ]
        );
        int contentLength = ParseContentLength(headers);
        byte[] payload = new byte[contentLength];
        int offset = 0;

        while (offset < payload.Length)
        {
            int count = await stream.ReadAsync(
                payload.AsMemory(offset),
                cancellationToken
            );

            if (count == 0)
            {
                throw new InvalidDataException("Unexpected end of LSP payload.");
            }

            offset += count;
        }

        return JsonDocument.Parse(payload);
    }

    private static int ParseContentLength(string headers)
    {
        foreach (string header in headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = header.IndexOf(':');

            if (separator < 0)
            {
                continue;
            }

            string name = header[..separator].Trim();

            if (!name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = header[(separator + 1)..].Trim();

            if (
                int.TryParse(value, out int contentLength) &&
                contentLength >= 0
            )
            {
                return contentLength;
            }

            break;
        }

        throw new InvalidDataException("The LSP message has no valid Content-Length header.");
    }
}
