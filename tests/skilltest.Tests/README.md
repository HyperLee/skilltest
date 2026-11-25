# 時鐘瀏覽器端對端測試

測試透過 Playwright 啟動 Chromium 和網站，從使用者可操作的頁面驗證首頁導覽、時鐘顯示、時區搜尋與切換、瀏覽器保存，以及 IANA 時區的夏令時間偏移。

## 執行

首次使用時，先還原套件並安裝 Playwright Chromium：

```powershell
dotnet build tests\skilltest.Tests\skilltest.Tests.csproj
pwsh tests\skilltest.Tests\bin\Debug\net11.0\playwright.ps1 install chromium
```

執行測試：

```powershell
dotnet test tests\skilltest.Tests\skilltest.Tests.csproj
```
