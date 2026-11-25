# 瀏覽器端對端測試

測試透過 Playwright 啟動 Chromium 和本機網站，驗證從首頁進入「現在時間」與「番茄鐘」及各自的操作流程。

- **現在時間**涵蓋時鐘顯示與更新、時區搜尋與切換、時區選擇的保存，以及 IANA 時區的夏令時間偏移。
- **番茄鐘**涵蓋專注暫停與繼續、四番茄循環、離頁後保存專注剩餘時間，以及背景分頁恢復時處理到期的專注與休息。桌面通知權限與音效播放流程使用模擬介面驗證。

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
