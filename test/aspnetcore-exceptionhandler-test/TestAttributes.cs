﻿using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Frogvall.AspNetCore.ExceptionHandling.ExceptionHandling;
using Frogvall.AspNetCore.ExceptionHandling.Filters;
using Frogvall.AspNetCore.ExceptionHandling.Test.TestResources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace AspNetCoreApiUtilities.Tests
{
    public class TestAttributes
    {
        private readonly ITestOutputHelper _output;
        private HttpClient _client;

        public TestAttributes(ITestOutputHelper output)
        {
            _output = output;
            // Run for every test case
            SetupServer();
        }

        private void SetupServer()
        {
            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    var descriptor =
                        new ServiceDescriptor(
                            typeof(ILogger<ValidateModelFilter>),
                            TestLogger.Create<ValidateModelFilter>(_output));
                    services.Replace(descriptor);
                    services.AddExceptionMapper();
                    services.AddMvc(options =>
                    {
                        options.Filters.Add(new ValidateModelFilter { ErrorCode = 1337 });
                    });
                })
                .Configure(app =>
                {
                    app.UseApiExceptionHandler();
                    app.UseExceptionStatusCodeDecorator();
                    app.UseMvc();
                });

            var server = new TestServer(builder);
            _client = server.CreateClient();
        }

        [Fact(Skip = "Used to manually verify caching of SkipModelValidation")]
        public async Task PostTest_TestCache_ManualVerify()
        {
            //Arrange
            var content = new StringContent($@"{{""NullableObject"": ""string"", ""NonNullableObject"": 0}}", Encoding.UTF8, "text/json");

            // Act
            await _client.PostAsync("/api/Test/NoValidation", content);
            await _client.PostAsync("/api/Test/NoValidation", content);

            await _client.PostAsync("/api/Test", content);
            await _client.PostAsync("/api/Test", content);

            await _client.PostAsync("/api/Test/NoValidation", content);
            await _client.PostAsync("/api/Test", content);

        }


        [Fact]
        public async Task PostTest_NoValidation_ReturnsOk()
        {
            //Arrange
            var content = new StringContent($@"{{""NullableObject"": ""string"", ""NonNullableObject"": 0}}", Encoding.UTF8, "text/json");
            var content2 = new StringContent($@"{{""NullableObject"": ""string""}}", Encoding.UTF8, "text/json");

            // Act
            var response = await _client.PostAsync("/api/Test/NoValidation", content);

            // Assert
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task PostTest_DefaultIntDto_ReturnsBadRequest()
        {
            //Arrange
            var content = new StringContent($@"{{""NullableObject"": ""string"", ""NonNullableObject"": 0}}", Encoding.UTF8, "text/json");
            const string expectedError = "The NonNullableObject field requires a non-default value.";
            var expectedServiceName = Assembly.GetEntryAssembly().GetName().Name;

            // Act
            var response = await _client.PostAsync("/api/Test", content);
            var error = JsonConvert.DeserializeObject<ApiError>(await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.ErrorCode.Should().Be(1337);
            ((JObject)error.DeveloperContext)["NonNullableObject"].ToObject<string[]>().FirstOrDefault().Should().Be(expectedError);
            error.Service.Should().Be(expectedServiceName);
        }

        [Fact]
        public async Task PostTest_NoIntDto_ReturnsBadRequest()
        {
            //Arrange
            var content = new StringContent($@"{{""NullableObject"": ""string""}}", Encoding.UTF8, "text/json");
            const string expectedError = "The NonNullableObject field requires a non-default value.";
            var expectedServiceName = Assembly.GetEntryAssembly().GetName().Name;

            // Act
            var response = await _client.PostAsync("/api/Test", content);
            var error = JsonConvert.DeserializeObject<ApiError>(await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.ErrorCode.Should().Be(1337);
            ((JObject)error.DeveloperContext)["NonNullableObject"].ToObject<string[]>().FirstOrDefault().Should().Be(expectedError);
            error.Service.Should().Be(expectedServiceName);
        }

        [Fact]
        public async Task PostTest_NoStringDto_ReturnsBadRequest()
        {
            //Arrange
            var content = new StringContent($@"{{""NonNullableObject"": 1}}", Encoding.UTF8, "text/json");
            const string expectedError = "The NullableObject field is required.";
            var expectedServiceName = Assembly.GetEntryAssembly().GetName().Name;

            // Act
            var response = await _client.PostAsync("/api/Test", content);
            var error = JsonConvert.DeserializeObject<ApiError>(await response.Content.ReadAsStringAsync());

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.ErrorCode.Should().Be(1337);
            ((JObject)error.DeveloperContext)["NullableObject"].ToObject<string[]>().FirstOrDefault().Should().Be(expectedError);
            error.Service.Should().Be(expectedServiceName);
        }
    }
}