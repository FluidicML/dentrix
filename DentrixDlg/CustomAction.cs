using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using WixToolset.Dtf.WindowsInstaller;

namespace DentrixDlg
{
    public class CustomActions
    {
        private const string PROPERTY = "FL_DENTRIX_DIR";
        private const string DtxApiDll = "Dentrix.API.dll";
        private const string DtxRegPath = @"SOFTWARE\Dentrix Dental Systems, Inc.\Dentrix\General";

        /// <summary>
        /// Attempts to find the the Dentrix (32-bit) installation automatically.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        [CustomAction]
        public static ActionResult FL_DentrixDirSetProperty(Session session)
        {
            var regViews = new List<RegistryView>() { RegistryView.Registry64, RegistryView.Registry32 };

            // When a 32-bit application accesses the Registry64 view, it receives the 32-bit
            // registry. This might mean the second registry lookup is redundant.

            foreach (RegistryView regView in regViews)
            {
                RegistryKey hklm = null;
                RegistryKey subKey = null;

                try
                {
                    hklm = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, regView);
                    subKey = Registry.CurrentUser.OpenSubKey(Path.Combine("Software", DtxRegPath));

                    var value = subKey?.GetValue("ExePath");
                    if (value != null)
                    {
                        session[PROPERTY] = value.ToString();
                        return ActionResult.Success;
                    }
                }
                catch (Exception e)
                {
                    session.Log("Encountered error when accessing registry: {msg}", e.Message);
                }
                finally
                {
                    hklm?.Dispose();
                    subKey?.Dispose();
                }
            }

            var programFiles = new List<Environment.SpecialFolder>() {
                Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86
            };

            foreach (Environment.SpecialFolder dir in programFiles)
            {
                var fallback = Path.Combine(Environment.GetFolderPath(dir), "Dentrix");

                if (File.Exists(Path.Combine(fallback, DtxApiDll)))
                {
                    session[PROPERTY] = fallback;

                    return ActionResult.Success;
                }
            }

            return ActionResult.Success;
        }

        /// <summary>
        /// A demonstration of a deferred action with a property value set.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        [CustomAction]
        public static ActionResult FL_DentrixDirReadProperty(Session session)
        {
            // Uncomment the following to run the debugger at this point. You
            // can use this to confirm the value of our CustomActionData is that
            // of our `PROPERTY` set earlier.

            // System.Diagnostics.Debugger.Launch();

            var data = session.CustomActionData;

            return ActionResult.Success;
        }
    }
}
