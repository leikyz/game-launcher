using Microsoft.Win32; // Ajout pour la gestion du Registre
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Launcher
{
    /// <summary>
    /// Logique d'interaction pour SettingWindow.xaml
    /// </summary>
    public partial class SettingWindow : Window
    {
        private MainWindow mainWindow;
        // Nom de l'application tel qu'il apparaîtra dans le gestionnaire de démarrage
        private const string AppName = "Launcher";
        // Clé de Registre pour le démarrage
        private const string StartupRegistryKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        public SettingWindow(MainWindow mainWindow)
        {
            InitializeComponent();

            // Appliquer la bordure
            var color = (Color)ColorConverter.ConvertFromString("#190C0C");
            this.BorderBrush = new SolidColorBrush(color);
            this.BorderThickness = new Thickness(5);

            this.mainWindow = mainWindow; // Stocke la référence

            // Désactiver le bouton Réparer si une màj/install est en cours
            if (mainWindow.InUpdate || mainWindow.Repaired || mainWindow.InInstall)
                btnReload.IsEnabled = false;

            // Charger la version
            string launcherVersionFilePath = "Launcher.version";
            if (File.Exists(launcherVersionFilePath))
            {
                string version = "v" + File.ReadAllText(launcherVersionFilePath);
                versionLabel.Content = version;
            }

            // Charger l'état de la case "Lancer au démarrage"
            LoadStartupCheckState();
        }

        /// <summary>
        /// Vérifie le Registre pour voir si l'application est configurée pour démarrer avec Windows
        /// </summary>
        private void LoadStartupCheckState()
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false))
                {
                    var value = rk?.GetValue(AppName);
                    bool isStartup = (value != null);

                    chkStartup.IsChecked = isStartup;

                    // Synchroniser avec les paramètres si différent
                    if (isStartup != Settings.Default.launchOnStartup)
                    {
                        Settings.Default.launchOnStartup = isStartup;
                        Settings.Default.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible de lire les paramètres de démarrage : " + ex.Message);
                chkStartup.IsEnabled = false;
            }
        }

        private void folderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = Directory.GetCurrentDirectory() + "/app",
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception s) // Correction de la variable (e -> s)
            {
                MessageBox.Show("Impossible de trouver le dossier : " + s.Message);
            }
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Repair_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.downloadButton.IsEnabled = false;
            btnReload.IsEnabled = false;
            if (!mainWindow.InUpdate || !mainWindow.InInstall)
            {
                mainWindow.CloseGame();
                mainWindow.Update();
                mainWindow.InUpdate = true;
                mainWindow.Repaired = true;
            }

            this.Close();
        }

        private void Site_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = Config.SiteURL,
                UseShellExecute = true
            });
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            OpenDiscord();
        }

        private void OpenDiscord()
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = Config.DiscordURL,
                UseShellExecute = true
            });
        }

        // --- NOUVEAUX GESTIONNAIRES POUR LA CASE À COCHER DE DÉMARRAGE ---

        private void chkStartup_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ajoute l'application au démarrage
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true))
                {
                    // Utilise le chemin de l'exécutable en cours
                    string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    rk.SetValue(AppName, $"\"{exePath}\""); // Ajout de guillemets pour les chemins avec espaces
                }

                Settings.Default.launchOnStartup = true;
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de l'activation du démarrage automatique : " + ex.Message);
                chkStartup.IsChecked = false; // Annuler le cochage
            }
        }

        private void chkStartup_UnChecked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Supprime l'application du démarrage
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true))
                {
                    rk.DeleteValue(AppName, false);
                }

                Settings.Default.launchOnStartup = false;
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de la désactivation du démarrage automatique : " + ex.Message);
                chkStartup.IsChecked = true; // Rétablir le cochage
            }
        }
    }
}