# High-Accuracy NES Emulator (AccuracyCoin Ready)

一個使用 C# 開發、專為高相容性與精準時鐘週期設計的任天堂 NES（紅白機）模擬器。包含完整的 CPU、PPU、APU 音效、記憶體映射器（Mappers）以及原生的 Windows 視窗介面。

本模擬器經過針對 **[100thCoin/AccuracyCoin](https://github.com/100thCoin/AccuracyCoin)** 測試套件的精確度最佳化設計。

---

## 🎮 主要功能特色

* **6502 (Ricoh 2A03) CPU 核心**：
  * 完整支援全部 56 個官方指令及定址模式。
  * 支援常用非官方指令（Unofficial / Undocumented Opcodes）：`SLO`, `RLA`, `SRE`, `RRA`, `SAX`, `LAX`, `DCP`, `ISC`, `ANC`, `ALR`, `ARR`, `AXS`, `LAS` 及多位元組 `NOP`。
  * 準確的時鐘週期計算（Cycle-accurate timing），包含分支成功（+1 cycle）、跨 Page Boundary 分支與讀取懲罰（+1 cycle）。
  * 6502 原生硬體 Bug 模擬：`JMP ($xxFF)` 間接跳躍定址跨頁 Wrap Bug。
  * 中斷狀態（NMI, IRQ, RESET）與 PHP/BRK/NMI/IRQ 的 B 旗標與 Bit 5（U 旗標）精確壓棧邏輯。
* **PPU (Ricoh 2C02) 圖像核心**：
  * 256x240 解析度，支援 Loopy 內部滾動暫存器（`v`, `t`, `x`, `w`）與即時 Nametable / Attribute 定址。
  * 8x8 與 8x16 精靈渲染、垂直/水平翻轉、背景優先權判定。
  * 精確 Sprite 0 Hit 碰撞偵測（非透明像素即時碰撞，支援超級瑪利歐 SMB、AccuracyCoin 等分屏測試）。
  * 內建 64 色標準 NES 調色盤。
* **Open Bus（浮空匯流排）模擬**：
  * 精確模擬 NES 未映射記憶體、控制台手把高 3 位元（Bits 5-7）、$4015 狀態暫存器浮動資料匯流排。
* **APU (Ricoh 2A03) 音效處理**：
  * 支援 Pulse 1、Pulse 2、Triangle、Noise 聲道混音。
  * 採用 Windows 原生 Win32 `waveOut` 低延遲即時音訊串流，**完全無須安裝任何第三方音訊庫**。
* **Mapper 晶片支援**：
  * **Mapper 0 (NROM)**：超級瑪利歐、大金剛、小精靈、AccuracyCoin
  * **Mapper 1 (MMC1)**：薩爾達傳說、銀河戰士、洛克人 2
  * **Mapper 2 (UxROM)**：魂斗羅、惡魔城、洛克人
  * **Mapper 3 (CNROM)**：所羅門之鑰、高橋名人之冒險島
  * **Mapper 4 (MMC3)**：超級瑪利歐 3、洛克人 3-6、星之卡比
  * **Mapper 7 (AxROM)**：忍者蛙、雷霆任務
* **現代 Windows 視窗介面**：
  * 拖曳（Drag & Drop）載入 `.nes` 檔案即開即玩。
  * 畫面倍率切換（1x, 2x, 3x, 4x），支援像素點對點與雙線性平滑濾鏡（Bilinear Filtering）。
  * 即時存檔（F5 快速存檔、F7 快速讀檔）。
  * 暫停、Reset、Turbo 加速模式。

---

## ⌨️ 操作控制方式

| 按鍵 | 功能 |
| :--- | :--- |
| **方向鍵 (↑ ↓ ← →)** | 方向鍵 (D-Pad) |
| **Z** | A 鈕 |
| **X** | B 鈕 |
| **Right Shift / C** | Select 鈕 |
| **Enter** | Start 鈕 |
| **P** | 暫停 / 繼續 (Pause) |
| **Tab** | Turbo 快速前進 (加速模式) |
| **F5** | 快速存檔 (Quick Save State) |
| **F7** | 快速讀檔 (Quick Load State) |
| **Ctrl + O** | 開啟 ROM 檔案 |
| **Ctrl + R** | 重置模擬器 (Reset) |
| **Ctrl + M** | 靜音 / 開啟音效 |

---

## 🛠️ 如何將程式碼編譯成 .exe

本專案經過特殊設計，**完全不需要安裝龐大的 Visual Studio 或額外的 SDK**，Windows 系統中自帶的 C# 編譯器即可在 1 秒內編譯完成！

### 方法一：一鍵批次編譯（推薦）
直接在專案根目錄**雙擊執行 `build.bat`**。
編譯完成後，目錄中會立即生成 `NesEmulator.exe`，雙擊即可運行！

### 方法二：使用 PowerShell 編譯
開啟 PowerShell，切換至本專案目錄並執行：
```powershell
.\build.ps1
```

### 方法三：手動使用 Windows 內建編譯器指令
在命令提示字元（CMD）或 PowerShell 輸入：
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:NesEmulator.exe /recurse:src\*.cs
```

---

## 🚀 如何將檔案加入 GitHub 倉庫

按照以下詳細步驟，即可將你的專案發佈到 GitHub：

### 步驟 1：安裝 Git（若電腦尚未安裝）
打開 PowerShell 執行：
```powershell
winget install Git.Git
```
*或者至 [git-scm.com](https://git-scm.com/) 下載安裝程式。*

### 步驟 2：在 GitHub 上建立新的 Repository
1. 登入 [GitHub](https://github.com/)。
2. 點擊右上角的 **+** 號，選擇 **New repository**。
3. 輸入專案名稱（例如：`nes-emulator`）。
4. 選擇 **Public**（公開）或 **Private**（私人）。
5. **不要勾選** "Initialize this repository with a README"（因為我們本地已經有完整專案了）。
6. 點擊 **Create repository**。

### 步驟 3：在本地終端機推送程式碼
在專案根目錄開啟 PowerShell 或 CMD，依序輸入以下指令：

```bash
# 1. 初始化 Git 本地倉庫
git init

# 2. 加入所有源始程式碼與配置
git add .

# 3. 建立第一次提交
git commit -m "feat: Initial commit with high accuracy NES emulator"

# 4. 將本地主分支命名為 main
git branch -M main

# 5. 連接你在 GitHub 建立的倉庫（請換成你的 GitHub 網址）
git remote add origin https://github.com/你的使用者名稱/nes-emulator.git

# 6. 推送程式碼到 GitHub
git push -u origin main
```

---

## 📦 如何在 GitHub 發佈編譯好的 .exe 供他人下載

在 Git 中，一般源碼庫**不建議直接 commit 大型二進位檔案**，GitHub 官方推薦使用 **Releases** 功能來發佈編譯好的 `.exe`：

1. 前往你的 GitHub 倉庫頁面。
2. 在右側欄找到 **Releases**，點擊 **Create a new release**（或 **Draft a new release**）。
3. 填寫版本號標籤，例如：`v1.0.0`。
4. 填寫發布標題，例如：`NES Emulator v1.0.0 (AccuracyCoin Ready)`。
5. 在下方 **Attach binaries by dropping them here or selecting them** 區域，將編譯出的 `NesEmulator.exe` 拖曳進去。
6. 點擊 **Publish release**。
7. 任何人現在都可以直接從你的 GitHub Release 頁面一鍵下載 `NesEmulator.exe`，下載後直接點開就能玩！

---

## 🎯 測試 AccuracyCoin

本專案根目錄已附帶 `AccuracyCoin.nes` 測試 ROM。

1. 啟動 `NesEmulator.exe`。
2. 將 `AccuracyCoin.nes` 拖曳進模擬器視窗中。
3. 模擬器將會載入測試選單：
   * **方向鍵**：移動光標
   * **Z 鍵 (A)**：執行目前選取的測試
   * **Enter 鍵 (Start)**：自動執行全套 147+ 項硬體精確度測試並輸出結果統計表！
