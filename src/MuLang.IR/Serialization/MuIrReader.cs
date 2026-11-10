using MuLang.Core.Diagnostics;
using System.Text;

namespace MuLang.IR.Serialization;

/// <summary>Provides MuIR deserialization.</summary>
public static class MuIrReader
{
    /// <summary>Reads a MuIR document from text.</summary>
    /// <param name="text">The MuIR document.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(string text)
    {
        return Read(text, null);
    }

    /// <summary>Reads a MuIR document from text.</summary>
    /// <param name="text">The MuIR document.</param>
    /// <param name="options">The reader options, or <c>null</c> to use the defaults.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(
        string text,
        MuIrReaderOptions? options
    )
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        return Analyze(text, options).ReadResult;
    }

    /// <summary>Analyzes a MuIR document once, retaining grammatical locations only after successful reading and materialization.</summary>
    /// <param name="text">The MuIR document.</param>
    /// <param name="options">The reader options, or <c>null</c> to use the defaults.</param>
    /// <returns>The shared parsed and reconstructed result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <c>null</c>.</exception>
    public static MuIrAnalysisResult Analyze(string text, MuIrReaderOptions? options = null)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        return MuIrParser.Analyze(text, options ?? MuIrReaderOptions.Default);
    }

    /// <summary>Reads a MuIR document from a text reader.</summary>
    /// <param name="reader">The source reader.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(TextReader reader)
    {
        return Read(reader, null);
    }

    /// <summary>Reads a MuIR document from a text reader.</summary>
    /// <param name="reader">The source reader.</param>
    /// <param name="options">The reader options, or <c>null</c> to use the defaults.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(
        TextReader reader,
        MuIrReaderOptions? options
    )
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        MuIrReaderOptions effectiveOptions = options ?? MuIrReaderOptions.Default;
        string text = ReadText(reader, effectiveOptions.MaximumDocumentLength);
        return Read(text, effectiveOptions);
    }

    /// <summary>Reads a UTF-8 MuIR document from a stream.</summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(Stream stream)
    {
        return Read(stream, true, null);
    }

    /// <summary>Reads a UTF-8 MuIR document from a stream.</summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="leaveOpen">Whether to leave the stream open after reading.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(Stream stream, bool leaveOpen)
    {
        return Read(stream, leaveOpen, null);
    }

    /// <summary>Reads a UTF-8 MuIR document from a stream.</summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="leaveOpen">Whether to leave the stream open after reading.</param>
    /// <param name="options">The reader options, or <c>null</c> to use the defaults.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(
        Stream stream,
        bool leaveOpen,
        MuIrReaderOptions? options
    )
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        MuIrReaderOptions effectiveOptions = options ?? MuIrReaderOptions.Default;

        try
        {
            using StreamReader reader = new (
                stream,
                new UTF8Encoding(false, true),
                true,
                1024,
                leaveOpen
            );
            string text = ReadText(reader, effectiveOptions.MaximumDocumentLength);
            return Read(text, effectiveOptions);
        }
        catch (DecoderFallbackException)
        {
            return Failure(
                MuIrDiagnosticCodes.InvalidUtf8,
                0,
                0,
                1,
                1,
                "The stream contains invalid UTF-8."
            );
        }
    }

    private static string ReadText(TextReader reader, int maximumLength)
    {
        StringBuilder builder = new (Math.Min(maximumLength, 4096));
        char[] buffer = new char[4096];

        while (true)
        {
            int read = reader.Read(buffer, 0, buffer.Length);

            if (read == 0)
            {
                return builder.ToString();
            }

            if (builder.Length > maximumLength - read)
            {
                return new string(' ', maximumLength + 1);
            }

            builder.Append(buffer, 0, read);
        }
    }

    private static MuIrReadResult Failure(
        string code,
        int offset,
        int length,
        int line,
        int column,
        string message
    )
    {
        return new MuIrReadResult(
            null,
            [
                new MuIrDiagnostic(
                    code,
                    DiagnosticSeverity.Error,
                    offset,
                    length,
                    line,
                    column,
                    message
                ),
            ]
        );
    }
}
