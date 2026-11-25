# skilltest

skilltest 是一個使用 ASP.NET Core Razor Pages 的繁體中文網站，提供可切換時區的「現在時間」，以及每四個番茄為一個循環的「番茄鐘」。兩項工具以桌面瀏覽器為使用對象。

## 快速開始

安裝 .NET 11 SDK（專案目標為 `net11.0`），再到專案根目錄啟動網站。

```powershell
dotnet run --project .\skilltest.csproj --launch-profile https
```

在瀏覽器開啟 [https://localhost:7198](https://localhost:7198)。若瀏覽器提示本機開發憑證未受信任，可執行 `dotnet dev-certs https --trust` 後重新開啟。結束網站時，在終端機按 `Ctrl+C`。

| 頁面 | 路徑 | 用途 |
| --- | --- | --- |
| 首頁 | `/` | 開啟現在時間或番茄鐘 |
| 現在時間 | `/Clock` | 查看目前時間、日期和所選時區 |
| 番茄鐘 | `/Pomodoro` | 設定專注與休息時間，追蹤目前循環進度 |

## 現在時間

從首頁點選「現在時間」。頁面每秒讀取裝置的系統時間，依所選時區顯示 `yyyy/MM/dd` 日期、`HH:mm:ss` 24 小時制時間，以及城市、UTC 偏移量與 IANA 時區識別碼，例如 `台北 · UTC+8 · Asia/Taipei`。

- 點選「切換時區」，可用城市名稱、IANA 識別碼或 UTC 偏移量搜尋。選取後立即更新，並保存選擇。
- 開啟頁面時，依序使用有效的已保存時區、瀏覽器偵測的時區，以及預設的 `Asia/Taipei`。
- 清單包含常用城市與瀏覽器列出的 IANA 時區。瀏覽器無法列舉時區時，改用內建清單。UTC 偏移量會依當時的時區規則計算，例如紐約的冬季為 `UTC-5`，夏令時間為 `UTC-4`。

## 番茄鐘

從首頁點選「番茄鐘」。專注事項可留白，開始專注前可以修改或清除。專注進行中或暫停時會保留該段的事項，休息時可先編輯下一段要做的事。

| 階段 | 預設時間 |
| --- | ---: |
| 專注 | 25 分鐘 |
| 短休息 | 5 分鐘 |
| 長休息 | 20 分鐘 |

三種時間都接受正整數分鐘。計時開始後調整設定，新值於對應階段下次開始時生效。

1. 按「開始專注」開始本段倒數。專注期間可以暫停或繼續，也可按「重設此段」清除本段已計時間，同時保留已完成的番茄數。
2. 專注倒數完整結束即記為一個番茄。每個循環的前三個番茄各接一段短休息，第四個接一段長休息。休息會自動開始並倒數至結束。
3. 休息結束後，按「開始專注」開始下一段。長休息結束時，循環進度歸零。
4. 專注階段中，可在已有進度或已開始計時時按「重設循環」。確認後，循環進度歸零並取消當前未完成的專注，專注事項會保留。

每段計時結束時，頁面會顯示提示，並在瀏覽器允許時播放短音效。可勾選「靜音」關閉音效。桌面通知預設關閉，勾選「啟用桌面通知」並取得瀏覽器授權後才會顯示。

### 切換分頁與重新開啟

| 情境 | 計時行為 |
| --- | --- |
| 頁面開著，切到其他分頁 | 依截止時間繼續計時。回到頁面時會更新剩餘時間，並處理已到期的階段。 |
| 正常離開或重新整理，稍後重開 | 保存離頁時的剩餘時間。專注保持暫停，需按「繼續」。休息從保存的時間自動接續。 |

時區選擇、番茄鐘設定、專注事項、目前階段、剩餘時間、循環進度與提示偏好都儲存在瀏覽器的 `localStorage`。瀏覽器依網站來源區分資料，來源由通訊協定、主機名稱和連接埠組成。

## 專案結構

ASP.NET Core 提供 Razor Pages 頁面及靜態資源，時區換算與番茄鐘狀態由瀏覽器端 JavaScript 處理。時鐘以 `Intl.DateTimeFormat` 換算日期、時間和偏移量。番茄鐘以 `Date.now()` 和截止時間計算剩餘時間，避免背景分頁延遲執行時累積計時誤差。

| 內容 | 位置 |
| --- | --- |
| 網站啟動與本機設定 | [Program.cs](Program.cs)、[launchSettings.json](Properties/launchSettings.json) |
| 首頁與功能頁 | [Index.cshtml](Pages/Index.cshtml)、[Clock.cshtml](Pages/Clock.cshtml)、[Pomodoro.cshtml](Pages/Pomodoro.cshtml) |
| 時區與倒數邏輯 | [clock.js](wwwroot/js/clock.js)、[pomodoro.js](wwwroot/js/pomodoro.js) |
| 功能頁樣式 | [clock.css](wwwroot/css/clock.css)、[pomodoro.css](wwwroot/css/pomodoro.css) |
| 瀏覽器端對端測試 | [tests/skilltest.Tests](tests/skilltest.Tests/) |

## 執行測試

測試使用 xUnit 與 Playwright Chromium。在專案根目錄執行以下 PowerShell 指令，首次執行需透過 `pwsh` 安裝測試用 Chromium。

```powershell
dotnet build .\tests\skilltest.Tests\skilltest.Tests.csproj
pwsh -File .\tests\skilltest.Tests\bin\Debug\net11.0\playwright.ps1 install chromium
dotnet test .\tests\skilltest.Tests\skilltest.Tests.csproj
```

測試自行啟動本機網站與瀏覽器，驗證首頁導覽、時區切換與保存、夏令時間偏移、番茄循環、離頁暫停和背景分頁的計時。桌面通知權限與音效播放流程使用模擬介面驗證。

## 規格與參考

- [現在時間功能規格](docs/spec1.md)
- [番茄鐘功能規格](docs/spec2.md)
- [番茄鐘詞彙表](GLOSSARY.md)
- [瀏覽器測試指南](tests/skilltest.Tests/README.md)
