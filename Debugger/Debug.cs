// In Debugger.Debug.cs
using System;
using System.Collections.Generic;
using System.Diagnostics; // For [Conditional] and System.Diagnostics.Debug
using System.IO;
using System.Runtime.InteropServices;

namespace Debugger
{
    public static class Debug
    {
        public static List<string> messages = new List<string>();
        public static bool initialized { get; private set; } = false; // Readonly public, private set
        private static bool consoleWasAllocatedByThisClass = false; // Track if we called AllocConsole

        public const string LogFileName = "EngineLog.txt";

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FreeConsole();

        const int SW_SHOW = 5;

        [Conditional("DEBUG")]
        public static void OpenConsole()
        {
            if (initialized) return;

            // This method body will only exist in DEBUG builds of the Debugger assembly.
            // Calls to it from MiloNet will only exist if MiloNet is also in DEBUG.
            System.Diagnostics.Debug.WriteLine("Debug.OpenConsole() entered."); // Use System.Diagnostics.Debug for early diagnostics

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr currentConsole = GetConsoleWindow();
                if (currentConsole != IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("Milo Engine: Console already exists. Attaching to it.");
                    // Optionally, ensure it's visible if it might be hidden
                    // ShowWindow(currentConsole, SW_SHOW); 
                    Console.WriteLine("Milo Engine Debug Console (attached to existing).");
                    initialized = true;
                    consoleWasAllocatedByThisClass = false;
                }
                else
                {
                    if (AllocConsole())
                    {
                        System.Diagnostics.Debug.WriteLine("Milo Engine: AllocConsole() successful.");
                        IntPtr newConsoleHandle = GetConsoleWindow();
                        if (newConsoleHandle != IntPtr.Zero)
                        {
                            ShowWindow(newConsoleHandle, SW_SHOW);
                            Console.WriteLine("Milo Engine Debug Console Initialized (newly allocated).");
                            initialized = true;
                            consoleWasAllocatedByThisClass = true;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Milo Engine: AllocConsole succeeded but GetConsoleWindow returned IntPtr.Zero.");
                            initialized = false; // Failed to get handle
                        }
                    }
                    else
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        System.Diagnostics.Debug.WriteLine($"Milo Engine: Failed to allocate console. Win32 Error Code: {errorCode}");
                        // If it failed, maybe it's because it's a console app but GetConsoleWindow initially returned Zero for some reason.
                        // Try writing to Console anyway to see if it appears in an existing one.
                        try
                        {
                            Console.WriteLine("Milo Engine Debug Console (attempting to use existing/fallback after AllocConsole failure).");
                            initialized = true; // Assume we can use the implicit console for logging
                            consoleWasAllocatedByThisClass = false;
                        }
                        catch (IOException)
                        {
                            System.Diagnostics.Debug.WriteLine("Milo Engine: Cannot write to Console after AllocConsole failure.");
                            initialized = false;
                        }
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Milo Engine: Console management is Windows-specific. Using System.Diagnostics.Debug for logging.");
                initialized = true; // Allow logging to System.Diagnostics.Debug
                consoleWasAllocatedByThisClass = false;
            }

            if (initialized)
            {
                Log("Debug system started."); // This now correctly uses the initialized state
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Debug system failed to initialize a visible console but will use System.Diagnostics.Debug.");
                // We can still allow logging to System.Diagnostics.Debug by setting initialized to true
                // but consoleWasAllocatedByThisClass will be false.
                // For PushMessage to work without the "(Uninitialized Console)" prefix, initialized needs to be true.
                initialized = true;
                consoleWasAllocatedByThisClass = false; // Explicitly state no console was allocated by us.
                Log("Debug system started (fallback to System.Diagnostics.Debug).");
            }
        }

        [Conditional("DEBUG")]
        public static void Log(string message) // Keep your existing Log, LogError, LogWarning
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string msg = $"[{timestamp}][LOG] {message}";
            messages.Add(msg);
            PushMessage(msg);
        }

        public static void LogError(string message) // Keep your existing Log, LogError, LogWarning
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string msg = $"[{timestamp}][ERROR] {message}";
            messages.Add(msg);
            PushMessage(msg,ConsoleColor.Red);
        }

        public static void LogWarning(string message) // Keep your existing Log, LogError, LogWarning
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string msg = $"[{timestamp}][WARNING] {message}";
            messages.Add(msg);
            PushMessage(msg,ConsoleColor.Yellow);
        }


        [Conditional("DEBUG")]
        private static void PushMessage(string fullMessage, ConsoleColor? color = null)
        {
            // Use System.Diagnostics.Debug.WriteLine unconditionally for all messages if you want them in the VS Output window
            // System.Diagnostics.Debug.WriteLine(fullMessage); 

            if (initialized) // This should now be more reliable
            {
                // Check if we have a usable console window (either pre-existing or allocated)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && GetConsoleWindow() != IntPtr.Zero)
                {
                    try
                    {
                        ConsoleColor originalColor = Console.ForegroundColor;
                        if (color.HasValue)
                        {
                            Console.ForegroundColor = color.Value;
                        }
                        Console.WriteLine(fullMessage); // This will write to the console
                        if (color.HasValue)
                        {
                            Console.ForegroundColor = originalColor;
                        }
                    }
                    catch (IOException ex) // Handle cases where Console might not be available
                    {
                        System.Diagnostics.Debug.WriteLine($"Console.WriteLine failed: {ex.Message}. Falling back to System.Diagnostics.Debug for: {fullMessage}");
                        System.Diagnostics.Debug.WriteLine(fullMessage); // Fallback
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(fullMessage); // Fallback for non-Windows or no console handle
                }
            }
            else
            {
                // This case should ideally not be hit if OpenConsole is called and DEBUG is defined.
                System.Diagnostics.Debug.WriteLine($"(Debug Uninitialized) {fullMessage}");
            }
        }

        [Conditional("DEBUG")]
        public static void End()
        {
            // These System.Diagnostics.Debug.WriteLine calls will appear in VS Output Window
            System.Diagnostics.Debug.WriteLine("Debug.End() method entered.");

            Log("Debug session ending. Writing log to file..."); // This will use PushMessage
            WriteFile();

            if (consoleWasAllocatedByThisClass && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                System.Diagnostics.Debug.WriteLine("Attempting to call FreeConsole() as console was allocated by this class.");
                if (FreeConsole())
                {
                    System.Diagnostics.Debug.WriteLine("FreeConsole() returned true.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"FreeConsole() failed. Win32 Error Code: {Marshal.GetLastWin32Error()}");
                }
            }
            else if (initialized && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                System.Diagnostics.Debug.WriteLine("Skipping FreeConsole() as console was pre-existing or not allocated by this class.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Skipping FreeConsole() as not on Windows or not initialized for console allocation.");
            }

            initialized = false;
            consoleWasAllocatedByThisClass = false;
            System.Diagnostics.Debug.WriteLine("Debug.End() method finished.");
        }

        [Conditional("DEBUG")]
        private static void WriteFile() // Keep your existing WriteFile
        {
            if (messages.Count > 0)
            {
                try
                {
                    string filePath = Path.Combine(Environment.CurrentDirectory, LogFileName);
                    File.WriteAllLines(filePath, messages);
                    PushMessage($"Log successfully written to: {filePath}");
                }
                catch (Exception ex)
                {
                    PushMessage($"[ERROR] Failed to write log file: {ex.Message}", ConsoleColor.Red);
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