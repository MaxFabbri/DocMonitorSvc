using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CustomResourceProviders;
using Present.Data;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Threading;

namespace PresentDocMonitorSvc
{
    class DocumentMonitor:IDisposable
    {

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        // classe contenente i parametri
        DocumentMonitorParameters param = new DocumentMonitorParameters();

        // smista i messaggi
        readonly IMessageDispatch messageDispatch;

        // funzione di callback di mapping
        Action<DocumentMonitorData> map;

        FileSystemWatcher fileWatcher;

        // thread secondario che smista i messaggi
        Thread messageNotificationThread = null;

        // accodamento messaggi
        Queue<DocumentMonitorData> messageNotificationQueue = null;

        // l'app sta girando
        bool isRunning = false;

        // filtro eventi uguali
        string sameFileFilter;

        Object lockQueue=null;

        // chiavi registry per fotografare la situazione della cartella
        const string REG_KEY_PROGRAM = "FileMonitor";
        const string REG_KEY_SNAPSHOT_LIST = "SnapShopFileList";

        /// <summary>
        /// Impostazione parametri
        /// </summary>
        /// <param name="Parameters"></param>
        /// <returns></returns>
        public DocumentMonitor Settings(Action<DocumentMonitorParameters> Parameters)
        {
            param.RegExFiles = new List<RegExFile>();
            Parameters(param);
            return this;
        }

        /// <summary>
        /// costruttore classe
        /// </summary>
        /// <param name="MessageDispatch"></param>
        public DocumentMonitor(IMessageDispatch MessageDispatch)
        {

            messageDispatch = MessageDispatch;

            try
            {
                fileWatcher = new FileSystemWatcher();
                fileWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                fileWatcher.Filter = "*.*";
                fileWatcher.IncludeSubdirectories = true;

                fileWatcher.Created += OnFolderCreatedOrChanged;
                fileWatcher.Changed += OnFolderCreatedOrChanged;
                fileWatcher.Renamed += OnFolderRenamed;
                fileWatcher.Deleted += OnFolderDeleted;
                messageNotificationQueue= new Queue<DocumentMonitorData>();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "public DocumentMonitor(IMessageDispatch MessageDispatch)" );
            }

        }

        /// <summary>
        /// evento file creato o modificato
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        void OnFolderCreatedOrChanged(object source, FileSystemEventArgs e)
        {

            if (FilterEvents(e.FullPath + eFileState.CreatedOrChanged.ToString()))
            {
                var fullPath = AdjustBackSlash(e.FullPath);
                var fileData = ExctractFileData(fullPath);

                if (fileData.IndexFile > 0)
                {
                    
                    fileData.FullFileName = fullPath;
                    if (map != null)
                        map.Invoke(fileData);

                    AddMessageNotificationQueue(fileData);
            //        messageDispatch.SendsNotification(fileData);
                }
            }
        }

        /// <summary>
        /// evento file rinominato
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnFolderRenamed(object sender, RenamedEventArgs e)
        {
            if (FilterEvents(e.FullPath + e.OldFullPath + eFileState.Renamed.ToString()))
            {
                var fullPath = AdjustBackSlash(e.FullPath);
                var oldFullPath = AdjustBackSlash(e.OldFullPath);

                var newFileData = ExctractFileData(fullPath);
                var oldFileData = ExctractFileData(oldFullPath);

                // uno dei due percorsi deve essere valido
                if ((newFileData.IndexFile>0) || (oldFileData.IndexFile>0))
                {
                    if ((newFileData.IndexFile>0) && (oldFileData.IndexFile>0))
                    {
                        // tutte e due sono validi
                        newFileData.FileState = eFileState.Renamed;
                        newFileData.FullFileName = fullPath;
                        newFileData.OldFullFileName = oldFullPath;
                        newFileData.OldValue = oldFileData.Value;
                        if (map!=null)
                            map.Invoke(newFileData);
                        AddMessageNotificationQueue(newFileData);
                        //messageDispatch.SendsNotification(newFileData);
                    }
                    else if (newFileData.IndexFile==0)
                    {
                        // il nuovo non è valido è come se fosse stato cancellato
                        oldFileData.FileState = eFileState.Deleted;
                        oldFileData.FullFileName = oldFullPath;
                        if (map!=null)
                            map.Invoke(oldFileData);
                        AddMessageNotificationQueue(oldFileData);
                        //messageDispatch.SendsNotification(oldFileData);
                    }
                    else if (oldFileData.IndexFile==0)
                    {
                        // il vecchio non è valido è come se fosse stato creato
                        newFileData.FileState = eFileState.CreatedOrChanged;
                        newFileData.FullFileName = fullPath;
                        if (map!=null)
                            map.Invoke(newFileData);
                        AddMessageNotificationQueue(newFileData);
                        //messageDispatch.SendsNotification(newFileData);
                    }
                }
            }
        }

        /// <summary>
        /// evento file cancellato
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnFolderDeleted(object sender, FileSystemEventArgs e)
        {
            if (FilterEvents(e.FullPath + eFileState.Deleted.ToString()))
            {
                var fullPath = AdjustBackSlash(e.FullPath);
                var fileData = ExctractFileData(fullPath);

                if (fileData.IndexFile>0)
                {
                    fileData.FileState = eFileState.Deleted;
                    fileData.FullFileName = fullPath;
                    if (map!=null)
                        map.Invoke(fileData);

                    AddMessageNotificationQueue(fileData);
                    //messageDispatch.SendsNotification(fileData);
                }
            }
        }

        /// <summary>
        /// Aggiunge i dati del documento nella coda dei processi
        /// </summary>
        /// <param name="DMD"></param>
        void AddMessageNotificationQueue(DocumentMonitorData Data)
        {
            lock (lockQueue)
            {
                #if (DEBUG)
                {
                    Console.WriteLine("Aggiunto in coda " + Data.FullFileName);
                }
                #endif
                messageNotificationQueue.Enqueue(Data);
            }
        }

        /// <summary>
        /// filtra eventi uguali
        /// </summary>
        /// <param name="Event"></param>
        /// <returns></returns>
        bool FilterEvents(string Event)
        {
            var res = (sameFileFilter != Event);
            if (res)
            {
                sameFileFilter = Event;
            }
            return res;
        }

        /// <summary>
        /// Treath secondario che smista i messaggi
        /// </summary>
        void NotificationMessage()
        {

            while (isRunning)
            {
                Thread.Sleep(10); 
                while (messageNotificationQueue.Count > 0)
                {
                    DocumentMonitorData dmd;
                    lock (lockQueue)
                    {
                        dmd = messageNotificationQueue.Dequeue();
                        #if (DEBUG)
                        {
                            Console.WriteLine("smista coda " + dmd.FullFileName);
                        }
                        #endif
                    }
                    messageDispatch.SendsNotification(dmd);
                }
            }

        }

        /// <summary>
        /// Avviamento
        /// </summary>
        public void StartWatching()
        {

            // comparazione file attuali e fotografia nel momento in cui il servizio si è fermato
            var listSnapShotfiles = GetSnapShotFilesFromRegistry();
            var listActualFiles = GetFiles(param.PathToMonitoring);

            // // notifica i file cancellati
            listSnapShotfiles.ForEach(file =>
            {
                if (!listActualFiles.Contains(file))
                {
                    var fileData = ExctractFileData(file);
                    fileData.FullFileName = file;
                    fileData.FileState = eFileState.Deleted;
                    if (map != null)
                        map.Invoke(fileData);
                    messageDispatch.SendsNotification(fileData);
                }
            });

            // notifica i file aggiunti
            listActualFiles.ForEach(file =>
            {
                if (!listSnapShotfiles.Contains(file))
                {
                    var fileData = ExctractFileData(file);
                    fileData.FullFileName = file;
                    fileData.FileState = eFileState.CreatedOrChanged;
                    if (map != null)
                        map.Invoke(fileData);
                    messageDispatch.SendsNotification(fileData);
                }
            });

            try
            {
            
                // avviamento
                fileWatcher.Path = param.PathToMonitoring;
                messageNotificationThread = new Thread(NotificationMessage);
                messageNotificationThread.Priority = ThreadPriority.BelowNormal;
                lockQueue = new Object();

                isRunning = true;
                fileWatcher.EnableRaisingEvents = true;
                messageNotificationThread.Start();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Errore nel percorso principale da monitorare");
            }
        }

        /// <summary>
        /// fermata
        /// </summary>
        public void StopWatching()
        {
            // ferma il servizio
            fileWatcher.EnableRaisingEvents = false;
            isRunning = false;
            messageNotificationThread.Join();
            // salva la cartella
            WriteSnapShotFilesInRegistry();
        }

        /// <summary>
        /// preleva i dati memorizzati dal registry
        /// </summary>
        /// <returns></returns>
        List<string> GetSnapShotFilesFromRegistry()
        {

            var tot = new List<string>();
            try
            {
                var key = Registry.CurrentUser.OpenSubKey("Software", true);
                if (key.OpenSubKey(REG_KEY_PROGRAM, true) == null)
                    return tot;

                key = key.OpenSubKey(REG_KEY_PROGRAM, true);
                if (key.OpenSubKey(REG_KEY_SNAPSHOT_LIST, true) == null)
                    return tot;

                key = key.OpenSubKey(REG_KEY_SNAPSHOT_LIST, true);
                for (int i = 0; i < Convert.ToInt64(key.GetValue("Number")); i++)
                {
                    tot.Add((string)key.GetValue("File" + i.ToString("0000000000")));
                }
                return tot;
            }
            catch (Exception ex)
            {
                logger.Error(ex, string.Format("Chiave Software {0} REG_KEY_SNAPSHOT_LIST non accessibile", REG_KEY_PROGRAM));
                return tot;
            }

        }

        /// <summary>
        /// memorizza cartella
        /// </summary>
        void WriteSnapShotFilesInRegistry()
        {
            // fotografa la cartella nel registry
            var tot = GetFiles(param.PathToMonitoring).ToArray();

            try
            {
                var key = Registry.CurrentUser.OpenSubKey("Software", true);
                if (key.OpenSubKey(REG_KEY_PROGRAM, true) == null)
                {
                    key.CreateSubKey(REG_KEY_PROGRAM);
                }
                key = key.OpenSubKey(REG_KEY_PROGRAM, true);
                if (key.OpenSubKey(REG_KEY_SNAPSHOT_LIST, true) != null)
                {
                    key.DeleteSubKey(REG_KEY_SNAPSHOT_LIST);
                }
                key.CreateSubKey(REG_KEY_SNAPSHOT_LIST);
                key = key.OpenSubKey(REG_KEY_SNAPSHOT_LIST, true);

                key.SetValue("Number", tot.Length.ToString("0000000000"));
                for (int i = 0; i < tot.Length; i++)
                {
                    key.SetValue("File" + i.ToString("0000000000"), tot[i]);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, string.Format("Chiave Software {0} REG_KEY_SNAPSHOT_LIST non accessibile", REG_KEY_PROGRAM));
            }
        }

        /// <summary>
        /// call back mapping
        /// </summary>
        /// <param name="cbMap"></param>
        public void Map(Action<DocumentMonitorData> cbMap)
        {
            map = cbMap;
        }

        /// <summary>
        /// estrae i dati dal nome del file
        /// </summary>
        /// <param name="FullFileName"></param>
        /// <returns></returns>
        DocumentMonitorData ExctractFileData(string FullFileName)
        {
            var attr = new DocumentMonitorData();
            try
            {
                param.RegExFiles.ForEach(reg =>
                {
                    var matchPeriod = Regex.Match(FullFileName, reg.FullFileNameRegEx, RegexOptions.IgnoreCase);
                    if (matchPeriod.Success)
                    {
                        attr.IndexFile = reg.IndexFile;
                        // dal nome viene estratto il periodo
                        int year = (reg.YearPosition > 0) ? Convert.ToInt32(matchPeriod.Groups[reg.YearPosition].Value) : 0;
                        int month = (reg.MonthPosition > 0) ? Convert.ToInt32(matchPeriod.Groups[reg.MonthPosition].Value) : 0;
                        //var month = (reg.MonthPosition > 0) ? Convert.ToInt32(matchPeriod.Groups[reg.MonthPosition].Value) : 1;
                        // dal nome del file viene estratta la chiave della persona
                        string key = (reg.KeyPosition > 0) ? Convert.ToInt64(matchPeriod.Groups[reg.KeyPosition].Value).ToString(param.FormatDipKey) : param.FormatDipKey;
                        
                        attr.ReadMonth = (reg.MonthPosition > 0);
                        attr.ReadYear = (reg.YearPosition > 0);
                        attr.Value = key;

                        // versione 1.0.0.2 inseriscono i mesi con il 13 per la tredicesima e con il 14 per la quattordicesima
                        if (month > 12)
                        {
                            attr.ExtraMonth = month - 12;
                            // default reiserisce un mese valido
                            month = 12;
                            // prima leggeva il mese dagli attributi del file
                            //month = File.GetLastWriteTime(FullFileName).Month;
                            switch (attr.ExtraMonth)
                            {
                                case 1:
                                    {
                                        // mese tredicesima
                                        if (param.ThirteenthMonth >= 1 && param.ThirteenthMonth <= 12)
                                            month = param.ThirteenthMonth;
                                        break;
                                    }
                                case 2:
                                    {
                                        // mese quattordicesima
                                        if (param.FourteenthMonth >= 1 && param.FourteenthMonth <= 12)
                                            month = param.FourteenthMonth;
                                        break;
                                    }
                            }
                        }
                        if (attr.ReadMonth || attr.ReadYear)
                        {

                            // se il mese non è impostato setta di default gennaio
                            // solo perché non dia errore nella stringa del database localization estrae poi correttamente solo il mese o l'anno
                            if (!attr.ReadMonth)
                                month = 1;

                            // se l'anno non è impostato setta di default l'anno corrente
                            // solo perché non dia errore nella stringa del database localization estrae poi correttamente solo il mese o l'anno
                            if (!attr.ReadYear)
                                year = DateTime.Today.Year;

                            // imposta il periodo relativo al nome del file
                            attr.Period = new DateTime(year, month, 1);
                        }
                        // imposta se il messaggio è pubblico o privato
                        attr.isPublicMessage = reg.isPublicMessage;
                    }
                });
            }
            catch
            {
                return attr;
            }

            return attr;

        }

        /// <summary>
        /// legge i dati nella cartella e verifica se sono validi
        /// </summary>
        /// <param name="Folder"></param>
        /// <returns></returns>
        List<string> GetFiles(string Folder)
        {
            var files = new List<string>();
            try
            {
                var d = new DirectoryInfo(Folder);
                var Files = d.GetFiles("*.*");
                foreach (FileInfo file in Files)
                {
                    var fullfile = Path.Combine(AdjustBackSlash(Folder), file.Name);
                    var attr = ExctractFileData(fullfile);
                    if (attr.IndexFile > 0)
                        files.Add(fullfile);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Errore nella lettura della cartella");
            }
            return files;
        }

        string AdjustBackSlash(string path)
        {
            return path.Replace("\\\\", "\\");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing == true)
            {
                ReleaseManagedResources();
            }
            ReleaseUnmangedResources();
        }

        ~DocumentMonitor()
        {
            Dispose(false);
        }

        void ReleaseManagedResources()
        {
            fileWatcher.Dispose();
        }

        void ReleaseUnmangedResources()
        {
            //watcher.Dispose();
        }

    }
}
