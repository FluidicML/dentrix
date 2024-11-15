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

        private static ActionResult FL_MaybeFindDentrixDir(Session session, Environment.SpecialFolder folder)
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

            var fallback = Path.Combine(Environment.GetFolderPath(folder), "Dentrix");

            if (File.Exists(Path.Combine(fallback, DtxApiDll)))
            {
                session[PROPERTY] = fallback;
            }

            return ActionResult.Success;
        }

        /// <summary>
        /// Attempts to find the the Dentrix (32-bit) installation automatically.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        [CustomAction]
        public static ActionResult FL_x86_DentrixDirSetProperty(Session session)
        {
            return FL_MaybeFindDentrixDir(session, Environment.SpecialFolder.ProgramFilesX86);
        }

        /// <summary>
        /// Attempts to find the the Dentrix (64-bit) installation automatically.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        [CustomAction]
        public static ActionResult FL_x64_DentrixDirSetProperty(Session session)
        {
            return FL_MaybeFindDentrixDir(session, Environment.SpecialFolder.ProgramFiles);
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
