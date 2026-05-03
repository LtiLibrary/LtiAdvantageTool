using System.ComponentModel.DataAnnotations;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using IdentityModel;
using Microsoft.AspNetCore.Mvc;

namespace AdvantageTool.Pages.Platforms;

public class PlatformInputModel
{
    public PlatformInputModel() { }

    public PlatformInputModel(HttpRequest request, IUrlHelper url, Platform? platform = null)
    {
        if (platform is null)
        {
            PlatformId = CryptoRandom.CreateUniqueId(8);
            var pair = PemHelper.GenerateRsaKeyPair();
            PrivateKey = pair.PrivateKey;
            PublicKey = pair.PublicKey;
            KeyId = pair.KeyId;
        }
        else
        {
            Id = platform.Id;
            Name = platform.Name;
            Issuer = platform.Issuer;
            AuthorizeUrl = platform.AuthorizeUrl;
            AccessTokenUrl = platform.AccessTokenUrl;
            JwkSetUrl = platform.JwkSetUrl;
            PlatformId = platform.PlatformId;
            ClientId = platform.ClientId;
            KeyId = platform.KeyId;
            PrivateKey = platform.PrivateKey;
            PublicKey = platform.PublicKey;
        }

        LaunchUrl = url.Page("/Tool", null, new { platformId = PlatformId }, request.Scheme)!;
        LoginUrl = url.Page("/OidcLogin", null, null, request.Scheme)!;
        DeepLinkingUrl = url.Page("/Tool", null, new { platformId = PlatformId }, request.Scheme)!;
        JwksUrl = $"{request.Scheme}://{request.Host}/jwks/{PlatformId}";
    }

    public int Id { get; set; }

    [Required, Display(Name = "Display name")] public string Name { get; set; } = string.Empty;
    [Required, Display(Name = "Issuer")] public string Issuer { get; set; } = string.Empty;
    [Required, Display(Name = "Authorize URL")] public string AuthorizeUrl { get; set; } = string.Empty;
    [Required, Display(Name = "Access token URL")] public string AccessTokenUrl { get; set; } = string.Empty;
    [Required, Display(Name = "JWKS URL (platform's)")] public string JwkSetUrl { get; set; } = string.Empty;
    [Required, Display(Name = "Client ID")] public string ClientId { get; set; } = string.Empty;

    public string PlatformId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    [Required] public string PrivateKey { get; set; } = string.Empty;
    [Required] public string PublicKey { get; set; } = string.Empty;

    public string LaunchUrl { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
    public string DeepLinkingUrl { get; set; } = string.Empty;
    public string JwksUrl { get; set; } = string.Empty;

    public void UpdateEntity(Platform p)
    {
        p.Name = Name;
        p.Issuer = Issuer;
        p.AuthorizeUrl = AuthorizeUrl;
        p.AccessTokenUrl = AccessTokenUrl;
        p.JwkSetUrl = JwkSetUrl;
        p.PlatformId = PlatformId;
        p.ClientId = ClientId;
        p.KeyId = KeyId;
        p.PrivateKey = PrivateKey;
        p.PublicKey = PublicKey;
    }
}
