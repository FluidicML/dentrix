using Microsoft.Win32;
using System;
using System.IO;
using WixToolset.Dtf.WindowsInstaller;

namespace DentrixDlg
{
    public class CustomActions
    {
        private const string PROPERTY = "FL_DENTRIX_DIR";
        private const string DtxApiDll = "Dentrix.API.dll";

        /// <summary>
        /// Attempts to find the the Dentrix installation automatically.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        [CustomAction]
        public static ActionResult FL_DentrixDirSetProperty(Session session)
        {
            var exePath = string.Empty;

            try
            {
                var hKey = Registry.CurrentUser.OpenSubKey(@"Software\Dentrix Dental Systems, Inc.\Dentrix\General");
                if (hKey != null)
                {
                    Object value = hKey.GetValue("ExePath");
                    if (value != null)
                    {
                        exePath = value.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                session.Log("Encountered error when accessing registry: {msg}", e.Message);
            }

            if (!string.IsNullOrEmpty(exePath))
            {
                session[PROPERTY] = exePath;

                return ActionResult.Success;
            }

            var dir1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Dentrix");

            if (File.Exists(Path.Combine(dir1, DtxApiDll)))
            {
                session[PROPERTY] = dir1;

                return ActionResult.Success;
            }

            var dir2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Dentrix");

            if (File.Exists(Path.Combine(dir2, DtxApiDll)))
            {
                session[PROPERTY] = dir2;
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
