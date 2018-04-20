using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;

namespace PresentDocMonitorSvc
{
    public partial class WindowsService : ServiceBase
    {
        public const string SERVICE_NAME = "PresentDocMonitorService";
        public const string SERVICE_DISPLAY_NAME = "Present Web Document Monitor Service";

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        // Variabile di istanza principale 
        private DocumentMonitor monitor;

        public WindowsService()
        {
            this.ServiceName = SERVICE_NAME;
        }

        protected override void OnStart(string[] args)
        {

            //logger.Info("Service started, command line args: '{0}'", string.Join(" ", args));

            try
            {
                monitor = new DocumentMonitor(
                new MessageDispatch()
                    .Settings(par =>
                    {

                        // in interpretato andare in properties Settings.settings per aggiornare il file di configurazione

                        // Campo anagrafico Present relativo al valore estratto dalla cartella
                        par.PersonKeyField = Properties.Settings.Default.PersonKeyField;
                        par.MonitoredFolderPath = Properties.Settings.Default.MonitoredFolderPath;
                        par.UserName = Properties.Settings.Default.UserName;
                        par.Password = Properties.Settings.Default.Password;
                        par.UpdateMessage = Convert.ToBoolean(Properties.Settings.Default.UpdateMessage);
                        par.MaxLoggedHours = Convert.ToInt32(Properties.Settings.Default.RefreshLoginInHours);

                        for (int i = 1; i <= Convert.ToInt32(Properties.Settings.Default.MonitoredFileNumber); i++)
                        {
                            par.LanguageKey.Add(new LanguageKey
                            {
                                
                                Subject = Properties.Settings.Default["SubjectResourceKey" + i.ToString()].ToString(),
                                Text = Properties.Settings.Default["TextResourceKey" + i.ToString()].ToString(),
                                IndexFile = i
                            });
                        }

                    }))
                .Settings(par =>
                {
                    //Percorso esplicito della cartella 
                    // es: c:\\inetpub\\wwwroot\\Present.Web\\docs
                    par.PathToMonitoring = Properties.Settings.Default.MonitoredFolderPath;

                    // Formattazione chiave dipendente es: 0000
                    par.FormatDipKey = Properties.Settings.Default.PersonKeyFieldFormat;

                    // mese tredicesima
                    par.ThirteenthMonth = Convert.ToInt32(Properties.Settings.Default.ThirteenthMonth);

                    // mese quattordicesima
                    par.FourteenthMonth= Convert.ToInt32(Properties.Settings.Default.FourteenthMonth);

                    for (int i = 1; i <= Convert.ToInt32(Properties.Settings.Default.MonitoredFileNumber); i++)
                    {
                        par.RegExFiles.Add(new RegExFile
                        {
                            // Regular expression percorso completo cartella + file (CUD o altro file privato o pubblico)
                            FullFileNameRegEx = Properties.Settings.Default["NameFileRegEx" + i.ToString()].ToString(),
                            IndexFile = i,
                            YearPosition = Convert.ToInt32(Properties.Settings.Default["YearFilePosition" + i.ToString()]),
                            MonthPosition = Convert.ToInt32(Properties.Settings.Default["MonthFilePosition" + i.ToString()]),
                            KeyPosition = Convert.ToInt32(Properties.Settings.Default["PersonFileKeyPosition" + i.ToString()]),
                            // definisce se la notifica è pubblica o privata relativa ad una persona
                            isPublicMessage=(Properties.Settings.Default["PublicMessage" + i.ToString()].ToString()!="0")
                        });

                    }

                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Errore durante l'impostazione iniziale dei parametri");
            }

            // Regular expression per sostituire la prima parte della cartella col percorso relativo ~ (tilde)
            var RegExPercorsoRelativo = Properties.Settings.Default.RelativePathRegEx;

            if (monitor != null)
            {
                monitor.Map(data =>
                {
                    data.FullFileName = Regex.Replace(data.FullFileName, RegExPercorsoRelativo, "$1");
                    data.OldFullFileName = Regex.Replace(data.OldFullFileName, RegExPercorsoRelativo, "$1");
                });
            }

            if (monitor != null)
            {
                monitor.StartWatching();
                logger.Info("Service started");
            }
            else
            {
                logger.Info("Service in error");
            }

        }

        protected override void OnStop()
        {
            if (monitor != null)
            {
                monitor.StopWatching();
                monitor.Dispose();
            }
            monitor = null;
            logger.Info("Service stopped");
        }
    }
}
