using Azure.Core;
using Azure.Identity;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

internal sealed class OfflineTokenCredential : TokenCredential
{
    public TaskCompletionSource<string[]> RequestedScopes { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        throw GetTokenFailure(requestContext);
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return ValueTask.FromException<AccessToken>(GetTokenFailure(requestContext));
    }

    private Exception GetTokenFailure(TokenRequestContext requestContext)
    {
        RequestedScopes.TrySetResult(requestContext.Scopes);
        // Cosmos eagerly acquires a token during construction. Fail locally before any identity or account I/O.
        return new CredentialUnavailableException("Token acquisition is disabled in offline transport tests.");
    }
}
