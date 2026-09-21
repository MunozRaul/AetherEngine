using Aether.Realmheart;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

GameWindowSettings gameSettings = GameWindowSettings.Default;

NativeWindowSettings nativeSettings = NativeWindowSettings.Default;
nativeSettings.Title = "Realmheart";

// After a few hours, i found out that OpenTK/GLFW defaults to requesting an OpenGL 3.3 context.
// Compute shaders (used by GpuRaytracer) require OpenGL 4.3+, so i have to ask for a higher version explicitly
// as otherwise the driver creates a 3.3 context and rejects the compute shaders #version 430
// with a cryptic syntax error instead of a clear "unsupported version" message.
nativeSettings.APIVersion = new Version(4, 3);

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
