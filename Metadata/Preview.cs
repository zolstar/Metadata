using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Metadata
{
    public class MetaSelector
    {
        public string KeyName { get; init; }
        public string Selector { get; init; }
        public string Func { get; init; }
        public string Param { get; init; }
    }

    public interface IPreviewResponse
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }
    }

    public record PreviewResponse() : IPreviewResponse
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }
    }

    public class Preview
    {
        private readonly ILogger<Preview> _logger;

        public Preview(ILogger<Preview> logger)
        {
            _logger = logger;
        }
        
        [Function("Preview")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "preview")] HttpRequestData req)
        {
            this._logger.LogInformation("C# HTTP trigger function processed a request.");
            
            var queryParameters = HttpUtility.ParseQueryString(req.Url.Query);
            var url = queryParameters.Get("url");

            // validate url
            if (url == null || !ValidateUrl(url))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            // check allowed hosts
            string[] allowedHost = {"www.bandlab.com"};
            var hostUri = new Uri(url);   
            var host = hostUri.Host;

            if (!allowedHost.Contains(host))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var result = await ExtractMetadata(url);
            Console.WriteLine(result.Title);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            string jsonString = JsonSerializer.Serialize(result);
            await response.WriteStringAsync(jsonString, Encoding.UTF8);
            return response;
        }

        private static async Task<IPreviewResponse> ExtractMetadata(string url)
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            var page = await browser.NewPageAsync();
            await page.GotoAsync(url);

            MetaSelector[] selectors =
            {
                new MetaSelector() {KeyName = nameof(PreviewResponse.Title), Selector = "head>title", Func = "InnerTextAsync"},
                new MetaSelector() {KeyName = nameof(PreviewResponse.Description), Selector = "head>meta[name=\"description\"]", Func = "GetAttributeAsync", Param = "content"},
                new MetaSelector() {KeyName = nameof(PreviewResponse.Image), Selector = "head>meta[property=\"og:image\"]", Func = "GetAttributeAsync", Param = "content"},
            };
            var map = new Dictionary<string, string>() {};
            foreach (MetaSelector metaSelector in selectors)
            {
                var element = await page.QuerySelectorAsync(metaSelector.Selector);
                string value = null;
                if (element != null)
                {
                    if (metaSelector.Func == "InnerTextAsync")
                    {
                        value = await element.InnerTextAsync();
                    }
                    if (metaSelector.Func == "GetAttributeAsync")
                    {
                        value = await element.GetAttributeAsync(metaSelector.Param);
                    }
                }

                map.Add(metaSelector.KeyName, value);
            }
            var result = new PreviewResponse
            {
                Url = url,
                Title = map[nameof(PreviewResponse.Title)],
                Description = map[nameof(PreviewResponse.Description)],
                Image = map[nameof(PreviewResponse.Image)],
            };
            await browser.CloseAsync();
            return result;
        }
        
        private static bool ValidateUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var validatedUri))
            {
                //If true: validatedUri contains a valid Uri. Check for the scheme in addition.
                return (validatedUri.Scheme == Uri.UriSchemeHttp || validatedUri.Scheme == Uri.UriSchemeHttps);
            }
            return false;
        }
    }
}