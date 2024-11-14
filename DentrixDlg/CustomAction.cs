using WixToolset.Dtf.WindowsInstaller;

namespace DentrixDlg
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult FindDentrix(Session session)
        {
            // string myPropertyValue = session["MY_PROPERTY"];
            session.Log("Begin CustomAction1");

            return ActionResult.Success;
        }
    }
}
