using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Metadata
{
    public class Tests
    {
        [Fact]
        public async Task ShouldReturnOk()
        {
            
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<ILoggerFactory, LoggerFactory>();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var context = new Mock<FunctionContext>();
            context.SetupProperty(c => c.InstanceServices, serviceProvider);
            
            var requestObj = new Mock<HttpRequestData>(context.Object);
            
            requestObj.Setup(r => r.Url).Returns(new Uri("http://localhost:7071/api/Preview?url=https%3A%2F%2Fwww.bandlab.com%2Feumonik"));
            
            requestObj.Setup(r => r.CreateResponse()).Returns(() =>
            {
                var response = new Mock<HttpResponseData>(context.Object);
                response.SetupProperty(r => r.Headers, new HttpHeadersCollection());
                response.SetupProperty(r => r.StatusCode);
                response.SetupProperty(r => r.Body, new MemoryStream());
                return response.Object;
            });
            
            var req = requestObj.Object;
            var logger = NullLoggerFactory.Instance.CreateLogger<Metadata.Preview>();
            var function = new Preview(logger);
            var response = await function.Run(req);

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}