using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel;
using IdentityModel.Client;

namespace AdvantageTool.Utility
{
    /// <summary>
    /// Extensions for <see cref="TokenClient"/>.
    /// </summary>
    public static class TokenClientExtensions
    {
        /// <summary>
        /// Request a token based on client credentials with a signed JWT.
        /// </summary>
        /// <remarks>
        /// Based on https://www.imsglobal.org/spec/security/v1p0/#using-json-web-tokens-with-oauth-2-0-client-credentials-grant.
        /// </remarks>
        /// <param name="client">The client.</param>
        /// <param name="jwt">The signed JWT.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="extra">Extra parameters.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public static Task<TokenResponse> RequestClientCredentialsWithSignedJwtAsync(this TokenClient client, string jwt, string scope = null,
            object extra = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var fields = new Dictionary<string, string>
            {
                { OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.ClientCredentials },
                { OidcConstants.TokenRequest.ClientAssertionType, OidcConstants.ClientAssertionTypes.JwtBearer },
                { OidcConstants.TokenRequest.ClientAssertion, jwt }
            };

            if (!string.IsNullOrEmpty(scope))
            {
                fields.Add(OidcConstants.TokenRequest.Scope, scope);
            }

            return client.RequestAsync(client.Merge(fields, extra), cancellationToken);
        }
    }
}
