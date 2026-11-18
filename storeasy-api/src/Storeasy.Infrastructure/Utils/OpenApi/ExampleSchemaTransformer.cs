using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Milvasoft.Components.Rest.Enums;
using Milvasoft.Components.Rest.Request;
using System.Text.Json;

namespace Storeasy.Infrastructure.Utils.OpenApi;

/// <summary>
/// Example schema transformer to add examples to OpenAPI schemas.
/// </summary>
public class ExampleSchemaTransformer : IOpenApiSchemaTransformer
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <inheritdoc/>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        if (type == null)
            return Task.CompletedTask;

        object exampleData = null;

        // 1. Define your examples as pure C# objects (Easy to read/write)
        if (type.IsAssignableTo(typeof(ListRequest)))
        {
            exampleData = new
            {
                pageNumber = 1,
                rowCount = 10,
                filtering = new
                {
                    Criterias = new[]
                    {
                        new {
                            FilterBy = "Id",
                            Value = 1,
                            OtherValue = (object)null,
                            Type = FilterType.EqualTo
                        }
                    }
                },
                sorting = new
                {
                    SortBy = "Id",
                    Type = SortType.Asc
                },
                aggregation = new
                {
                    Criterias = new[]
                    {
                        new {
                            AggregateBy = "Id",
                            Type = AggregationType.Max
                        }
                    }
                }
            };
        }

        // 2. Convert C# Object to OpenApiAny automatically
        if (exampleData != null)
        {
            var jsonNode = JsonSerializer.SerializeToNode(exampleData, _jsonSerializerOptions);

            schema.Example = jsonNode;
        }

        return Task.CompletedTask;
    }
}