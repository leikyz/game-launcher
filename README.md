Game Launcher
<br>
<img width="793" height="431" alt="gamelauncher" src="https://github.com/user-attachments/assets/7d3ade73-35c1-4889-926b-6db5fd716769" />
<br>
<br>

Features

Core :
 - Automatically downloads and installs the game from a remote server.
 - Verifies local game files against a remote file manifest, downloading only the files that are new or have changed.
 - Launches the game executable directly.
 -  A "Repair" option (in Settings and tray menu) that forces a full file re-check and re-download, fixing corrupted or missing files.
 - The main panel displays the latest game news and updates, pulled from remote sources (in this demo, hard-coded).
 - A visual progress bar and percentage text show the status of any downloads or updates.

Settings :
  - Open Game Folder
  - Quick Links to Website & Discord
  - Launch on PC Startup (Toggles a Windows Registry key)

System & Community Integration
-  Automatically connects to Discord and updates the user's status (e.g., "On the Launcher" or "Playing..").
- The launcher can be minimized to the system tray, where it continues to run in the background.

Tray Context Menu: Right-clicking the tray icon provides quick access to:
- Open Launcher
- Play Game
- Repair
- Discord
- Exit

Technology Stack
- C# / .NET
- WPF (Windows Presentation Foundation) for the UI
- DiscordRPC-Sharp for Discord Rich Presence integration
- System.Net.Http (HttpClient) for modern asynchronous file downloads
- System.Xml for parsing the file manifest
