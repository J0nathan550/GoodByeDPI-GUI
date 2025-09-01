using AutoUpdaterDotNET;
using GoodByeDPI_GUI.Core.Config;
using GoodByeDPI_GUI.Core.Data;
using GoodByeDPI_GUI.Core.Formatters;
using GoodByeDPI_GUI.Core.Logs;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GoodByeDPI_GUI.Views
{
    public partial class MainWindow : HandyControl.Controls.Window
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/ValdikSS/GoodbyeDPI/releases/latest";
        private const string EXTRACT_FOLDER = "GoodbyeDPI";
        private const string CONFIG_FILE = "config.json";
        private const string LOGS_FILE = "logs.json";
        private const string GITHUB_REPO_URL = "https://github.com/J0nathan550/GoodByeDPI-GUI";

        private static readonly HttpClient httpClient = new HttpClient();

        private bool _isDownloading = false;
        private readonly StringBuilder _statusDisplay = new StringBuilder();
        private AppConfig Config = new AppConfig();
        private List<LogEntry> _logs;

        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(); }
        }

        public string StatusDisplay
        {
            get => _statusDisplay.ToString();
        }

        static MainWindow()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "GoodByeDPI-GUI/1.0");
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadConfiguration();
            LoadLogs();
            foreach (var item in _logs)
            {
                string levelPrefix = GetLevelPrefix(item.Level);
                _statusDisplay.AppendLine($"[{item.Timestamp:HH:mm:ss}] [{levelPrefix}] {item.Message}");
            }
            DataContext = null;
            RefreshStatusDisplay();
            AppendLogEntry("Приложение запущено", LogLevel.Info);
            DataContext = this;
        }

        private void LoadLastPreferedGoodByeDPIOption()
        {
            switch (Config.LastPreferedOptionStart)
            {
                case 1:
                    ButtonConfigurationEnableRussiaRedirects_Clicked(null, null);
                    break;
                case 2:
                    ButtonConfigurationEnableYouTubeRedirects_Clicked(null, null);
                    break;
                case 3:
                    ButtonConfigurationEnableYouTubeAlternative_Clicked(null, null);
                    break;
                case 4:
                    ButtonConfigurationEnableEuropeRedirects_Clicked(null, null);
                    break;
                default:
                    break;
            }
        }

        public async void LoadConfiguration()
        {
            try
            {
                var releaseInfo = await GetLatestReleaseInfoAsync() ?? throw new Exception("Не удалось получить информацию о релизе");
                if (File.Exists(CONFIG_FILE))
                {
                    string json = File.ReadAllText(CONFIG_FILE);
                    Config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                    CheckBoxConfigurationAutoStartWithWindows.IsChecked = Config.AutoStartWithWindows;
                    await CheckExistsGoodByeDPI();
                    LoadLastPreferedGoodByeDPIOption();
                    CheckBoxConfigurationMinimizeToTray.IsChecked = Config.MinimizeToTray;
                    InstalledVersionTextBlock.Text = string.IsNullOrEmpty(Config.InstalledVersion) ? "Не установлено" : "Версия GoodByeDPI: " + Config.InstalledVersion;
                    if (Config.MinimizeToTray)
                    {
                        Hide();
                    }
                }
                else
                {
                    Config = new AppConfig();
                    await CheckExistsGoodByeDPI();
                    SaveConfiguration();
                }
            }
            catch
            {
                Config = new AppConfig();
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Config, Formatting.Indented);
                File.WriteAllText(CONFIG_FILE, json);
            }
            catch
            {
            }
        }

        private void LoadLogs()
        {
            try
            {
                if (File.Exists(LOGS_FILE))
                {
                    string json = File.ReadAllText(LOGS_FILE);
                    var logsContainer = JsonConvert.DeserializeObject<LogsContainer>(json);
                    _logs = logsContainer?.Logs ?? new List<LogEntry>();
                }
                else
                {
                    _logs = new List<LogEntry>();
                }

                if (_logs.Count > 1000)
                {
                    _logs = _logs.GetRange(_logs.Count - 1000, 1000);
                    SaveLogs();
                }
            }
            catch (Exception ex)
            {
                _logs = new List<LogEntry>();
                AppendLogEntry($"Ошибка загрузки логов: {ex.Message}", LogLevel.Warning);
            }
        }

        private void SaveLogs()
        {
            try
            {
                var logsToSave = new LogsContainer
                {
                    Logs = _logs,
                    LastSaved = DateTime.Now,
                    TotalEntries = _logs.Count
                };

                string json = JsonConvert.SerializeObject(logsToSave, Formatting.Indented);
                File.WriteAllText(LOGS_FILE, json);
            }
            catch
            {
            }
        }

        private void AppendLogEntry(string message, LogLevel level = LogLevel.Info)
        {
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Message = message,
                Level = level
            };

            _logs.Add(logEntry);

            if (_logs.Count > 500)
            {
                _logs.RemoveRange(0, _logs.Count - 500);
                RefreshStatusDisplay();
                return;
            }

            string levelPrefix = GetLevelPrefix(level);
            _statusDisplay.AppendLine($"[{logEntry.Timestamp:HH:mm:ss}] [{levelPrefix}] {message}");

            OnPropertyChanged(nameof(StatusDisplay));

            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusTextBox?.ScrollToEnd();
            }));

            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                SaveLogs();
            });
        }

        private void UpdateLastLogEntryText(string message)
        {
            if (_logs.Count > 0)
            {
                var lastLog = _logs[_logs.Count - 1];
                lastLog.Message = message;

                string currentText = _statusDisplay.ToString();
                int lastLineStart = currentText.LastIndexOf(Environment.NewLine);

                if (lastLineStart >= 0)
                {
                    _statusDisplay.Length = lastLineStart + Environment.NewLine.Length;
                    string levelPrefix = GetLevelPrefix(lastLog.Level);
                    _statusDisplay.AppendLine($"[{lastLog.Timestamp:HH:mm:ss}] [{levelPrefix}] {message}");
                }
                else
                {
                    _statusDisplay.Clear();
                    string levelPrefix = GetLevelPrefix(lastLog.Level);
                    _statusDisplay.AppendLine($"[{lastLog.Timestamp:HH:mm:ss}] [{levelPrefix}] {message}");
                }

                OnPropertyChanged(nameof(StatusDisplay));
            }
        }

        private string GetLevelPrefix(LogLevel level)
        {
            return level == LogLevel.Error ? "ОШИБКА" :
                   level == LogLevel.Warning ? "ВНИМАНИЕ" : "ИНФО";
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите очистить все логи?\nЭто действие нельзя отменить.",
                                       "Очистка логов",
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _logs.Clear();
                _statusDisplay.Clear();
                OnPropertyChanged(nameof(StatusDisplay));

                try
                {
                    if (File.Exists(LOGS_FILE))
                    {
                        File.Delete(LOGS_FILE);
                    }
                }
                catch (Exception ex)
                {
                    AppendLogEntry($"Не удалось удалить файл логов: {ex.Message}", LogLevel.Warning);
                }

                AppendLogEntry("Логи очищены пользователем", LogLevel.Info);
            }
        }

        private async void UpdateDownloadGoodByeDPI_Click(object sender, RoutedEventArgs e)
        {
            if (IsDownloading)
            {
                AppendLogEntry("Загрузка уже выполняется...", LogLevel.Warning);
                return;
            }

            try
            {
                IsDownloading = true;
                UpdateDownloadGoodByeDPI.IsEnabled = false;

                AppendLogEntry("Начата проверка обновлений GoodbyeDPI", LogLevel.Info);

                await DownloadLatestReleaseAsync();
            }
            catch (Exception ex)
            {
                AppendLogEntry($"Ошибка загрузки: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"Не удалось загрузить: {ex.Message}", "Ошибка загрузки",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsDownloading = false;
                UpdateDownloadGoodByeDPI.IsEnabled = true;
            }
        }

        private async Task DownloadLatestReleaseAsync()
        {
            AppendLogEntry("Запрос информации о последнем релизе...", LogLevel.Info);
            var releaseInfo = await GetLatestReleaseInfoAsync() ?? throw new Exception("Не удалось получить информацию о релизе");
            string downloadUrl = releaseInfo.DownloadUrl;
            string fileName = releaseInfo.FileName;
            string folderName = Path.GetFileNameWithoutExtension(fileName);
            string version = releaseInfo.Version;

            AppendLogEntry($"Найдена Версия GoodByeDPI: {version} ({ByteFormatting.FormatBytes(releaseInfo.Size)})", LogLevel.Info);

            if (string.IsNullOrEmpty(Config.InstalledPath))
            {
                ButtonConfigurationEnableRussiaRedirects.IsEnabled = false;
                ButtonConfigurationEnableYouTubeRedirects.IsEnabled = false;
                ButtonConfigurationEnableYouTubeAlternative.IsEnabled = false;
                ButtonConfigurationEnableEuropeRedirects.IsEnabled = false;
                UpdateDNSGoodByeDPIButton.IsEnabled = false;
                ButtonStopAllServices.IsEnabled = false;
            }
            else
            {
                if (Directory.Exists(Config.InstalledPath))
                {
                    var files = Directory.EnumerateFiles(Config.InstalledPath, "*.*", SearchOption.AllDirectories);
                    if (Config.FileCount == files.Count() && Config.InstalledVersion == releaseInfo.Version)
                    {
                        AppendLogEntry($"Версия {version} уже установлена", LogLevel.Info);
                        ButtonConfigurationEnableRussiaRedirects.IsEnabled = true;
                        ButtonConfigurationEnableYouTubeRedirects.IsEnabled = true;
                        ButtonConfigurationEnableYouTubeAlternative.IsEnabled = true;
                        ButtonConfigurationEnableEuropeRedirects.IsEnabled = true;
                        UpdateDNSGoodByeDPIButton.IsEnabled = true;
                        ButtonStopAllServices.IsEnabled = true;
                        return;
                    }
                }
                else
                {
                    ButtonConfigurationEnableRussiaRedirects.IsEnabled = false;
                    ButtonConfigurationEnableYouTubeRedirects.IsEnabled = false;
                    ButtonConfigurationEnableYouTubeAlternative.IsEnabled = false;
                    ButtonConfigurationEnableEuropeRedirects.IsEnabled = false;
                    UpdateDNSGoodByeDPIButton.IsEnabled = false;
                    ButtonStopAllServices.IsEnabled = false;
                }
            }

            AppendLogEntry($"Начинаем загрузку {fileName}...", LogLevel.Info);

            string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);
            await DownloadFileWithStreamAsync(downloadUrl, tempFilePath, releaseInfo.Size);

            AppendLogEntry("Извлечение файлов...", LogLevel.Info);

            string extractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, EXTRACT_FOLDER);
            await ExtractZipFileAsync(tempFilePath, extractPath);

            string path = Path.Combine(extractPath, folderName);

            var filesToMove = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories);
            Config.FileCount = filesToMove.Count();
            Config.InstalledPath = extractPath;
            foreach (var file in filesToMove)
            {
                string relativePath = new Uri(path + Path.DirectorySeparatorChar)
                    .MakeRelativeUri(new Uri(file))
                    .ToString()
                    .Replace('/', Path.DirectorySeparatorChar);

                string destFile = Path.Combine(extractPath, relativePath);
                string destDir = Path.GetDirectoryName(destFile);

                try
                {
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    if (File.Exists(destFile))
                    {
                        File.Delete(destFile);
                    }

                    File.Move(file, destFile);
                }
                catch (Exception ex)
                {
                    AppendLogEntry($"Не удалось переместить файл {file} в {destFile}: {ex.Message}", LogLevel.Warning);
                }
            }

            Directory.Delete(path, true);

            Config.InstalledVersion = version;
            InstalledVersionTextBlock.Text = string.IsNullOrEmpty(Config.InstalledVersion) ? "Не установлено" : "Версия GoodByeDPI: " + Config.InstalledVersion;
            ButtonConfigurationEnableRussiaRedirects.IsEnabled = true;
            ButtonConfigurationEnableYouTubeRedirects.IsEnabled = true;
            ButtonConfigurationEnableYouTubeAlternative.IsEnabled = true;
            ButtonConfigurationEnableEuropeRedirects.IsEnabled = true;
            UpdateDNSGoodByeDPIButton.IsEnabled = true;
            ButtonStopAllServices.IsEnabled = true;
            SaveConfiguration();

            try
            {
                File.Delete(tempFilePath);
                AppendLogEntry("Временные файлы очищены", LogLevel.Info);
            }
            catch (Exception ex)
            {
                AppendLogEntry($"Не удалось удалить временный файл: {ex.Message}", LogLevel.Warning);
            }

            AppendLogEntry($"Успешно обновлено до версии {version}", LogLevel.Info);
            AppendLogEntry($"Файлы установлены в: {extractPath}", LogLevel.Info);

            MessageBox.Show($"GoodbyeDPI успешно загружен и установлен!\n\nВерсия GoodByeDPI: {version}\nПуть: {extractPath}",
                          "Обновление завершено", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task<ReleaseInfo> GetLatestReleaseInfoAsync(bool includePreRelease = true)
        {
            try
            {
                string apiUrl = includePreRelease ?
                    GITHUB_API_URL.Replace("/releases/latest", "/releases") :
                    GITHUB_API_URL;

                using (var response = await httpClient.GetAsync(apiUrl))
                {
                    response.EnsureSuccessStatusCode();
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    {
                        string jsonContent = await reader.ReadToEndAsync();

                        if (includePreRelease)
                        {
                            var releases = JArray.Parse(jsonContent);

                            foreach (var release in releases)
                            {
                                string tagName = release["tag_name"]?.ToString() ?? "";
                                bool isPrerelease = release["prerelease"]?.ToObject<bool>() ?? false;

                                if (isPrerelease || tagName.Contains("rc"))
                                {
                                    var assets = release["assets"] as JArray;
                                    foreach (var asset in assets)
                                    {
                                        string name = asset["name"]?.ToString();
                                        if (name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                        {
                                            return new ReleaseInfo
                                            {
                                                DownloadUrl = asset["browser_download_url"]?.ToString(),
                                                FileName = name,
                                                Version = tagName,
                                                Size = asset["size"]?.ToObject<long>() ?? 0,
                                                IsPreRelease = isPrerelease
                                            };
                                        }
                                    }
                                }
                            }

                            AppendLogEntry("RC релиз не найден, используем последний стабильный релиз", LogLevel.Warning);
                        }

                        var jsonData = JObject.Parse(jsonContent);
                        var stableAssets = jsonData["assets"] as JArray;

                        foreach (var asset in stableAssets)
                        {
                            string name = asset["name"]?.ToString();
                            if (name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                return new ReleaseInfo
                                {
                                    DownloadUrl = asset["browser_download_url"]?.ToString(),
                                    FileName = name,
                                    Version = jsonData["tag_name"]?.ToString() ?? "Unknown",
                                    Size = asset["size"]?.ToObject<long>() ?? 0,
                                    IsPreRelease = false
                                };
                            }
                        }
                    }
                }

                throw new Exception("В релизах не найден zip файл");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Не удалось подключиться к GitHub API: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось обработать информацию о релизе: {ex.Message}");
            }
        }

        private async Task DownloadFileWithStreamAsync(string url, string filePath, long totalSize)
        {
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? totalSize;
                var downloadedBytes = 0L;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 32768, true))
                {
                    var buffer = new byte[32768];
                    int bytesRead;
                    var lastUpdateTime = DateTime.Now;
                    var startTime = DateTime.Now;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        downloadedBytes += bytesRead;

                        if ((DateTime.Now - lastUpdateTime).TotalMilliseconds > 500 && totalBytes > 0)
                        {
                            double progressPercent = (double)downloadedBytes / totalBytes * 100;
                            double speed = downloadedBytes / Math.Max((DateTime.Now - startTime).TotalSeconds, 1);
                            string sizeInfo = $"{ByteFormatting.FormatBytes(downloadedBytes)} / {ByteFormatting.FormatBytes(totalBytes)}";
                            string speedInfo = $"{ByteFormatting.FormatBytes((long)speed)}/с";

                            UpdateLastLogEntryText($"Загружено: {progressPercent:F1}% ({sizeInfo}) - {speedInfo}");
                            lastUpdateTime = DateTime.Now;
                        }
                    }

                    await fileStream.FlushAsync();
                }

                AppendLogEntry($"Загрузка завершена: {ByteFormatting.FormatBytes(downloadedBytes)}", LogLevel.Info);
            }
        }

        private async Task ExtractZipFileAsync(string zipFilePath, string extractPath)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(extractPath))
                    {
                        Directory.Delete(extractPath, true);
                    }

                    Directory.CreateDirectory(extractPath);

                    using (var archive = ZipFile.OpenRead(zipFilePath))
                    {
                        int totalEntries = archive.Entries.Count;
                        int extractedEntries = 0;

                        foreach (var entry in archive.Entries)
                        {
                            string destinationPath = Path.Combine(extractPath, entry.FullName);

                            string directoryName = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                            {
                                Directory.CreateDirectory(directoryName);
                            }

                            if (!string.IsNullOrEmpty(entry.Name))
                            {
                                entry.ExtractToFile(destinationPath, true);
                            }

                            extractedEntries++;

                            if (extractedEntries % 10 == 0 || extractedEntries == totalEntries)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    UpdateLastLogEntryText($"Извлечено файлов: {extractedEntries}/{totalEntries}");
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка при извлечении файлов: {ex.Message}");
                }
            });
        }

        private void RefreshStatusDisplay()
        {
            _statusDisplay.Clear();

            var recentLogs = _logs?.Count > 100 ? _logs.GetRange(_logs.Count - 100, 100) : _logs ?? new List<LogEntry>();

            foreach (var log in recentLogs)
            {
                string levelPrefix = GetLevelPrefix(log.Level);
                _statusDisplay.AppendLine($"[{log.Timestamp:HH:mm:ss}] [{levelPrefix}] {log.Message}");
            }

            OnPropertyChanged(nameof(StatusDisplay));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            DataContext = null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            DataContext = this;
        }

        private void CheckBoxConfigurationAutoStartWithWindows_Clicked(object sender, RoutedEventArgs e)
        {
            bool autoStart = CheckBoxConfigurationAutoStartWithWindows.IsChecked == true;
            Config.AutoStartWithWindows = autoStart;

            try
            {
                SetAutoStart(autoStart);
                SaveConfiguration();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update auto-start setting: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Warning);

                CheckBoxConfigurationAutoStartWithWindows.IsChecked = !autoStart;
                Config.AutoStartWithWindows = !autoStart;
            }
        }

        private void SetAutoStart(bool enable)
        {
            string appName = "GoodByeDPI-GUI";
            string batchPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launch-goodbyedpi.bat");

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null)
                        throw new InvalidOperationException("Unable to access Windows startup registry key.");

                    if (enable)
                    {
                        if (!File.Exists(batchPath))
                        {
                            CreateLauncherBatch(batchPath);
                        }

                        key.SetValue(appName, batchPath);
                    }
                    else
                    {
                        if (key.GetValue(appName) != null)
                        {
                            key.DeleteValue(appName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(ex.Message + "\n" + ex.StackTrace);
            }
        }

        private void CreateLauncherBatch(string batchPath)
        {
            string batchContent = @"@echo off
            cd /d ""%~dp0""
            start """" ""GoodByeDPI-GUI.exe""";
            File.WriteAllText(batchPath, batchContent);
        }

        private async void ButtonConfigurationEnableRussiaRedirects_Clicked(object sender, RoutedEventArgs e)
        {
            ButtonConfigurationEnableRussiaRedirects.IsEnabled = false;
            bool result = await CheckExistsGoodByeDPI();
            if (!result)
            {
                return;
            }

            AppendLogEntry("Завершаем все процессы goodbyedpi...", LogLevel.Info);

            ShutdownGoodByeDPI();

            Config.LastPreferedOptionStart = 1;
            AppendLogEntry("Включаем передадресации для россии...", LogLevel.Info);
            SaveConfiguration();

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Config.InstalledPath, "1_russia_blacklist_dnsredir.cmd"),
                        WorkingDirectory = Config.InstalledPath,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    };

                    process.OutputDataReceived += (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLogEntry(args.Data, LogLevel.Info);
                            });
                        }
                    };

                    process.ErrorDataReceived += (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLogEntry(args.Data, LogLevel.Error);
                            });
                        }
                    };

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await Task.Run(() => process.WaitForExit());

                    AppendLogEntry($"Процесс завершен с кодом: {process.ExitCode}",
                                  process.ExitCode == 0 ? LogLevel.Info : LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                AppendLogEntry($"Ошибка при выполнении процесса: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                AppendLogEntry("Завершили передадресации для россии.", LogLevel.Info);
                ButtonConfigurationEnableRussiaRedirects.IsEnabled = true;
            }
        }

        private async void ButtonConfigurationEnableYouTubeRedirects_Clicked(object sender, RoutedEventArgs e)
        {
            ButtonConfigurationEnableYouTubeRedirects.IsEnabled = false;
            bool result = await CheckExistsGoodByeDPI();
            if (!result)
            {
                return;
            }

            AppendLogEntry("Завершаем все процессы goodbyedpi...", LogLevel.Info);

            ShutdownGoodByeDPI();

            Config.LastPreferedOptionStart = 2;
            AppendLogEntry("Включаем YouTube передадресации для россии...", LogLevel.Info);
            SaveConfiguration();

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Config.InstalledPath, "1_russia_blacklist_YOUTUBE_ALT.cmd"),
                        WorkingDirectory = Config.InstalledPath,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    };

                    process.OutputDataReceived += (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLogEntry(args.Data, LogLevel.Info);
                            });
                        }
                    };

                    process.ErrorDataReceived += (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLogEntry(args.Data, LogLevel.Error);
                            });
                        }
                    };

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await Task.Run(() => process.WaitForExit());

                    AppendLogEntry($"Процесс завершен с кодом: {process.ExitCode}",
                                  process.ExitCode == 0 ? LogLevel.Info : LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                AppendLogEntry($"Ошибка при выполнении процесса: {ex.Message}", LogLevel.Error);
            }
            finally
            {

                AppendLogEntry("Завершили YouTube передадресации для россии.", LogLevel.Info);
                ButtonConfigurationEnableYouTubeRedirects.IsEnabled = true;
            }
        }

        private void ShutdownGoodByeDPI()
        {
            Process[] processes = Process.GetProcessesByName("goodbyedpi");
            foreach (var proc in processes)
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit();
                }
                catch (Exception ex)
                {
                    AppendLogEntry($"Не удалось завершить процесс GoodByeDPI (PID: {proc.Id}): {ex.Message}", LogLevel.Warning);
                }
            }
        }

        private async void ButtonConfigurationEnableYouTubeAlternative_Clicked(object sender, RoutedEventArgs e)
        {
            ButtonConfigurationEnableYouTubeAlternative.IsEnabled = false;
            bool result = await CheckExistsGoodByeDPI();
            if (!result)
            {
                return;
            }

            AppendLogEntry("Завершаем все процессы goodbyedpi...", LogLevel.Info);

            ShutdownGoodByeDPI();

            Config.LastPreferedOptionStart = 3;
            AppendLogEntry("Включаем альтернативные YouTube передадресации для россии...", LogLevel.Info);
            SaveConfiguration();

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Config.InstalledPath, "1_russia_blacklist_YOUTUBE_ALT.cmd"),
                        WorkingDirectory = Config.InstalledPath,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    };

                    process.OutputDataReceived += (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLogEntry(args.Data, LogLevel.Info);
                            });
                        }
                    };

                    process.ErrorDataReceived += (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLogEntry(args.Data, LogLevel.Error);
                            });
                        }
                    };

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await Task.Run(() => process.WaitForExit());

                    AppendLogEntry($"Процесс завершен с кодом: {process.ExitCode}",
                                  process.ExitCode == 0 ? LogLevel.Info : LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                AppendLogEntry($"Ошибка при выполнении процесса: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                AppendLogEntry("Завершили альтернативные YouTube передадресации для россии.", LogLevel.Info);
                ButtonConfigurationEnableYouTubeAlternative.IsEnabled = true;
            }
        }

        private async void ButtonConfigurationEnableEuropeRedirects_Clicked(object sender, RoutedEventArgs e)
        {
            ButtonConfigurationEnableEuropeRedirects.IsEnabled = false;
            bool result = await CheckExistsGoodByeDPI();
            if (!result)
            {
                return;
            }

            AppendLogEntry("Завершаем все процессы goodbyedpi...", LogLevel.Info);

            ShutdownGoodByeDPI();

            Config.LastPreferedOptionStart = 4;
            AppendLogEntry("Включаем передадресации для стран Европы...", LogLevel.Info);
            SaveConfiguration();

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Config.InstalledPath, "2_any_country_dnsredir.cmd"),
                        WorkingDirectory = Config.InstalledPath,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    };

                    process.OutputDataReceived += (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLogEntry(args.Data, LogLevel.Info);
                            });
                        }
                    };

                    process.ErrorDataReceived += (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLogEntry(args.Data, LogLevel.Error);
                            });
                        }
                    };

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await Task.Run(() => process.WaitForExit());

                    AppendLogEntry($"Процесс завершен с кодом: {process.ExitCode}",
                                  process.ExitCode == 0 ? LogLevel.Info : LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                AppendLogEntry($"Ошибка при выполнении процесса: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                AppendLogEntry("Завершили передадресации для стран Европы.", LogLevel.Info);
                ButtonConfigurationEnableEuropeRedirects.IsEnabled = true;
            }
        }

        private void ButtonStopAllServices_Clicked(object sender, RoutedEventArgs e)
        {
            ButtonStopAllServices.IsEnabled = false;
            AppendLogEntry("Завершаем все процессы goodbyedpi...", LogLevel.Info);

            ShutdownGoodByeDPI();

            AppendLogEntry("Завершили все процессы goodbyedpi...", LogLevel.Info);
            Config.LastPreferedOptionStart = 0;
            SaveConfiguration();
            ButtonStopAllServices.IsEnabled = true;
        }

        private async Task<bool> CheckExistsGoodByeDPI()
        {
            var releaseInfo = await GetLatestReleaseInfoAsync() ?? throw new Exception("Не удалось получить информацию о релизе");
            if (string.IsNullOrEmpty(Config.InstalledPath))
            {
                ButtonConfigurationEnableRussiaRedirects.IsEnabled = false;
                ButtonConfigurationEnableYouTubeRedirects.IsEnabled = false;
                ButtonConfigurationEnableYouTubeAlternative.IsEnabled = false;
                ButtonConfigurationEnableEuropeRedirects.IsEnabled = false;
                UpdateDNSGoodByeDPIButton.IsEnabled = false;
                ButtonStopAllServices.IsEnabled = false;
                AppendLogEntry("Путь установки не задан. Пожалуйста, обновите или переустановите GoodbyeDPI.", LogLevel.Warning);
                return false;
            }
            else
            {
                if (Directory.Exists(Config.InstalledPath))
                {
                    var files = Directory.EnumerateFiles(Config.InstalledPath, "*.*", SearchOption.AllDirectories);
                    if (Config.FileCount != files.Count() || Config.InstalledVersion != releaseInfo.Version)
                    {
                        ButtonConfigurationEnableRussiaRedirects.IsEnabled = false;
                        ButtonConfigurationEnableYouTubeRedirects.IsEnabled = false;
                        ButtonConfigurationEnableYouTubeAlternative.IsEnabled = false;
                        ButtonConfigurationEnableEuropeRedirects.IsEnabled = false;
                        UpdateDNSGoodByeDPIButton.IsEnabled = false;
                        ButtonStopAllServices.IsEnabled = false;
                        AppendLogEntry("Путь установки не задан. Пожалуйста, обновите или переустановите GoodbyeDPI.", LogLevel.Warning);
                        return false;
                    }
                }
                else
                {
                    ButtonConfigurationEnableRussiaRedirects.IsEnabled = false;
                    ButtonConfigurationEnableYouTubeRedirects.IsEnabled = false;
                    ButtonConfigurationEnableYouTubeAlternative.IsEnabled = false;
                    ButtonConfigurationEnableEuropeRedirects.IsEnabled = false;
                    UpdateDNSGoodByeDPIButton.IsEnabled = false;
                    ButtonStopAllServices.IsEnabled = false;
                    AppendLogEntry("Путь установки не задан. Пожалуйста, обновите или переустановите GoodbyeDPI.", LogLevel.Warning);
                    return false;
                }
            }
            return true;
        }

        private async void UpdateDNSGoodByeDPIButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateDNSGoodByeDPIButton.IsEnabled = false;
            bool result = await CheckExistsGoodByeDPI();
            if (!result)
            {
                return;
            }

            AppendLogEntry("Обновляем DNS список для GoodByeDPI...", LogLevel.Info);

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Config.InstalledPath, "0_russia_update_blacklist_file.cmd"),
                        WorkingDirectory = Config.InstalledPath,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    process.OutputDataReceived += (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLogEntry(args.Data, LogLevel.Info);
                            });
                        }
                    };

                    process.ErrorDataReceived += (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                AppendLogEntry(args.Data, LogLevel.Error);
                            });
                        }
                    };

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await Task.Run(() => process.WaitForExit());

                    AppendLogEntry($"Процесс завершен с кодом: {process.ExitCode}",
                                  process.ExitCode == 0 ? LogLevel.Info : LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                AppendLogEntry($"Ошибка при выполнении процесса: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                AppendLogEntry("Обновление DNS списка завершено.", LogLevel.Info);
                UpdateDNSGoodByeDPIButton.IsEnabled = true;
            }
        }

        private void CheckBoxConfigurationMinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            bool minimizeToTray = CheckBoxConfigurationMinimizeToTray.IsChecked == true;
            Config.MinimizeToTray = minimizeToTray;
            SaveConfiguration();
        }

        private void GithubRepository_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = GITHUB_REPO_URL,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("Не удалось открыть ссылку. Пожалуйста, откройте её вручную:" + GITHUB_REPO_URL, "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenApplicationTrayMenu_Clicked(object sender, RoutedEventArgs e)
        {
            Show();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            if (!Config.MinimizeToTray)
            {
                Application.Current.Shutdown();
            }
            else
            {
                Hide();
            }
        }

        private void CloseApplicationTrayMenu_Clicked(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void CheckAutoUpdates_Click(object sender, RoutedEventArgs e)
        {
            AppendLogEntry("Проверяем обновления...", LogLevel.Info);
            AutoUpdater.Start("https://github.com/J0nathan550/GoodByeDPI-GUI/AutoUpdater.xml");
            AppendLogEntry("Проверка обновлений завершена.", LogLevel.Info);
        }
    }
}