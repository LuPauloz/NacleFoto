using System;
using System.IO;
using Microsoft.Win32;

namespace NacleFoto.Windows.Shell
{
    /// <summary>
    /// Explorer integration: context menu and file association.
    /// Everything in HKCU: does not require Administrator and does not modify the system.
    /// </summary>
    public static class ShellIntegration
    {
        private const string AppName = "Nacle Photo";

        public static void Register(string exePath)
        {
            try
            {
                var command = "\"" + exePath + "\" \"%1\"";

                // context menu on files
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\*\shell\NacleFoto"))
                {
                    k.SetValue("", "Open no " + AppName);
                    k.SetValue("Icon", exePath);
                    using (var c = k.CreateSubKey("command")) c.SetValue("", command);
                }

                // context menu on folders
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\shell\NacleFoto"))
                {
                    k.SetValue("", "Open no " + AppName);
                    k.SetValue("Icon", exePath);
                    using (var c = k.CreateSubKey("command")) c.SetValue("", command);
                }

                // file association for .jpg
                RegisterAssociation(".jpg", exePath);
                RegisterAssociation(".jpeg", exePath);
            }
            catch (Exception)
            {
                // integration is optional: failure here does not prevent the app from working
            }
        }


        private static void RegisterAssociation(string extension, string exePath)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + extension))
                k.SetValue("", "NacleFoto.Photo");

            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\NacleFoto.Photo"))
            {
                k.SetValue("", "Photo (" + AppName + ")");
                using (var c = k.CreateSubKey("DefaultIcon")) c.SetValue("", "\"" + exePath + "\",0");
                using (var c = k.CreateSubKey(@"shell\open\command"))
                    c.SetValue("", "\"" + exePath + "\" \"%1\"");
            }
        }

        public static void Unregister()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\NacleFoto");
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\NacleFoto");
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\NacleFoto.Photo");
            }
            catch (Exception)
            {
            }
        }

    }
}
