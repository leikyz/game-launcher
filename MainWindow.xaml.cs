using DiscordRPC;
using DiscordRPC.Logging;
using Launcher;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;
using Path = System.IO.Path;

namespace Launcher
{
    public partial class MainWindow : Window
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private double downloadedSizeInMB;
        private double totalSize = 0;
        private string fileVerified = "";
        private bool inUpdate = false;
        private bool repaired = false;
        private bool canPlay = false;
        private bool inInstall = false;
        private int nbTotalFilesChanged = 0;
        private string fileName;

        private readonly BackgroundWorker installWorker = new BackgroundWorker();
        private readonly BackgroundWorker updateWorker = new BackgroundWorker();
        private readonly RichPresence presence = new RichPresence();
        private readonly System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon();
        private DiscordRpcClient client;
        private readonly DateTime startTime;

        public bool InUpdate { get => inUpdate; set => inUpdate = value; }
        public bool Repaired { get => repaired; set => repaired = value; }
        public bool InInstall { get => inInstall; set => inInstall = value; }

        public MainWindow()
        {
            startTime = DateTime.Now;

            // Prevent multiple instances of the launcher
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                Application.Current.Shutdown();
            }

            // Initialize Discord RPC
            client = new DiscordRpcClient("1331317869841088587");
            client.OnReady += (sender, e) =>
            {
                Console.WriteLine($"RPC Connected! {e.User}");
            };
            client.Initialize();

            presence = new RichPresence()
            {
                Details = $"On {Config.GameName} launcher",
                Assets = new Assets()
                {
                    LargeImageKey = "logo", // Placeholder for DiscordRPC image
                    LargeImageText = Config.GameName
                },
                Buttons = new DiscordRPC.Button[]
                {
                    new DiscordRPC.Button() { Label = "Join Discord", Url = Config.DiscordURL },
                    new DiscordRPC.Button() { Label = "Visit Website", Url = Config.SiteURL }
                }
            };

            CompositionTarget.Rendering += UpdatePresence;

            InitializeComponent();
            MouseDown += Window_MouseDown;
            Application.Current.Exit += Application_ApplicationExit;

            dlPercent.Visibility = Visibility.Hidden;
            notifyIcon.Icon = new System.Drawing.Icon("icon.ico"); // Ensure this icon exists
            notifyIcon.Visible = true;
            notifyIcon.Text = $"{Config.GameName} Launcher";

            CreateSystemTray();

            // Asynchronously initialize launcher state (check for updates, etc.)
            Loaded += async (s, e) => await InitializeAsync();
        }

        // Updates Discord RPC with elapsed time
        private void UpdatePresence(object sender, EventArgs e)
        {
            TimeSpan elapsed = DateTime.Now - startTime;
            string timeElapsed = "online for ";

            if (elapsed.Days > 0)
                timeElapsed += $"{elapsed.Days} day{(elapsed.Days > 1 ? "s" : "")}";
            else if (elapsed.Hours > 0)
                timeElapsed += $"{elapsed.Hours} hour{(elapsed.Hours > 1 ? "s" : "")}";
            else if (elapsed.Minutes > 0)
                timeElapsed += $"{elapsed.Minutes} minute{(elapsed.Minutes > 1 ? "s" : "")}";
            else
                timeElapsed += $"{elapsed.Seconds} second{(elapsed.Seconds > 1 ? "s" : "")}";

            presence.State = timeElapsed;
            client.SetPresence(presence);
        }

        // Updates Discord RPC when the game is launched
        private void UpdatePresenceGame(object? sender, EventArgs e)
        {
            presence.Details = "In-game on {Config.GameName}" ;


            client.SetPresence(presence);
        }

        void Application_ApplicationExit(object sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Icon = null;
            _notifyIcon.Dispose();
            client.Dispose();
            Application.Current.Shutdown();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Allows dragging the frameless window
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (GameExeExists())
                {
                    if (await NeedUpdateAsync())
                    {
                        canPlay = false;
                        inUpdate = true;
                        infoUpdate.Content = "An update is available!";
                        notifyIcon.BalloonTipTitle = $"{Config.GameName} Launcher";
                        notifyIcon.BalloonTipText = "An update is available! Check Discord or the website for details.";
                        notifyIcon.ShowBalloonTip(3000);
                        downloadButton.Content = "Update now";
                    }
                    else
                    {
                        canPlay = true;
                        infoUpdate.Content = "Your game is up to date.";
                        downloadButton.Content = "Play";
                    }
                }
                else
                {
                    inInstall = true;
                    canPlay = false;
                    infoUpdate.Content = $"Your game is up to date.";
                    downloadButton.Content = $"Play";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization error: {ex.Message}");
            }
        }

        public async void Play()
        {
            CompositionTarget.Rendering += UpdatePresenceGame;

            infoUpdate.Content = $"Launching {Config.GameName}";
            downloadButton.Content = "Launching...";
            downloadButton.IsEnabled = false;

            string gamePath = Path.Combine(Directory.GetCurrentDirectory(), $"{Config.GameName}.exe");
            presence.State = "In-game"; // Update Discord status
            presence.Details = $"Playing {Config.GameName}";
            downloadButton.IsEnabled = true;
            infoUpdate.Content = "Your game is up to date.";
            downloadButton.Content = "Play";
        }

        public void CloseGame()
        {
            string processName = Path.GetFileNameWithoutExtension(Config.GameName);
            Process gameProcess = Process.GetProcessesByName(processName).FirstOrDefault();
            gameProcess?.Kill();
        }

        private void updateProgressBar(object sender, ProgressChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                dlPercent.Visibility = Visibility.Visible;
                dlPercent.Content = $"{e.ProgressPercentage}%";
                progressBar.Value = e.ProgressPercentage;
            });
        }

        private void updateProgressBarUpdate(object sender, ProgressChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                dlPercent.Visibility = Visibility.Visible;
                dlPercent.Content = $"{e.ProgressPercentage}%";
                progressBar.Value = e.ProgressPercentage;
            });
        }

        // Called when the install/update BackgroundWorker completes
        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                dlPercent.Content = "100%";
                progressBar.Value = 100;
                infoUpdate.Content = "Installation / update complete!";
                downloadButton.Content = "Play";
                canPlay = true;
                inInstall = false;
                inUpdate = false;
                repaired = false;
                downloadButton.IsEnabled = true;
            });
        }

        private async void Install()
        {
            try
            {
                if (!Directory.Exists(Config.GameName))
                    Directory.CreateDirectory(Config.GameName);

                await DownloadFileAsync(Config.filesURL + "Launcher.version", "Launcher.version");
                CloseGame();

                var progress = new Progress<int>(percent =>
                {
                    updateProgressBar(null, new ProgressChangedEventArgs(percent, null));
                });

                await DownloadFilesAsync(progress);
                Worker_RunWorkerCompleted(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during installation: {ex.Message}");
            }
        }

        public void Update()
        {
            CloseGame();
            updateWorker.DoWork += new DoWorkEventHandler(updateFiles);
            updateWorker.WorkerReportsProgress = true;
            updateWorker.ProgressChanged += new ProgressChangedEventHandler(updateProgressBarUpdate);
            updateWorker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            updateWorker.RunWorkerAsync();
        }

        // Checks the remote Launcher.version file's modification date against the local one
        private async Task<bool> NeedUpdateAsync()
        {
            bool result = false;
            try
            {
                if (!File.Exists("Launcher.version"))
                {
                    await DownloadFileAsync(Config.filesURL + "Launcher.version", "Launcher.version");
                    result = false;
                }
                else
                {
                    using (WebResponse response = WebRequest.Create(Config.filesURL + "Launcher.version").GetResponse())
                    {
                        string lastModified = response.Headers.Get("Last-Modified");
                        if (DateTime.TryParse(lastModified, out DateTime dtRemoteFile))
                        {
                            FileInfo localFile = new FileInfo("Launcher.version");
                            if (dtRemoteFile > localFile.LastWriteTime)
                                result = true;

                            localFile.LastWriteTime = dtRemoteFile;
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
            }
            return result;
        }

        private static readonly HttpClient httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Asynchronously downloads a single file
        private async Task DownloadFileAsync(string fileUrl, string localPath)
        {
            using var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            string directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
            await stream.CopyToAsync(fileStream);
        }

        // Downloads all files listed in the remote files.xml
        private async Task DownloadFilesAsync(IProgress<int>? progress = null)
        {
            XmlDocument clientfiles = new XmlDocument();
            clientfiles.Load(Config.filesURL + "files.xml");

            XmlNodeList nodes = clientfiles.SelectNodes("/configuration/path");
            int total = nodes.Count;
            int count = 0;

            if (!Directory.Exists(Config.GameName))
                Directory.CreateDirectory(Config.GameName);

            foreach (XmlNode node in nodes)
            {
                string relativePath = node.InnerText.TrimStart('\\', '/').Replace('\\', '/');
                string directory = Path.GetDirectoryName(relativePath);
                string localFilePath = Path.Combine(Config.GameName, relativePath);
                string remoteFileUrl = Config.filesURL + $"game/{relativePath}";

                if (!string.IsNullOrEmpty(directory))
                {
                    string fullDirPath = Path.Combine(Config.GameName, directory);
                    if (!Directory.Exists(fullDirPath))
                        Directory.CreateDirectory(fullDirPath);
                }

                try
                {
                    await DownloadFileAsync(remoteFileUrl, localFilePath);
                    count++;
                    int percent = (int)((double)count / total * 100);
                    progress?.Report(percent);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error downloading '{relativePath}': {ex.Message}");
                }
            }
        }

        // BackgroundWorker method for updating files
        private void updateFiles(object sender, DoWorkEventArgs e)
        {
            XmlDocument clientfiles = new XmlDocument();
            clientfiles.Load(Config.filesURL + "files.xml");
            XmlNodeList nodes = clientfiles.SelectNodes("/configuration/path");
            int count = 0;

            foreach (XmlNode node in nodes)
            {
                try
                {
                    string filePath = "./" + Config.GameName + "/" + node.InnerText;
                    string directory = Path.GetDirectoryName(filePath);

                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    var request = (HttpWebRequest)WebRequest.Create(Config.filesURL + "game/" + node.InnerText);
                    request.Method = "HEAD";

                    FileInfo file = new FileInfo(filePath);
                    fileVerified = file.Name;

                    if (!File.Exists(filePath) || IsRemoteFileModified(request, file))
                    {
                        using (var client = new WebClient())
                            client.DownloadFile(Config.filesURL + "game/" + node.InnerText, filePath);

                        nbTotalFilesChanged++;
                    }

                    Thread.Sleep(3);
                    count++;
                    int percentComplete = (int)((count / (double)nodes.Count) * 100);
                    updateWorker.ReportProgress(percentComplete);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error during update: " + ex.Message);
                }
            }
        }

        // Checks if remote file size differs from local file size
        private bool IsRemoteFileModified(HttpWebRequest request, FileInfo localFile)
        {
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (localFile.Length != response.ContentLength)
                        return true;
                }
            }
            catch (WebException ex)
            {
                Console.WriteLine("Web exception: " + ex.Message);
            }
            return false;
        }

        // Main button click handler
        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (canPlay) Play();
            else if (inUpdate) { Update(); downloadButton.IsEnabled = false; }
            else { Install(); downloadButton.IsEnabled = false; }
        }

        // Repair button handler (used by system tray)
        private void Repair_Click(object sender, RoutedEventArgs e)
        {
            downloadButton.IsEnabled = false;
            if (!inUpdate || !inInstall)
            {
                CloseGame();
                Update();
                inUpdate = true;
                repaired = true;
            }
        }

        public bool GameExeExists()
        {
            string filePath = Path.Combine($"{Config.GameName}.exe");
            return File.Exists(filePath);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            SettingWindow setting = new SettingWindow(this);
            setting.Owner = this;
            setting.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            setting.Show();
            this.IsEnabled = false;
            setting.Closed += (s, ev) => this.IsEnabled = true;
        }

        private void Site_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = Config.SiteURL, UseShellExecute = true });
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            OpenDiscord();
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void minusButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
            Hide();
            notifyIcon.BalloonTipTitle = $"{Config.GameName} Launcher";
            notifyIcon.BalloonTipText = "The launcher is running in the background.";
            notifyIcon.ShowBalloonTip(3000);
        }

        // Handler for the "Repair" item in the system tray menu
        private void RepairMenuItem_Click(object? sender, EventArgs e)
        {
            Repair_Click(sender, new RoutedEventArgs());
        }

        public void OpenDiscord()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Config.DiscordURL,
                UseShellExecute = true
            });
        }

        public void CreateSystemTray()
        {
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            var openMenuItem = new System.Windows.Forms.ToolStripMenuItem("Open");
            openMenuItem.Click += (s, e) => ShowLauncher();
            contextMenu.Items.Add(openMenuItem);

            var playMenuItem = new System.Windows.Forms.ToolStripMenuItem("Play");
            playMenuItem.Click += (s, e) => { if (!inInstall && !inUpdate) Play(); };
            contextMenu.Items.Add(playMenuItem);

            var repairMenuItem = new System.Windows.Forms.ToolStripMenuItem("Repair");
            repairMenuItem.Click += RepairMenuItem_Click;
            contextMenu.Items.Add(repairMenuItem);

            var discordMenuItem = new System.Windows.Forms.ToolStripMenuItem("Discord");
            discordMenuItem.Click += (s, e) => OpenDiscord();
            contextMenu.Items.Add(discordMenuItem);

            var closeMenuItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
            closeMenuItem.Click += (s, e) => Close();
            contextMenu.Items.Add(closeMenuItem);

            notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon = notifyIcon;
        }

        // Shows the launcher from the system tray
        private void ShowLauncher()
        {
            this.Visibility = Visibility.Visible;
            this.WindowState = WindowState.Normal;
            this.Activate();
        }
    }
}