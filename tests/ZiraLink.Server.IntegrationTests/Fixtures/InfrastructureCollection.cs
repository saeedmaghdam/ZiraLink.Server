using System.Diagnostics.CodeAnalysis;

namespace ZiraLink.Server.IntegrationTests.Fixtures
{
    [ExcludeFromCodeCoverage]
    [CollectionDefinition("Infrastructure Collection")]
    public class InfrastructureCollection : ICollectionFixture<InfrastructureFixture> { }
}
