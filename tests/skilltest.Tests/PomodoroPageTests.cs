using Microsoft.Playwright;
using Xunit;

namespace skilltest.Tests;

public sealed class PomodoroPageTests(ClockBrowserFixture fixture) : IClassFixture<ClockBrowserFixture>
{
    [Fact]
    public async Task VisitorCanOpenPomodoroAndSeeDefaultDurations()
    {
        await using var context = await fixture.CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(fixture.BaseUrl);
        await page.GetByRole(AriaRole.Link, new() { Name = "番茄鐘", Exact = true }).ClickAsync();

        Assert.Equal("/Pomodoro", new Uri(page.Url).AbsolutePath);
        Assert.Equal("番茄鐘", await page.GetByRole(AriaRole.Heading, new() { Name = "番茄鐘" }).InnerTextAsync());
        Assert.Equal("25", await page.GetByLabel("專注時間（分鐘）").InputValueAsync());
        Assert.Equal("5", await page.GetByLabel("短休息時間（分鐘）").InputValueAsync());
        Assert.Equal("20", await page.GetByLabel("長休息時間（分鐘）").InputValueAsync());
    }

    [Fact]
    public async Task VisitorCanSaveFocusItemAndDurationsInThisBrowser()
    {
        await using var context = await fixture.CreateContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        await page.GetByLabel("專注事項").FillAsync("完成提案");
        await page.GetByLabel("專注時間（分鐘）").FillAsync("30");
        await page.GetByLabel("短休息時間（分鐘）").FillAsync("6");
        await page.GetByLabel("長休息時間（分鐘）").FillAsync("15");
        await page.ReloadAsync();

        Assert.Equal("完成提案", await page.GetByLabel("專注事項").InputValueAsync());
        Assert.Equal("30", await page.GetByLabel("專注時間（分鐘）").InputValueAsync());
        Assert.Equal("6", await page.GetByLabel("短休息時間（分鐘）").InputValueAsync());
        Assert.Equal("15", await page.GetByLabel("長休息時間（分鐘）").InputValueAsync());
    }

    [Fact]
    public async Task VisitorCanPauseAndResumeAFocusSession()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        await page.GetByLabel("專注時間（分鐘）").FillAsync("1");
        await page.GetByRole(AriaRole.Button, new() { Name = "開始專注" }).ClickAsync();

        Assert.Equal("01:00", await page.GetByLabel("剩餘時間").InnerTextAsync());
        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 20000)");
        await page.WaitForFunctionAsync("() => document.querySelector('[aria-label=\"剩餘時間\"]')?.textContent === '00:40'");
        await page.GetByRole(AriaRole.Button, new() { Name = "暫停" }).ClickAsync();

        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 120000)");
        await page.WaitForTimeoutAsync(350);
        Assert.Equal("00:40", await page.GetByLabel("剩餘時間").InnerTextAsync());

        await page.GetByRole(AriaRole.Button, new() { Name = "繼續" }).ClickAsync();
        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 10000)");
        await page.WaitForFunctionAsync("() => document.querySelector('[aria-label=\"剩餘時間\"]')?.textContent === '00:30'");
    }

    [Fact]
    public async Task ResettingAnIncompleteFocusRestoresItsTimeWithoutCountingAPomodoro()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        await page.GetByLabel("專注時間（分鐘）").FillAsync("1");
        await page.GetByRole(AriaRole.Button, new() { Name = "開始專注" }).ClickAsync();
        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 20000)");
        await page.WaitForFunctionAsync("() => document.querySelector('[aria-label=\"剩餘時間\"]')?.textContent === '00:40'");

        await page.GetByRole(AriaRole.Button, new() { Name = "重設此段" }).ClickAsync();

        Assert.Equal("01:00", await page.GetByLabel("剩餘時間").InnerTextAsync());
        Assert.Equal("0", await page.Locator("#pomodoro-completed").InnerTextAsync());
        Assert.Equal("準備開始專注", await page.Locator("#pomodoro-phase").InnerTextAsync());
    }

    [Fact]
    public async Task CompletingFocusStartsShortRestAndCountsOnePomodoro()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        await page.GetByLabel("專注時間（分鐘）").FillAsync("1");
        await page.GetByLabel("短休息時間（分鐘）").FillAsync("1");
        await page.GetByRole(AriaRole.Button, new() { Name = "開始專注" }).ClickAsync();
        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 60000)");
        await page.WaitForFunctionAsync("() => document.querySelector('#pomodoro-phase')?.textContent === '短休息中'");

        Assert.Equal("01:00", await page.GetByLabel("剩餘時間").InnerTextAsync());
        Assert.Equal("1", await page.Locator("#pomodoro-completed").InnerTextAsync());
        Assert.False(await page.GetByRole(AriaRole.Button, new() { Name = "暫停" }).IsVisibleAsync());
        Assert.False(await page.GetByRole(AriaRole.Button, new() { Name = "重設此段" }).IsVisibleAsync());
    }

    [Fact]
    public async Task LeavingThePagePausesFocusAndPreservesItsRemainingTime()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        await page.GetByLabel("專注時間（分鐘）").FillAsync("1");
        await page.GetByRole(AriaRole.Button, new() { Name = "開始專注" }).ClickAsync();
        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 20000)");
        await page.WaitForFunctionAsync("() => document.querySelector('[aria-label=\"剩餘時間\"]')?.textContent === '00:40'");
        await page.EvaluateAsync("() => window.dispatchEvent(new Event('pagehide'))");
        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 120000)");
        await page.ReloadAsync();

        Assert.Equal("00:40", await page.GetByLabel("剩餘時間").InnerTextAsync());
        Assert.Equal("專注已暫停", await page.Locator("#pomodoro-phase").InnerTextAsync());
    }

    [Fact]
    public async Task ResettingTheCycleRequiresConfirmationAndKeepsTheFocusItem()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        var page = await context.NewPageAsync();
        page.Dialog += async (_, dialog) => await dialog.AcceptAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        await page.GetByLabel("專注事項").FillAsync("完成報告");
        await page.GetByLabel("專注時間（分鐘）").FillAsync("1");
        await page.GetByLabel("短休息時間（分鐘）").FillAsync("1");
        await page.GetByRole(AriaRole.Button, new() { Name = "開始專注" }).ClickAsync();
        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 60000)");
        await page.WaitForFunctionAsync("() => document.querySelector('#pomodoro-phase')?.textContent === '短休息中'");
        Assert.False(await page.GetByRole(AriaRole.Button, new() { Name = "重設循環" }).IsVisibleAsync());

        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 60000)");
        await page.WaitForFunctionAsync("() => document.querySelector('#pomodoro-phase')?.textContent === '短休息結束，準備開始下一段專注'");
        Assert.Equal("1", await page.Locator("#pomodoro-completed").InnerTextAsync());

        await page.GetByRole(AriaRole.Button, new() { Name = "重設循環" }).ClickAsync();

        Assert.Equal("0", await page.Locator("#pomodoro-completed").InnerTextAsync());
        Assert.Equal("完成報告", await page.GetByLabel("專注事項").InputValueAsync());
        Assert.Equal("準備開始專注", await page.Locator("#pomodoro-phase").InnerTextAsync());
    }

    [Fact]
    public async Task TimerTransitionsShowAPagePromptAtTheEndOfEachPhase()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        await page.GetByLabel("專注時間（分鐘）").FillAsync("1");
        await page.GetByLabel("短休息時間（分鐘）").FillAsync("1");
        await page.GetByRole(AriaRole.Button, new() { Name = "開始專注" }).ClickAsync();
        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 60000)");
        await page.WaitForFunctionAsync("() => document.querySelector('#pomodoro-phase')?.textContent === '短休息中'");

        Assert.Equal("專注時間結束，開始短休息。", await page.Locator("#timer-message").InnerTextAsync());

        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 60000)");
        await page.WaitForFunctionAsync("() => document.querySelector('#pomodoro-phase')?.textContent === '短休息結束，準備開始下一段專注'");
        Assert.Equal("短休息結束，準備開始下一段專注。", await page.Locator("#timer-message").InnerTextAsync());
    }

    [Fact]
    public async Task SoundAndNotificationPreferencesAreSavedAndNotificationsAreOptIn()
    {
        await using var context = await fixture.CreateContextAsync();
        await context.AddInitScriptAsync("""
            const permissionKey = "skilltest.tests.notificationPermission";
            window.notificationRequestAttempted = false;
            class TestNotification {
                static get permission() {
                    return sessionStorage.getItem(permissionKey) ?? "default";
                }
                static async requestPermission() {
                    window.notificationRequestAttempted = true;
                    sessionStorage.setItem(permissionKey, "granted");
                    return "granted";
                }
                constructor() {}
            }
            window.Notification = TestNotification;
            """);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        Assert.False(await page.EvaluateAsync<bool>("() => window.notificationRequestAttempted"));
        await page.GetByLabel("靜音").CheckAsync();
        await page.GetByLabel("啟用桌面通知").CheckAsync();

        Assert.True(await page.EvaluateAsync<bool>("() => window.notificationRequestAttempted"));
        await page.ReloadAsync();

        Assert.True(await page.GetByLabel("靜音").IsCheckedAsync());
        Assert.True(await page.GetByLabel("啟用桌面通知").IsCheckedAsync());
        Assert.False(await page.EvaluateAsync<bool>("() => window.notificationRequestAttempted"));
    }

    [Fact]
    public async Task UnmutingDuringFocusPreparesAudioForTheNextTimerAlert()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        await context.AddInitScriptAsync("""
            window.audioContextReady = false;
            window.audioAlertPlayed = false;
            class TestAudioContext {
                constructor() {
                    window.audioContextReady = true;
                    this.currentTime = 0;
                    this.state = "running";
                    this.destination = {};
                }
                createOscillator() {
                    return {
                        frequency: { setValueAtTime() {} },
                        connect() {},
                        start() { window.audioAlertPlayed = true; },
                        stop() {}
                    };
                }
                createGain() {
                    return {
                        gain: { setValueAtTime() {}, exponentialRampToValueAtTime() {} },
                        connect() {}
                    };
                }
            }
            window.AudioContext = TestAudioContext;
            """);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        await page.GetByLabel("靜音").CheckAsync();
        await page.GetByLabel("專注時間（分鐘）").FillAsync("1");
        await page.GetByRole(AriaRole.Button, new() { Name = "開始專注" }).ClickAsync();
        Assert.False(await page.EvaluateAsync<bool>("() => window.audioContextReady"));

        await page.GetByLabel("靜音").UncheckAsync();
        Assert.True(await page.EvaluateAsync<bool>("() => window.audioContextReady"));
        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 60000)");
        await page.WaitForFunctionAsync("() => document.querySelector('#pomodoro-phase')?.textContent === '短休息中'");

        Assert.True(await page.EvaluateAsync<bool>("() => window.audioAlertPlayed"));
    }

    [Fact]
    public async Task ActiveFocusKeepsItsDurationAndTaskWhileNewSettingsApplyToLaterPhases()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        await page.GetByLabel("專注事項").FillAsync("撰寫文件");
        await page.GetByLabel("專注時間（分鐘）").FillAsync("1");
        await page.GetByLabel("短休息時間（分鐘）").FillAsync("1");
        await page.GetByRole(AriaRole.Button, new() { Name = "開始專注" }).ClickAsync();
        Assert.False(await page.GetByLabel("專注事項").IsEditableAsync());

        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 10000)");
        await page.GetByLabel("專注時間（分鐘）").FillAsync("2");
        await page.GetByLabel("短休息時間（分鐘）").FillAsync("2");
        await page.WaitForFunctionAsync("() => document.querySelector('[aria-label=\"剩餘時間\"]')?.textContent === '00:50'");
        Assert.Equal("撰寫文件", await page.GetByLabel("專注事項").InputValueAsync());

        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 50000)");
        await page.WaitForFunctionAsync("() => document.querySelector('#pomodoro-phase')?.textContent === '短休息中'");
        Assert.Equal("02:00", await page.GetByLabel("剩餘時間").InnerTextAsync());
        Assert.True(await page.GetByLabel("專注事項").IsEditableAsync());
        await page.GetByLabel("專注事項").FillAsync("準備下一份文件");

        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 120000)");
        await page.WaitForFunctionAsync("() => document.querySelector('#pomodoro-phase')?.textContent === '短休息結束，準備開始下一段專注'");
        Assert.Equal("02:00", await page.GetByLabel("剩餘時間").InnerTextAsync());
        Assert.Equal("準備下一份文件", await page.GetByLabel("專注事項").InputValueAsync());
    }

    [Fact]
    public async Task FourthPomodoroStartsLongRestAndLongRestStartsANewCycle()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        await page.GetByLabel("專注時間（分鐘）").FillAsync("1");
        await page.GetByLabel("短休息時間（分鐘）").FillAsync("1");
        await page.GetByLabel("長休息時間（分鐘）").FillAsync("2");

        for (var completed = 1; completed <= 4; completed++)
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "開始專注" }).ClickAsync();
            await page.EvaluateAsync("() => window.setClockTime(Date.now() + 60000)");
            var expectedBreak = completed == 4 ? "長休息中" : "短休息中";
            await page.WaitForFunctionAsync(
                "expected => document.querySelector('#pomodoro-phase')?.textContent === expected",
                expectedBreak);
            Assert.Equal(completed.ToString(), await page.Locator("#pomodoro-completed").InnerTextAsync());

            if (completed < 4)
            {
                Assert.False(await page.GetByRole(AriaRole.Button, new() { Name = "暫停" }).IsVisibleAsync());
                await page.EvaluateAsync("() => window.setClockTime(Date.now() + 60000)");
                await page.WaitForFunctionAsync(
                    "() => document.querySelector('#pomodoro-phase')?.textContent === '短休息結束，準備開始下一段專注'");
            }
        }

        Assert.Equal("02:00", await page.GetByLabel("剩餘時間").InnerTextAsync());
        Assert.False(await page.GetByRole(AriaRole.Button, new() { Name = "暫停" }).IsVisibleAsync());
        Assert.False(await page.GetByRole(AriaRole.Button, new() { Name = "重設此段" }).IsVisibleAsync());

        await page.EvaluateAsync("() => window.setClockTime(Date.now() + 120000)");
        await page.WaitForFunctionAsync(
            "() => document.querySelector('#pomodoro-phase')?.textContent === '長休息結束，新循環已開始'");

        Assert.Equal("0", await page.Locator("#pomodoro-completed").InnerTextAsync());
        Assert.Equal("01:00", await page.GetByLabel("剩餘時間").InnerTextAsync());
    }

    [Fact]
    public async Task BackgroundTimeAdvancesThroughElapsedFocusAndRestBeforeTheNextFocus()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var context = await fixture.CreateContextAsync("UTC", initialTime);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Pomodoro");
        await page.GetByLabel("專注時間（分鐘）").FillAsync("1");
        await page.GetByLabel("短休息時間（分鐘）").FillAsync("1");
        await page.GetByRole(AriaRole.Button, new() { Name = "開始專注" }).ClickAsync();
        await page.EvaluateAsync("""
            () => {
                Object.defineProperty(document, "visibilityState", { configurable: true, value: "hidden" });
                document.dispatchEvent(new Event("visibilitychange"));
                window.setClockTime(Date.now() + 180000);
                Object.defineProperty(document, "visibilityState", { configurable: true, value: "visible" });
                document.dispatchEvent(new Event("visibilitychange"));
            }
            """);
        await page.WaitForTimeoutAsync(350);

        Assert.Equal("短休息結束，準備開始下一段專注", await page.Locator("#pomodoro-phase").InnerTextAsync());
        Assert.Equal("1", await page.Locator("#pomodoro-completed").InnerTextAsync());
        Assert.Equal("01:00", await page.GetByLabel("剩餘時間").InnerTextAsync());
    }
}
