﻿using CommunityToolkit.Mvvm.DependencyInjection;
using System.Threading.Tasks;
using FluentStore.Services;
using OwlCore.AbstractUI.Models;
using System.Collections.Generic;
using Flurl;
using IdentityModel.OidcClient;
using System.Security.Claims;
using System;

namespace FluentStore.SDK.Users
{
    public abstract class OpenIDAccountHandler<TAccount> : AccountHandlerBase<TAccount> where TAccount : Account
    {
        private const string OPENID_SCOPES = "openid email profile offline_access";

        protected string Token { get; private set; }
        protected string RefreshToken { get; private set; }
        protected string AccessToken { get; private set; }
        protected string[] Scopes { get; set; }

        protected abstract string Authority { get; }
        protected abstract string ClientId { get; }
        protected abstract string ClientSecret { get; }

        private OidcClient _client;
        private AuthorizeState _state;

        /// <summary>
        /// Called when sign-in is successful.
        /// </summary>
        protected virtual Task OnSignInSuccess() => Task.CompletedTask;

        /// <summary>
        /// Called when sign-out is successful.
        /// </summary>
        protected virtual Task OnSignOut() => Task.CompletedTask;

        public override Task<bool> SignInAsync(CredentialBase credential) => SignInAsync(null, credential.Password);

        /// <summary>
        /// Sign in using the supplied token and refresh token.
        /// </summary>
        /// <remarks>
        /// If <paramref name="token"/> is <see langword="null"/>,
        /// then <paramref name="refreshToken"/> will be used to
        /// get a new token.
        /// </remarks>
        /// <returns>Whether the sign-in succeeded.</returns>
        protected async Task<bool> SignInAsync(string token, string refreshToken)
        {
            try
            {
                Token = token;
                RefreshToken = refreshToken;

                if (Token == null)
                {
                    // Use refresh token to get a new token
                    var resp = await _client.RefreshTokenAsync(RefreshToken);
                    if (resp != null)
                    {
                        Token = resp.AccessToken;
                        RefreshToken = resp.RefreshToken;
                    }
                    else
                    {
#if DEBUG
                        System.Diagnostics.Debug.WriteLine(resp);
#endif
                    }
                }

                CurrentUser = await UpdateAccount();
                await OnSignInSuccess();

                if (RefreshToken != null)
                    SaveCredential(RefreshToken);

                IsLoggedIn = true;
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
#endif

                Token = null;
                RefreshToken = null;
                IsLoggedIn = false;
            }

            return IsLoggedIn;
        }

        public override async Task SignOutAsync()
        {
            await _client.LogoutAsync();

            await OnSignOut();

            RemoveCredential(RefreshToken);

            IsLoggedIn = false;
            _client = null;
            _state = null;
            Token = RefreshToken = null;
            CurrentUser = null;
        }

        protected override AbstractUICollection CreateSignInForm()
        {
            AbstractButton signInButton = new("SignInButton", "Sign in with browser", iconCode: "\uE8A7", type: AbstractButtonType.Confirm);
            signInButton.Clicked += async (sender, e) =>
            {
                // Generate start URL, state, nonce, code challenge
                RefreshClient();
                _state = await _client.PrepareLoginAsync();

                INavigationService navService = Ioc.Default.GetRequiredService<INavigationService>();
                await navService.OpenInBrowser(_state.StartUrl);
            };

            AbstractUICollection ui = new("SignInCollection")
            {
                signInButton,
            };
            return ui;
        }

        protected override AbstractUICollection CreateSignUpForm()
        {
            AbstractButton signUpButton = new("SignUpButton", "Sign up with browser", iconCode: "\uE8A7", type: AbstractButtonType.Confirm);
            signUpButton.Clicked += async (sender, e) =>
            {
                INavigationService navService = Ioc.Default.GetRequiredService<INavigationService>();
                await navService.OpenInBrowser("https://signup.live.com");
            };

            AbstractUICollection ui = new("SignUpCollection")
            {
                signUpButton
            };
            return ui;
        }

        public override async Task HandleAuthActivation(Url url)
        {
            var result = await _client.ProcessResponseAsync(url.ToString(), _state);
            await SignInAsync(result.AccessToken, result.RefreshToken);
        }

        public async Task<IEnumerable<Claim>> GetOpenIDClaims()
        {
            var result = await _client.GetUserInfoAsync(Token);
            return result.Claims;
        }

        private void RefreshClient()
        {
            OidcClientOptions options = new()
            {
                Authority = Authority,
                ClientId = ClientId,
                RedirectUri = GetAuthProtocolUrl(null),
                Scope = OPENID_SCOPES,
            };
            if (Scopes != null)
                options.Scope += " " + string.Join(' ', Scopes);

            options.Policy.Discovery.ValidateIssuerName = false;
            options.Policy.Discovery.ValidateEndpoints = false;
            options.Policy.ValidateTokenIssuerName = false;

            _client = new(options);
        }
    }
}
