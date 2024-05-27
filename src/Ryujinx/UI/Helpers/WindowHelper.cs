using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Ryujinx.Ava.UI.Windows;

namespace Ryujinx.Ava.UI.Helpers
{
    public class WindowHelper
    {
        public static Window GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime al)
            {
                foreach (Window item in al.Windows)
                {
                    if (item is MainWindow window)
                    {
                        return window;
                    }
                }
            }

            return null;
        }
    }
}