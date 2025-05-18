using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        /// open the console to display it along side the main
        /// window
        /// </summary>
        public static void OpenConsole()
        {
            //todo open the console along side the message
        }

        /// <summary>
        /// log a standard informational message to the screen
        /// </summary>
        /// <param name="message">the message to be logged</param>
        public static void Log(string message)
        {
            string msg = ("[LOG] " + message);
            messages.Add(msg);
            pushMessage(msg);
        }

        /// <summary>
        /// log an error message to the screen
        /// </summary>
        /// <param name="message">the message to be logged</param>
        public static void LogError(string message)
        {
            string msg = ("[ERROR] " + message);
            messages.Add(msg);
            pushMessage(msg);
        }

        /// <summary>
        /// log a warning message to the screen
        /// </summary>
        /// <param name="message">the message to be loged</param>
        public static void LogWarning(string message)
        {
            string msg = ("[WARNING] " + message);
            messages.Add(msg);
            pushMessage(msg);
        }

        /// <summary>
        /// push a message, displaying it to the screen
        /// </summary>
        /// <param name="fullMessage">the message along with the context</param>
        private static void pushMessage(string fullMessage)
        {
            //todo: display the message in the console
            if(initialized)
            {

            }
        }

        /// <summary>
        /// end the console window (write to file)
        /// </summary>
        public static void end()
        {
            writeFile();
        }


        /// <summary>
        /// flush the log to the disk
        /// </summary>
        private static void writeFile()
        {
            //todo: write data to the file
            if(initialized)
            {

            }
        }
     }
}
