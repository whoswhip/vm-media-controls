using System.Diagnostics;

namespace voicemeeter_media
{
    internal class UpdateManager
    {
        const string REPO_URL = "https://api.github.com/repos/whoswhip/vm-media-controls/releases/latest";
        public static Version Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        public static string VersionString = Version.ToString().Substring(0, Version.ToString().LastIndexOf('.'));

        public static void CheckForUpdates()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("vmMediaControls");
                    var response = client.GetStringAsync(REPO_URL).Result;
                    var json = System.Text.Json.JsonDocument.Parse(response);
                    var latestVersionString = json.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
                    if (!string.IsNullOrEmpty(latestVersionString) && Version.TryParse(latestVersionString ?? "0", out Version? latestVersion))
                    {
                        var testVersion = new Version(1, 0, 10);
                        if (testVersion > Version)
                        {
                            var result = MessageBox.Show($"A new version ({latestVersion}) is available. You are running version {VersionString}." +
                                $"\n\nWould you like to download the latest version?",
                                "Update Available",
                                MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                            if (result == DialogResult.OK)
                            {
                                bool installed = InstalledToProgramFiles();
                                if (installed)
                                {
                                    string downloadUrl = $"https://github.com/whoswhip/vm-media-controls/releases/download/v{latestVersionString}/VMMC-Setup.exe";
                                    var tempFile = Path.Combine(Path.GetTempPath(), "VMMC-Setup.exe");
                                    bool success = DownloadFile(downloadUrl, tempFile);
                                    if (success)
                                    {
                                        Process.Start(new ProcessStartInfo
                                        {
                                            FileName = tempFile,
                                            UseShellExecute = true
                                        });
                                        Application.Exit();
                                    }
                                    else
                                    {
                                        MessageBox.Show("Failed to download the update.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                }
                                else
                                {
                                    string downloadUrl = $"https://github.com/whoswhip/vm-media-controls/releases/download/v{latestVersionString}/VMMC-v{latestVersionString}-Portable.zip";
                                    var tempFile = Path.Combine(Path.GetTempPath(), $"VMMC-v{latestVersionString}-Portable.zip");
                                    bool success = DownloadFile(downloadUrl, tempFile);
                                    if (success)
                                    {
                                        string folder = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? "";
                                        if (!string.IsNullOrWhiteSpace(folder))
                                        {
                                            string tempExtractPath = Path.Combine(Path.GetTempPath(), $"VMMC-Extract-{Guid.NewGuid()}");
                                            System.IO.Compression.ZipFile.ExtractToDirectory(tempFile, tempExtractPath);
                                            string batchFile = Path.Combine(Path.GetTempPath(), $"UpdateVMMC-{Guid.NewGuid()}.bat");
                                            using (var writer = new StreamWriter(batchFile))
                                            {
                                                writer.WriteLine("@echo off");
                                                writer.WriteLine("timeout /t 2 /nobreak > nul");
                                                writer.WriteLine($"xcopy /y /e \"{tempExtractPath}\\*\" \"{folder}\\\"");
                                                writer.WriteLine($"rmdir /s /q \"{tempExtractPath}\"");
                                                writer.WriteLine($"del \"%~f0\"");
                                                writer.WriteLine($"del \"{tempFile}\"");
                                                writer.WriteLine($"start \"\" \"{Path.Combine(folder, "voicemeeter-media-controls.exe")}\"");
                                            }
                                            Process.Start(new ProcessStartInfo
                                            {
                                                FileName = batchFile,
                                                UseShellExecute = true,
                                                CreateNoWindow = true,
                                                WindowStyle = ProcessWindowStyle.Hidden
                                            });
                                            Application.Exit();
                                        }
                                    }
                                    else
                                    {
                                        MessageBox.Show("Failed to download the update.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
            }
        }

        static bool InstalledToProgramFiles()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            return appPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) ||
                   appPath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase);
        }
        static bool DownloadFile(string url, string destination)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = client.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        response.Content.CopyToAsync(fs).Wait();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading file: {ex.Message}");
                return false;
            }
        }
    }
}
