using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace MidiBleWpfSample.Services
{
    public static class LogService
    {
        public static ObservableCollection<string> Messages { get; } = new ObservableCollection<string>();

        public static void Log(string message)
        {
            // Ensure the update happens on the UI thread
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Add a timestamp for clarity
                string logEntry = $"{DateTime.Now:HH:mm:ss}: {message}";
                Messages.Insert(0, logEntry); // Insert at the top to show the latest first

                // Optional: Limit the number of messages to prevent memory issues
                if (Messages.Count > 200)
                {
                    Messages.RemoveAt(Messages.Count - 1);
                }
            }));
        }
    }
}
