using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Text.Json;

namespace Metadata
{
    public class MetaSelector
    {
        public string KeyName { get; init; }
        public string Selector { get; init; }
        public string Func { get; init; }
        public string Param { get; init; }
    }
    
    public static class Preview
    {
        [Function("Preview")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req,
            string url,
            FunctionContext executionContext)
        {
            // TODO validate url, check allowed domains
            var logger = executionContext.GetLogger("Hook");
            logger.LogInformation("C# HTTP trigger function processed a request.");
            
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            var page = await browser.NewPageAsync();
            await page.GotoAsync(url);

            MetaSelector[] selectors =
            {
                new MetaSelector() {KeyName = "title", Selector = "head>title", Func = "InnerTextAsync"},
                new MetaSelector() {KeyName = "description", Selector = "head>meta[name=\"description\"]", Func = "GetAttributeAsync", Param = "content"},
                new MetaSelector() {KeyName = "image", Selector = "head>meta[property=\"og:image\"]", Func = "GetAttributeAsync", Param = "content"},
            };
            var map = new Dictionary<string, string>() {{"url", url}};
            
            foreach (MetaSelector metaSelector in selectors)
            {
                var element = await page.QuerySelectorAsync(metaSelector.Selector);
                var value = "";
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
            await browser.CloseAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            string jsonString = JsonSerializer.Serialize(map);  
            await response.WriteStringAsync(jsonString);

            return response;
            
        }
    }
}