using System;
using System.Windows.Forms;
using ChessAI.UI;

namespace ChessAI
{
    internal static class Program
    {
        [STAThread]   // Required for Windows Forms (Single Thread Apartment)
        static void Main()
        {
            // Enable modern Windows visual styles (rounded buttons, etc.)
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Launch the main window
            Application.Run(new ChessForm());
        }
    }
}