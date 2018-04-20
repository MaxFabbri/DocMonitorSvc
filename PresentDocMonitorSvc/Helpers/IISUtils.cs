using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Web.Administration;

namespace PresentDocMonitorSvc
{
    public class IISUtils
    {
        public static string FindWebAppPathByVirtualFolderName(string virtualFolderName)
        {
            Application webApp = null;
            using (ServerManager serverManager = new Microsoft.Web.Administration.ServerManager())
            {
                var apps = serverManager.Sites.SelectMany(s => s.Applications);
                webApp = apps.SingleOrDefault(a => a.Path.ToLower() == @"/" + virtualFolderName.ToLower());
            }
            return webApp.VirtualDirectories[0].PhysicalPath;
        }
        
    }
}
