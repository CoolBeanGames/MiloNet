using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Debugger
{
    /// <summary>
    /// a class for displaying debug information during development
    /// onlu to be used/functional during a debug build
    /// </summary>
    public static class Debug
    {
        /// <summary>
        /// a list containing all the messages
        /// </summary>
        public static List<string> messages = new List<string>();
        /// <summary>
        /// wether or not the list has been initialized
        /// </summary>
        public static bool initialized = false;
        /// <summary>
        /// the filename to save the file under
        /// </summary>
        public const string LogFileName = "EngineLog.txt";

        //for opening the console
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_SHOW = 5;

        /// <summary>
        /// open the console to display it along side the main
        /// window
        /// </summary>
        [Conditional("DEBUG")] // This attribute ensures this method (and calls to it) are only compiled in DEBUG builds
        public static void OpenConsole()
        {
            if (!initialized)
            {
                try
                {
                    // Try to allocate a console window (Windows specific)
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        if (AllocConsole())
                        {
                            IntPtr consoleHandle = GetConsoleWindow();
                            if (consoleHandle != IntPtr.Zero)
                            {
                                ShowWindow(consoleHandle, SW_SHOW);
                                Console.WriteLine("Milo Engine Debug Console Initialized.");
                                initialized = true;
                            }
                            else
                            {
                                // Fallback or log that console couldn't be obtained
                                System.Diagnostics.Debug.WriteLine("Milo Engine: Could not get console window handle.");
                                // We can still use System.Diagnostics.Debug.WriteLine as a fallback if a separate console isn't critical
                                // or consider an in-game console.
                                initialized = false; // Or set to true and just use System.Diagnostics.Debug
                            }
                        }
                        else
                        {
                            // If AllocConsole fails (e.g., already has a console), we can try to use the existing one.
                            // For simplicity here, we'll just log to the standard debug output.
                            System.Diagnostics.Debug.WriteLine($"Milo Engine: Failed to allocate console. Error Code: {Marshal.GetLastWin32Error()}");
                            // Attempt to use existing console if possible, or just mark as "initialized" for logging to System.Diagnostics.Debug
                            // This might happen if running from a command prompt already.
                            // For now, we'll assume if AllocConsole fails, we might already have one or will fallback.
                            // A more robust solution might check GetConsoleWindow() first.
                            Console.WriteLine("Milo Engine Debug Console (using existing or fallback)."); // Try writing to see if it appears
                            initialized = true;
                        }
                    }
                    else
                    {
                        // For non-Windows platforms, we'll just use System.Diagnostics.Debug.WriteLine
                        // or you could integrate a simple in-game console UI later.
                        System.Diagnostics.Debug.WriteLine("Milo Engine: Console allocation is Windows-specific. Using System.Diagnostics.Debug for logging.");
                        initialized = true; // Mark as initialized to allow logging via System.Diagnostics.Debug
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Milo Engine: Error opening console: {ex.Message}");
                    initialized = false; // Ensure it's false if something went wrong
                }

                if (initialized)
                {
                    Log("Debug console started.");
                }
            }
        }

        /// <summary>
        /// log a standard informational message to the screen
        /// </summary>
        /// <param name="message">the message to be logged</param>
        [Conditional("DEBUG")]
        public static void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string msg = $"[{timestamp}][LOG] {message}";
            messages.Add(msg);
            PushMessage(msg);
        }

        /// <summary>
        /// log an error message to the screen
        /// </summary>
        /// <param name="message">the message to be logged</param>
        [Conditional("DEBUG")]
        public static void LogError(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string msg = $"[{timestamp}][ERROR] {message}";
            messages.Add(msg);
            PushMessage(msg, ConsoleColor.Red); // Example: Log errors in red
        }

        /// <summary>
        /// log a warning message to the screen
        /// </summary>
        /// <param name="message">the message to be loged</param>
        [Conditional("DEBUG")]
        public static void LogWarning(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string msg = $"[{timestamp}][WARNING] {message}";
            messages.Add(msg);
            PushMessage(msg, ConsoleColor.Yellow); // Example: Log warnings in yellow
        }

        /// <summary>
        /// push a message, displaying it to the screen
        /// </summary>
        /// <param name="fullMessage">the message along with the context</param>
        [Conditional("DEBUG")]
        private static void PushMessage(string fullMessage, ConsoleColor? color = null)
        {
            if (initialized)
            {
                // If we successfully allocated a console on Windows and can write to it
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && GetConsoleWindow() != IntPtr.Zero)
                {
                    ConsoleColor originalColor = Console.ForegroundColor;
                    if (color.HasValue)
                    {
                        Console.ForegroundColor = color.Value;
                    }
                    Console.WriteLine(fullMessage);
                    if (color.HasValue)
                    {
                        Console.ForegroundColor = originalColor;
                    }
                }
                else
                {
                    // Fallback for non-Windows or if console couldn't be shown,
                    // write to the IDE's debug output.
                    System.Diagnostics.Debug.WriteLine(fullMessage);
                }
            }
            else
            {
                // If not initialized, still try to output to System.Diagnostics.Debug
                // This ensures messages aren't lost if OpenConsole wasn't called or failed silently.
                System.Diagnostics.Debug.WriteLine($"(Uninitialized Console) {fullMessage}");
            }
        }

        /// <summary>
        /// end the console window (write to file)
        /// </summary>
        [Conditional("DEBUG")]
        public static void End()
        {
            Log("Debug session ending. Writing log to file...");
            WriteFile();
            initialized = false; // Mark as uninitialized
            // No explicit "closing" of the AllocConsole'd window is typically needed,
            // it closes with the parent process. FreeConsole() exists but can be tricky.
        }


        /// <summary>
        /// flush the log to the disk
        /// </summary>
        [Conditional("DEBUG")]
        private static void WriteFile()
        {
            // We only attempt to write the file if there are messages.
            // The initialized check isn't strictly necessary here if we always want to write,
            // but it's good practice if OpenConsole must be called first.
            // However, for robustness, let's allow writing even if console init failed,
            // as long as messages were collected.
            if (messages.Count > 0)
            {
                try
                {
                    // You might want to put this in a more specific logs directory
                    // e.g., Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", LogFileName);
                    // Ensure the directory exists if you do: Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    string filePath = Path.Combine(Environment.CurrentDirectory, LogFileName);
                    File.WriteAllLines(filePath, messages);
                    PushMessage($"Log successfully written to: {filePath}");
                }
                catch (Exception ex)
                {
                    PushMessage($"[ERROR] Failed to write log file: {ex.Message}", ConsoleColor.Red);
                    // Also output to System.Diagnostics.Debug as a last resort
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to write log file: {ex.Message}");
                }
            }
            else
            {
                PushMessage("No messages to write to log file.");
            }
        }
    }
}
