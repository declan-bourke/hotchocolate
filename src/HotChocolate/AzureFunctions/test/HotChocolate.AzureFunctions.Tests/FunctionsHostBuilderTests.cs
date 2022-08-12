using System;
using System.Threading.Tasks;
using HotChocolate.AzureFunctions.Tests.Helpers;
using HotChocolate.Types;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HotChocolate.AzureFunctions.Tests;
public class FunctionsHostBuilderTests
{
    [Fact]
    public async Task AzFuncInProcess_OriginalHostBuilderSetup()
    {
        var hostBuilder = new MockInProcessFunctionsHostBuilder();

        hostBuilder
            .AddGraphQLFunction()
            .AddQueryType(d => d.Name("Query").Field("test").Resolve("test"));

        AssertFunctionsHostBuilderIsValid(hostBuilder);
    }

    [Fact]
    public void AzFuncInProcess_HostBuilderSetupWithPortableConfigMatchingIsolatedProcess()
    {
        var hostBuilder = new MockInProcessFunctionsHostBuilder();

        //Register using the config func that matches the Isolated Process configuration so the config code is portable...
        hostBuilder.AddGraphQLFunction(graphQL =>
        {
            graphQL.AddQueryType(d => d.Name("Query").Field("test").Resolve("test"));
        });

        AssertFunctionsHostBuilderIsValid(hostBuilder);
    }

    private void AssertFunctionsHostBuilderIsValid(MockInProcessFunctionsHostBuilder hostBuilder)
    {
        ServiceProvider serviceProvider = hostBuilder.BuildServiceProvider();

        //The executor should resolve without error as a Required service...
        IGraphQLRequestExecutor requestExecutor = serviceProvider.GetRequiredService<IGraphQLRequestExecutor>();
    }
}
