﻿using IdentityModel.AspNetCore.AccessTokenManagement;
using IdentityModel.Client;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Gateway.DelegatingHandlers
{
    public class TokenExchangeDelegatingHandler : DelegatingHandler
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IClientAccessTokenCache _clientAccessTokenCache;


        public TokenExchangeDelegatingHandler(IHttpClientFactory httpClientFactory,
            IClientAccessTokenCache clientAccessTokenCache)
        {
            _clientAccessTokenCache = clientAccessTokenCache;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> GetAccessToken(string incomingToken)
        {
            var item = await _clientAccessTokenCache
                .GetAsync("proeprestaurantgatewaytodownstreamtokenexchangeclient_api");
            var asd = DateTime.Now;
            if (item != null)
            {
                return item.AccessToken;
            }

            var (accessToken, expiresIn) = await ExchangeToken(incomingToken);

            await _clientAccessTokenCache.SetAsync(
                "proeprestaurantgatewaytodownstreamtokenexchangeclient_api",
                accessToken,
                expiresIn);

            return accessToken;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // extract the current token
            var incomingToken = request.Headers.Authorization.Parameter;

            // set the bearer token
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer",
                    await GetAccessToken(incomingToken));

            return await base.SendAsync(request, cancellationToken);
        }

        private async Task<(string, int)> ExchangeToken(string incomingToken)
        {
            var client = _httpClientFactory.CreateClient();

            var discoveryDocumentResponse = await client
                .GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
                {
                    Address = "https://localhost:5001",
                    Policy =
                    {
                        ValidateIssuerName = false,
                    },
                });
           
            if (discoveryDocumentResponse.IsError)
            {
                throw new Exception(discoveryDocumentResponse.Error);
            }

            var customParams = new Parameters(new Dictionary<string, string>
            {
                { "subject_token_type", "urn:ietf:params:oauth:token-type:access_token"},
                { "subject_token", incomingToken},
                { "scope", "openid profile meals.fullaccess orders.read" }
            });

            var tokenResponse = await client.RequestTokenAsync(new TokenRequest()
            {
                Address = discoveryDocumentResponse.TokenEndpoint,
                GrantType = "urn:ietf:params:oauth:grant-type:token-exchange",
                Parameters = customParams,
                ClientId = "proeprestaurantgatewaytodownstreamtokenexchangeclient",
                ClientSecret = "0cdea0bc-779e-4368-b46b-09956f70712c"
            });

            if (tokenResponse.IsError)
            {
                throw new Exception(tokenResponse.Error);
            }

            return (tokenResponse.AccessToken, tokenResponse.ExpiresIn);

        }
    }
}
