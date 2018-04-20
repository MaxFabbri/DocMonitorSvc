using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Reflection;

namespace PresentDocMonitorSvc
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            
            // Initialize array of service(s) to run
            ServicesToRun = new ServiceBase[]
            {
                new WindowsService()
            };

            if (Environment.UserInteractive)
                RunInteractive(ServicesToRun);
            else
                ServiceBase.Run(ServicesToRun);
        }

        private static void RunInteractive(ServiceBase[] servicesToRun)
        {
            MethodInfo onStartMethod = typeof(ServiceBase).GetMethod("OnStart", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (ServiceBase service in servicesToRun)
            {
                Console.WriteLine("Waiting for starting {0}...", service.ServiceName);
                //onStartMethod.Invoke(service, new object[] { new string[] { } });
                onStartMethod.Invoke(service, new object[] { Environment.GetCommandLineArgs() });
            }

            var quit = "";
            while (quit!="quit")
            {
                Console.WriteLine("Please enter 'quit' to stop the service...");
                quit=Console.ReadLine();
            }
            
            MethodInfo onStopMethod = typeof(ServiceBase).GetMethod("OnStop", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (ServiceBase service in servicesToRun)
            {
                Console.WriteLine("Stopping {0}", service.ServiceName);
                onStopMethod.Invoke(service, null);
            }
        }
    }

}
