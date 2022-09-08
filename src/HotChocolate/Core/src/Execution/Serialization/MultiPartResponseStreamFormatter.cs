using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate.Utilities;
using static HotChocolate.Execution.ExecutionResultKind;

namespace HotChocolate.Execution.Serialization;

// https://github.com/graphql/graphql-over-http/blob/master/rfcs/IncrementalDelivery.md
public sealed partial class MultiPartResponseStreamFormatter : IExecutionResultFormatter
{
    private readonly IQueryResultFormatter _payloadFormatter;

    /// <summary>
    /// Creates a new instance of <see cref="MultiPartResponseStreamFormatter" />.
    /// </summary>
    /// <param name="indented">
    /// Defines whether the underlying <see cref="Utf8JsonWriter"/>
    /// should pretty print the JSON which includes:
    /// indenting nested JSON tokens, adding new lines, and adding
    /// white space between property names and values.
    /// By default, the JSON is written without any extra white space.
    /// </param>
    /// <param name="encoder">
    /// Gets or sets the encoder to use when escaping strings, or null to use the default encoder.
    /// </param>
    public MultiPartResponseStreamFormatter(
        bool indented = false,
        JavaScriptEncoder? encoder = null)
    {
        _payloadFormatter = new JsonQueryResultFormatter(indented, encoder);
    }

    /// <summary>
    /// Creates a new instance of <see cref="MultiPartResponseStreamFormatter" />.
    /// </summary>
    /// <param name="queryResultFormatter">
    /// The serializer that shall be used to serialize query results.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="queryResultFormatter"/> is <c>null</c>.
    /// </exception>
    public MultiPartResponseStreamFormatter(
        IQueryResultFormatter queryResultFormatter)
    {
        _payloadFormatter = queryResultFormatter ??
            throw new ArgumentNullException(nameof(queryResultFormatter));
    }

    public async ValueTask FormatAsync(
        IExecutionResult result,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        if (result.Kind is SingleResult)
        {
            await WriteSingleResponseAsync(
                (IQueryResult)result,
                outputStream,
                cancellationToken)
                .ConfigureAwait(false);
        }
        else if (result.Kind is DeferredResult or BatchResult or SubscriptionResult)
        {
            await WriteResponseStreamAsync(
                (IResponseStream)result,
                outputStream,
                cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // TODO : ThrowHelper
            throw new NotSupportedException(
                $"The {GetType().FullName} does not support formatting `{result.Kind}`.");
        }
    }

    public Task FormatAsync(
        IResponseStream responseStream,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        if (responseStream is null)
        {
            throw new ArgumentNullException(nameof(responseStream));
        }

        if (outputStream is null)
        {
            throw new ArgumentNullException(nameof(outputStream));
        }

        return WriteResponseStreamAsync(responseStream, outputStream, cancellationToken);
    }

    private async Task WriteResponseStreamAsync(
        IResponseStream responseStream,
        Stream outputStream,
        CancellationToken ct = default)
    {
        await foreach (var result in
            responseStream.ReadResultsAsync().WithCancellation(ct).ConfigureAwait(false))
        {
            try
            {
                await WriteNextAsync(outputStream, ct).ConfigureAwait(false);
                await WriteResultAsync(result, outputStream, ct).ConfigureAwait(false);
                await outputStream.FlushAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                // The result objects use pooled memory so we need to ensure that they
                // return the memory by disposing them.
                await result.DisposeAsync().ConfigureAwait(false);
            }
        }

        await WriteEndAsync(outputStream, ct).ConfigureAwait(false);
        await outputStream.FlushAsync(ct).ConfigureAwait(false);
    }

    private async Task WriteSingleResponseAsync(
        IQueryResult queryResult,
        Stream outputStream,
        CancellationToken ct = default)
    {
        await WriteNextAsync(outputStream, ct).ConfigureAwait(false);

        try
        {
            await WriteResultAsync(queryResult, outputStream, ct).ConfigureAwait(false);
        }
        finally
        {
            await queryResult.DisposeAsync().ConfigureAwait(false);
        }

        await WriteEndAsync(outputStream, ct).ConfigureAwait(false);
        await outputStream.FlushAsync(ct).ConfigureAwait(false);
    }

    private async ValueTask WriteResultAsync(
        IQueryResult result,
        Stream outputStream,
        CancellationToken ct)
    {
        using var writer = new ArrayWriter();
        _payloadFormatter.Format(result, writer);

        await WriteResultHeaderAsync(outputStream, ct).ConfigureAwait(false);

        // The payload is sent, followed by a CRLF.
        var buffer = writer.GetInternalBuffer();
        await outputStream.WriteAsync(buffer, 0, writer.Length, ct).ConfigureAwait(false);
    }

    private static async ValueTask WriteResultHeaderAsync(
        Stream outputStream,
        CancellationToken ct)
    {
        // Each part of the multipart response must contain a Content-Type header.
        // Similar to the GraphQL specification this specification does not require
        // a specific serialization format. For consistency and ease of notation,
        // examples of the response are given in JSON throughout the spec.
        await outputStream.WriteAsync(ContentType, 0, ContentType.Length, ct).ConfigureAwait(false);
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);

        // After all headers, an additional CRLF is sent.
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);
    }

    private static async ValueTask WriteNextAsync(
        Stream outputStream,
        CancellationToken ct)
    {
        // Before each part of the multi-part response, a boundary (CRLF, ---, CRLF) is sent.
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);
        await outputStream.WriteAsync(Start, 0, Start.Length, ct).ConfigureAwait(false);
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);
    }

    private static async ValueTask WriteEndAsync(
        Stream outputStream,
        CancellationToken ct)
    {
        // After the final payload, the terminating boundary of CRLF followed by
        // ----- followed by CRLF is sent.
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);
        await outputStream.WriteAsync(End, 0, End.Length, ct).ConfigureAwait(false);
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);
    }
}
