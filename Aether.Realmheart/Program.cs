using Aether.Realmheart;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

GameWindowSettings gameSettings = GameWindowSettings.Default;

NativeWindowSettings nativeSettings = NativeWindowSettings.Default;
nativeSettings.Title = "Realmheart";

//NativeWindowSettings nativeSettings = new NativeWindowSettings
//{
//    ClientSize = new Vector2i(1280, 720),
//    Title = "Realmheart"
//};

MonitorInfo primaryMonitorInfo = Monitors.GetPrimaryMonitor();
nativeSettings.CurrentMonitor = primaryMonitorInfo.Handle;
nativeSettings.WindowState = WindowState.Fullscreen;

using GameApp app = new GameApp(GameWindowSettings.Default, nativeSettings);
app.Run();
