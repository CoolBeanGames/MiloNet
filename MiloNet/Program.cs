using System;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Debugger;
using MiloRender;


namespace MiloNet
{
    internal class Program
    {
        private static IWindow window; 
        private static Render render; 
        static void Main(string[] args)
        {
            Debug.OpenConsole();
            // Create a window with the default options
            WindowOptions options = WindowOptions.Default;
            options.Size = new Vector2D<int>(800, 600);
            options.Title = "MiloNet";
            window = Window.Create(options);
            // Set up the event handlers
            window.Load += OnLoad;
            window.Update += OnUpdate;
            window.Render += OnRender;
            window.Resize += OnResize;
            window.Closing += OnClose;
            // Run the window
            window.Run();
        }

        static void OnLoad()
        {
            Debug.Log("Window Loaded");
            render = new Render();
            // Initialize OpenGL or other resources here
        }

        static void OnUpdate(double delta)
        {
            // Update your application logic here
        }

        static void OnRender(double delta)
        {
            // Render your application here
            render.BeginDraw();
            //drawing code here
            render.EndDraw();
        }

        static void OnResize(Vector2D<int> size)
        {
            // Handle window resizing here
        }

        static void OnClose()
        {
            Debug.Log("Window Closing");
            Debug.End();
            // Clean up resources here
            //window?.Close();
        }
    }
}
