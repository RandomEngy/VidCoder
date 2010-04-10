using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;
using System.IO;

namespace VidCoder.Model
{
    public class Logger : IDisposable
    {
        private StreamWriter logFile;
        private bool disposed;

        public Logger()
        {
            HandBrakeInstance.MessageLogged += this.OnMessageLogged;
            HandBrakeInstance.ErrorLogged += this.OnErrorLogged;

            string logFolder = Path.Combine(Utilities.AppFolder, "Logs");
            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }

            string logFilePath = Path.Combine(logFolder, DateTime.UtcNow.ToString("yyyy-MM-dd HH-mm-ss") + ".txt");
            this.logFile = new StreamWriter(logFilePath);
        }

        public void Log(string message)
        {
            this.logFile.WriteLine(message);
            this.logFile.Flush();
        }

        /// <summary>
        /// Frees any resources associated with this object.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                this.disposed = true;
                this.logFile.Close();
            }
        }

        private void OnMessageLogged(object sender, MessageLoggedEventArgs e)
        {
            this.logFile.WriteLine(e.Message);
            this.logFile.Flush();
        }

        private void OnErrorLogged(object sender, MessageLoggedEventArgs e)
        {
            this.logFile.WriteLine("ERROR: " + e.Message);
            this.logFile.Flush();
        }
    }
}
