using Avalonia;
using Avalonia.Headless;
using Fterm.UI.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Fterm.UI.Tests;

public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class HeadlessTestApp : Application
{
}
