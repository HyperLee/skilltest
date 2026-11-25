using Microsoft.Playwright;
using Xunit;

namespace skilltest.Tests;

public sealed class ClockPageTests(ClockBrowserFixture fixture) : IClassFixture<ClockBrowserFixture>
{
    [Fact]
    public async Task VisitorCanOpenClockFromHomeAndReturnHome()
    {
        await using var context = await fixture.CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(fixture.BaseUrl);
        await page.GetByRole(AriaRole.Link, new() { Name = "現在時間", Exact = true }).ClickAsync();

        Assert.Equal("/Clock", new Uri(page.Url).AbsolutePath);
        Assert.Equal(
            "現在時間",
            await page.GetByRole(AriaRole.Heading, new() { Name = "現在時間" }).InnerTextAsync());

        await page.GetByRole(AriaRole.Link, new() { Name = "回首頁" }).ClickAsync();

        Assert.Equal("/", new Uri(page.Url).AbsolutePath);
        Assert.Equal(
            "現在時間，一眼掌握",
            await page.GetByRole(AriaRole.Heading, new() { Name = "現在時間，一眼掌握" }).InnerTextAsync());
    }

    [Fact]
    public async Task VisitorCanSearchSelectAndKeepATimeZone()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 30, 40, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("America/Los_Angeles", initialTime);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Clock");

        Assert.Equal("2025/12/31", await page.GetByLabel("目前日期").InnerTextAsync());
        Assert.Equal("16:30:40", await page.GetByLabel("目前時間").InnerTextAsync());
        Assert.Equal(
            "洛杉磯 · UTC-8 · America/Los_Angeles",
            await page.Locator("#clock-zone").InnerTextAsync());

        await page.GetByRole(AriaRole.Button, new() { Name = "切換時區" }).ClickAsync();
        await page.GetByRole(AriaRole.Searchbox, new() { Name = "搜尋時區" }).FillAsync("Asia/Tokyo");
        await page.GetByText("東京", new() { Exact = true }).ClickAsync();

        Assert.Equal("2026/01/01", await page.GetByLabel("目前日期").InnerTextAsync());
        Assert.Equal("09:30:40", await page.GetByLabel("目前時間").InnerTextAsync());
        Assert.Equal("東京 · UTC+9 · Asia/Tokyo", await page.Locator("#clock-zone").InnerTextAsync());

        await page.ReloadAsync();

        Assert.Equal("東京 · UTC+9 · Asia/Tokyo", await page.Locator("#clock-zone").InnerTextAsync());
        Assert.Equal("09:30:40", await page.GetByLabel("目前時間").InnerTextAsync());
    }

    [Fact]
    public async Task ClockRefreshesItsDisplayedTimeEverySecond()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 30, 40, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Clock");

        Assert.Equal("00:30:40", await page.GetByLabel("目前時間").InnerTextAsync());
        await page.EvaluateAsync(
            "iso => window.setClockTime(Date.parse(iso))",
            "2026-01-01T00:30:41Z");
        await WaitForTextAsync(page.GetByLabel("目前時間"), "00:30:41");
    }

    [Fact]
    public async Task NewYorkOffsetFollowsItsDaylightSavingRules()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 30, 40, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Clock");
        await page.GetByRole(AriaRole.Button, new() { Name = "切換時區" }).ClickAsync();
        await page.GetByRole(AriaRole.Searchbox, new() { Name = "搜尋時區" }).FillAsync("America/New_York");
        await page.GetByText("紐約", new() { Exact = true }).ClickAsync();

        Assert.Equal("紐約 · UTC-5 · America/New_York", await page.Locator("#clock-zone").InnerTextAsync());
        Assert.Equal("19:30:40", await page.GetByLabel("目前時間").InnerTextAsync());

        await page.EvaluateAsync(
            "iso => window.setClockTime(Date.parse(iso))",
            "2026-07-01T12:00:00Z");
        await WaitForTextAsync(page.Locator("#clock-zone"), "紐約 · UTC-4 · America/New_York");

        Assert.Equal("2026/07/01", await page.GetByLabel("目前日期").InnerTextAsync());
        Assert.Equal("08:00:00", await page.GetByLabel("目前時間").InnerTextAsync());
    }

    [Fact]
    public async Task UnsupportedSavedAndDetectedZonesFallBackToTaipei()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 30, 40, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        await context.AddInitScriptAsync("""
            localStorage.setItem("skilltest.selectedTimeZone", "Invalid/SavedZone");
            const originalResolvedOptions = Intl.DateTimeFormat.prototype.resolvedOptions;
            Intl.DateTimeFormat.prototype.resolvedOptions = function () {
                return { ...originalResolvedOptions.call(this), timeZone: "Invalid/DetectedZone" };
            };
            """);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Clock");

        Assert.Equal("2026/01/01", await page.GetByLabel("目前日期").InnerTextAsync());
        Assert.Equal("08:30:40", await page.GetByLabel("目前時間").InnerTextAsync());
        Assert.Equal("台北 · UTC+8 · Asia/Taipei", await page.Locator("#clock-zone").InnerTextAsync());
    }

    private static async Task WaitForTextAsync(ILocator locator, string expectedText)
    {
        var deadline = DateTime.UtcNow.AddSeconds(4);
        var actualText = await locator.InnerTextAsync();

        while (actualText != expectedText && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
            actualText = await locator.InnerTextAsync();
        }

        Assert.Equal(expectedText, actualText);
    }
}
