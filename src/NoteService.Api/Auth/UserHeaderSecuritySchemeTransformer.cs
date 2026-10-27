using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace NoteService.Api.Auth;

/// <summary>Documents the <c>X-User-Id</c> header so the API reference UI can send it.</summary>
public class UserHeaderSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[HeaderAuthenticationHandler.SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = HeaderAuthenticationHandler.HeaderName,
            Description = "Identifier of the calling user.",
        };
        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(HeaderAuthenticationHandler.SchemeName, document)] = [],
            },
        ];

        return Task.CompletedTask;
    }
}
