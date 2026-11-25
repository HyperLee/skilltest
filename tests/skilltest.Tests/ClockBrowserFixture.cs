using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;
using Xunit;

namespace skilltest.Tests;

public sealed class ClockBrowserFixture : IAsyncLifetime
{
    private readonly List<string> _serverOutput = [];
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private Process? _serverProcess;

    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var projectRoot = FindWebProjectRoot();
        var testOutputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var configuration = testOutputDirectory.Parent?.Name ?? "Debug";
        var targetFramework = testOutputDirectory.Name;
        var appAssembly = Path.Combine(projectRoot, "bin", configuration, targetFramework, "skilltest.dll");

        if (!File.Exists(appAssembly))
        {
            throw new FileNotFoundException("The web application build output was not found.", appAssembly);
        }

        var port = GetAvailablePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(appAssembly);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(BaseUrl);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_CONTENTROOT"] = projectRoot;
        startInfo.Environment["ASPNETCORE_WEBROOT"] = Path.Combine(projectRoot, "wwwroot");

        _serverProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the web application.");
        _serverProcess.OutputDataReceived += CaptureServerOutput;
        _serverProcess.ErrorDataReceived += CaptureServerOutput;
        _serverProcess.BeginOutputReadLine();
        _serverProcess.BeginErrorReadLine();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();

        await WaitForServerAsync();
    }

    public async Task<IBrowserContext> CreateContextAsync(
        string timeZoneId = "UTC",
        DateTimeOffset? initialTime = null)
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            TimezoneId = timeZoneId
        });

        if (initialTime is not null)
        {
            var initialTimestamp = initialTime.Value.ToUnixTimeMilliseconds();
            var clockScript = $$"""
                (() => {
                    const NativeDate = Date;
                    let currentTime = {{initialTimestamp}};

                    class ControlledDate extends NativeDate {
                        constructor(...args) {
                            if (args.length === 0) {
                                super(currentTime);
                            } else {
                                super(...args);
                            }
                        }

                        static now() {
                            return currentTime;
                        }
                    }

                    globalThis.setClockTime = (milliseconds) => {
                        currentTime = milliseconds;
                    };
                    globalThis.Date = ControlledDate;
                })();
                """;

            await context.AddInitScriptAsync(clockScript);
        }

        return context;
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_serverProcess is { HasExited: false })
        {
            _serverProcess.Kill(entireProcessTree: true);
            await _serverProcess.WaitForExitAsync();
        }

        _serverProcess?.Dispose();
    }

    private IBrowser Browser => _browser
        ?? throw new InvalidOperationException("The browser has not been initialized.");

    private async Task WaitForServerAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var deadline = DateTime.UtcNow.AddSeconds(20);

        while (DateTime.UtcNow < deadline)
        {
            if (_serverProcess?.HasExited == true)
            {
                throw new InvalidOperationException(
                    $"The web application exited before becoming ready.{Environment.NewLine}{GetServerOutput()}");
            }

            try
            {
                using var response = await client.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Kestrel may need a short time to bind the selected port.
            }
            catch (TaskCanceledException)
            {
                // Retry while the application is starting.
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"The web application did not become ready at {BaseUrl}.{Environment.NewLine}{GetServerOutput()}");
    }

    private void CaptureServerOutput(object sender, DataReceivedEventArgs eventArgs)
    {
        if (!string.IsNullOrWhiteSpace(eventArgs.Data))
        {
            lock (_serverOutput)
            {
                _serverOutput.Add(eventArgs.Data);
            }
        }
    }

    private string GetServerOutput()
    {
        lock (_serverOutput)
        {
            return string.Join(Environment.NewLine, _serverOutput);
        }
    }

    private static string FindWebProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "skilltest.csproj")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate skilltest.csproj from the test output directory.");
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
