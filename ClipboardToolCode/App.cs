using System;
using System.Windows;

namespace ClipboardTool
{
    public class App : Application
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                var app = new App();
                var window = new MainWindow();
                app.Run(window);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "启动错误");
            }
        }
    }
}
