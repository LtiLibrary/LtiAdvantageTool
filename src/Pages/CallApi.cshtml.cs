using System;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace AdvantageTool.Pages
{
    public class CallApiModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly IDiscoveryCache _discoveryCache;
        private readonly IHttpClientFactory _httpClientFactory;

        public CallApiModel(IConfiguration config, IDiscoveryCache discoveryCache, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _discoveryCache = discoveryCache;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> OnGet()
        {
            var disco = await _discoveryCache.GetAsync();
            if (disco.IsError)
            {
                Console.WriteLine(disco.Error);
                return Page();
            }

            var tokenClient = new TokenClient(disco.TokenEndpoint, "5a69e6484db14f8b91536eed969c24ea", "kjYPV8rkQwCh8zrJetVoVF4V5U_z6wn8");
            var tokenResponse = await tokenClient.RequestClientCredentialsAsync("api1");
            if (tokenResponse.IsError)
            {
                Console.WriteLine(tokenResponse.Error);
                return Page();
            }

            var client = _httpClientFactory.CreateClient();
            client.SetBearerToken(tokenResponse.AccessToken);

            var response = await client.GetStringAsync(_config["Authority"] + "/identity");
            ViewData["Json"] = JArray.Parse(response).ToString();

            return Page();
        }
    }
}