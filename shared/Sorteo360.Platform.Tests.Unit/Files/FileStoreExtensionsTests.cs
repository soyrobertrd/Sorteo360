using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Platform.Files;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Files;

public sealed class FileStoreExtensionsTests
{
    [Fact]
    public void AddFileStore_RegistersTheFilesystemProvider_WhenItIsTheOneConfigured()
    {
        var services = new ServiceCollection();

        services.AddFileStore(ConfigurationOf(new()
        {
            ["FileStore:Provider"] = FilesystemFileStore.ProviderName,
            ["FileStore:Filesystem:RootPath"] = Path.Combine(Path.GetTempPath(), "pgti360-files")
        }));

        services.BuildServiceProvider()
            .GetRequiredService<IFileStore>()
            .ShouldBeOfType<FilesystemFileStore>();
    }

    [Fact]
    public void AddFileStore_Throws_WhenNoProviderIsConfigured()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<InvalidOperationException>(
            () => services.AddFileStore(ConfigurationOf([])));

        exception.Message.ShouldContain("FileStore:Provider");
    }

    [Fact]
    public void AddFileStore_Throws_WhenTheProviderIsUnknown()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<InvalidOperationException>(
            () => services.AddFileStore(ConfigurationOf(new() { ["FileStore:Provider"] = "onedrive" })));

        exception.Message.ShouldContain("onedrive");
    }

    [Fact]
    public void AddFileStore_Throws_WhenTheRootPathIsMissing()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<InvalidOperationException>(
            () => services.AddFileStore(ConfigurationOf(new()
            {
                ["FileStore:Provider"] = FilesystemFileStore.ProviderName
            })));

        exception.Message.ShouldContain("FileStore:Filesystem:RootPath");
    }

    private static IConfiguration ConfigurationOf(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
