using Xunit;

namespace PMS.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<PmsWebApplicationFactory>
{
    public const string Name = "Integration";
}