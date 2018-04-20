using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;


namespace PresentDocMonitorSvc
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        private ServiceProcessInstaller process;
        private ServiceInstaller service;

        public ProjectInstaller()
        {
            process = new ServiceProcessInstaller();
            process.Account = ServiceAccount.LocalSystem;
            service = new ServiceInstaller();
            service.ServiceName = WindowsService.SERVICE_NAME;
            service.DisplayName = WindowsService.SERVICE_DISPLAY_NAME;
            service.Description = WindowsService.SERVICE_DISPLAY_NAME;
            Installers.Add(process);
            Installers.Add(service);
        }
    }
}
