using System.Collections.Generic;

#nullable enable

namespace HotChocolate.Execution;

/// <summary>
/// Represents a query result object.
/// </summary>
public interface IQueryResult : IExecutionResult
{
    /// <summary>
    /// A string that was passed to the label argument of the @defer or @stream 
    /// directive that corresponds to this results.
    /// </summary>
    string? Label { get; }

    /// <summary>
    ///  A path to the insertion point that informs the client how to patch a 
    /// subsequent delta payload into the original payload.
    /// </summary>
    Path? Path { get; }

    /// <summary>
    /// The data that is being delivered.
    /// </summary>
    IReadOnlyDictionary<string, object?>? Data { get; }

    /// <summary>
    /// A boolean that is present and <c>true</c> when there are more payloads 
    /// that will be sent for this operation. The last payload in a multi payload response 
    /// should return HasNext: <c>false</c>. 
    /// HasNext is null for single-payload responses to preserve backwards compatibility.
    /// </summary>
    bool? HasNext { get; }

    /// <summary>
    /// Serializes this GraphQL result into a dictionary.
    /// </summary>
    IReadOnlyDictionary<string, object?> ToDictionary();
}
