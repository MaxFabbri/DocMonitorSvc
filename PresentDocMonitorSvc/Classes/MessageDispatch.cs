using Present.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CustomResourceProviders;
using System.Data;
using System.IO;
using PresentDocMonitorSvc.Classes;

namespace PresentDocMonitorSvc
{
    public class MessageDispatch : IMessageDispatch
    {

        private PresentDataContext context;
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private DateTime LoggedTime;

        // classe contenente i parametri
        readonly MessageDispatchParameters param = new MessageDispatchParameters();

        /// <summary>
        /// settaggio parametri
        /// </summary>
        /// <param name="Parameters"></param>
        /// <returns></returns>
        public MessageDispatch Settings(Action<MessageDispatchParameters> Parameters)
        {
            param.LanguageKey = new List<LanguageKey>();

            // passa in lambda i parametri tramite la classe
            Parameters(param);

            try
            {
                var d = Directory.GetParent(param.MonitoredFolderPath);
                //var pwPath = IISUtils.FindWebAppPathByVirtualFolderName("Present.Web");
                // imposta il percorso del database localization
                Resources.SetLocation(Path.Combine(d.FullName, @"App_data\Localization.mdb"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Errore nell'individuazione delle risorse " + Path.Combine(param.MonitoredFolderPath, @"App_data\Localization.mdb"));
            }

            CheckLoggedTime(true);
                
            return this;
        }

        /// <summary>
        /// spedisce la notifica
        /// </summary>
        /// <param name="Message"></param>
        public void SendsNotification(DocumentMonitorData Message)
        {

            // nessuna regola matcha esce
            if (Message.IndexFile == 0)
                return;

            // effettua il refresh del login di present dal PRP
            CheckLoggedTime();
            
		    if (context == null)
		    {
		        logger.Warn("PresentDataContext is null SendsNotification failed!");
		        return;
		    }

            // imposta il messaggio da notificare nel documento
            var notifyMessage = ResourceDecodeMessage(Message);

            // il messaggio per essere valido deve avere il testo e il soggetto
            if (notifyMessage.Subject == string.Empty || notifyMessage.Text == string.Empty) 
                return;

            switch (Message.FileState)
            {
                case eFileState.CreatedOrChanged:

                    var log = string.Empty;
                    var messageFound=false;

                    if (Message.isPublicMessage)
                    {
                        // la notifica è pubblica per cui il targetAccoutID è 0
                        // cancella eventuale notifica precedente se UpdateMessage
                        messageFound = RemoveMessage(0, param.UpdateMessage, notifyMessage);
                        // se trova un messaggio precedente e non è impostato il flag UpdateMessage NON inserisce la nuova notifica 
                        if ((!messageFound && !param.UpdateMessage) || param.UpdateMessage)
                        {
                            PushMessage(0, notifyMessage);
                            log = string.Format("inserita notifica pubblica {0}", Message.FullFileName);
                        }
                        else
                        {
                            log = string.Format("notifica globale pubblica {0}", Message.FullFileName);
                        }
                        logger.Info(log);
                    }
                    else
                    {
                        // piu utenti web possono avere associato un utente present effettua le notifiche a tutti gli utenti web 
                        GetAccountID(Message.Value).ForEach(targetAccountId =>
                        {
                            // cancella eventuale notifica precedente se UpdateMessage
                            messageFound = RemoveMessage(targetAccountId, param.UpdateMessage, notifyMessage);
                            // se trova un messaggio precedente e non è impostato il flag UpdateMessage NON inserisce la nuova notifica 
                            if ((!messageFound && !param.UpdateMessage) || param.UpdateMessage)
                            {
                                PushMessage(targetAccountId, notifyMessage);
                                log = string.Format("inserita notifica {0} {1} {2} accountID {3}", Message.FullFileName, param.PersonKeyField, Message.Value, targetAccountId);
                            }
                            else
                            {
                                log = string.Format("notifica trovata {0} {1} {2} accountID {3}", Message.FullFileName, param.PersonKeyField, Message.Value, targetAccountId);
                            }
                            logger.Info(log);
                        });
                    }
                  
                    break;

                case eFileState.Deleted:

                    if (Message.isPublicMessage)
                    {
                        // la notifica è pubblica per cui il targetAccoutID è 0
                        RemoveMessage(0, true, notifyMessage);
                        log = string.Format("rimossa notifica pubblica {0}", Message.FullFileName);
                        logger.Info(log);
                    }
                    else
                    {
                        // piu utenti web possono avere associato un utente present effettua le notifiche a tutti gli utenti web 
                        GetAccountID(Message.Value).ForEach(targetAccountId =>
                        {
                            // se non c'è + il documento lo cancella 
                            RemoveMessage(targetAccountId, true, notifyMessage);
                            log = string.Format("rimossa notifica {0} {1} {2} accountID {3}", Message.FullFileName, param.PersonKeyField, Message.Value, targetAccountId);
                            logger.Info(log);
                        });
                    }

                    break;

                case eFileState.Renamed:

                    var oldLog = string.Empty;

                    if (Message.isPublicMessage)
                    {
                        // la notifica è pubblica per cui il targetAccoutID è 0
                        oldLog = string.Format("rinominata notifica pubblica {0} in {1}", Message.OldFullFileName, Message.FullFileName);
                        logger.Info(oldLog);

                        // cancella la vecchia notifica rinominata
                        RemoveMessage(0, true, notifyMessage);
                        oldLog = string.Format("rimossa notifica pubblica {0}", Message.OldFullFileName);
                        logger.Info(oldLog);

                        // cancella eventuale notifica rinominata precedente se UpdateMessage
                        messageFound = RemoveMessage(0, param.UpdateMessage, notifyMessage);
                        // se trova un messaggio precedente e non è impostato il flag UpdateMessage NON inserisce la nuova notifica 
                        if ((!messageFound && !param.UpdateMessage) || param.UpdateMessage)
                        {
                            PushMessage(0, notifyMessage);
                            log = string.Format("inserita notifica pubblica {0}", Message.FullFileName);
                            logger.Info(log);
                        }
                        else
                        {
                            log = string.Format("notifica trovata pubblica {0}", Message.FullFileName);
                        }
                    }
                    else
                    {
                        // notifica privata
                        oldLog = string.Format("rinominata notifica {0} in {1} {2} {3}", Message.OldFullFileName, Message.FullFileName, param.PersonKeyField, Message.OldValue);
                        logger.Info(oldLog);

                        // piu utenti web possono avere associato un utente present effettua le notifiche a tutti gli utenti web 
                        GetAccountID(Message.OldValue).ForEach(oldTargetAccountId =>
                        {
                            // se non c'è + il documento lo cancella 
                            RemoveMessage(oldTargetAccountId, true, notifyMessage);
                            oldLog = string.Format("rimossa notifica {0} {1} {2} accountID {3}", Message.OldFullFileName, param.PersonKeyField, Message.OldValue, oldTargetAccountId);
                            logger.Info(oldLog);
                        });

                        // piu utenti web possono avere associato un utente present effettua le notifiche a tutti gli utenti web 
                        GetAccountID(Message.Value).ForEach(targetAccountId =>
                        {
                            // cancella eventuale notifica precedente se UpdateMessage
                            messageFound = RemoveMessage(targetAccountId, param.UpdateMessage, notifyMessage);
                            // se trova un messaggio precedente e non è impostato il flag UpdateMessage NON inserisce la nuova notifica 
                            if ((!messageFound && !param.UpdateMessage) || param.UpdateMessage)
                            {
                                PushMessage(targetAccountId, notifyMessage);
                                log = string.Format("inserita notifica {0} {1} {2} accountID {3}", Message.FullFileName, param.PersonKeyField, Message.Value, targetAccountId);
                                logger.Info(log);
                            }
                            else
                            {
                                log = string.Format("notifica trovata {0} {1} {2} accountID {3}", Message.FullFileName, param.PersonKeyField, Message.Value, targetAccountId);
                            }
                        });
                    }

                    break;

                case eFileState.None:
                    break;
                default:
                    break;
            }

        }
        
        /// <summary>
        /// ritorna gli account associati alla matricola
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        private List<int> GetAccountID(string Value)
        {

            List<int> AccountIDs= new List<int>();
            int pageIndex = 0;
            int pageCount = 0;
            int totalCount = 0;

            try
            {

                // ricerca la chiave ma solo monoDB
                //var queryKey = string.Format("SELECT a.AccountID FROM Accounts a JOIN Dipendenti d ON a.DipID=d.DipID WHERE d.{0}='{1}'", param.CampoAnagraficoChiaveDipendente, Message.NewValue);

                var persons = context.GetPagedPersonsList(
                    String.Format("{0}='{1}'", param.PersonKeyField, Value), "", false, false, DateInterval.Unbounded, null, 0, ref pageIndex, ref pageCount, ref totalCount);

                if (persons.Count == 1)
                {
                    // piu utenti web possono avere associato un utente present effettua le notifiche a tutti gli utenti web 
                    var accounts = context.GetCachedAccounts().Where(a => a.PersID == persons[0].ID);

                    if (accounts != null)
                    {
                        foreach (var account in accounts)
                            AccountIDs.Add(account.ID);
                    }
                    else
                    {
                        logger.Info(string.Format("nessun account associato alla {0} {1}", param.PersonKeyField, Value));
                    }

                    if (AccountIDs.Count == 0)
                    {
                        logger.Info(string.Format("nessun account associato alla {0} {1}", param.PersonKeyField, Value));
                    }

                    // fallisce se + di 1 utente web ha assegnato lo stesso utente present!!
                    //var account = accounts.SingleOrDefault(a => a.PersID == persons[0].ID);
                }
                else if (persons.Count > 1)
                {
                    logger.Info(string.Format("{0} con valore {1} trovate diverse corrispondenze!", param.PersonKeyField, Value));
                }
                else
                {
                    logger.Info(string.Format("{0} con valore {1} non trovata!", param.PersonKeyField, Value));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, string.Format("Errore nella ricerca accountID {0} {1}", param.PersonKeyField, Value));
            }

            return AccountIDs;
        }

        /// <summary>
        /// Rimuove la notifica globale o personale
        /// </summary>
        /// <param name="targetAccountId"></param>
        /// <param name="remove"></param>
        /// <param name="notifyMessage"></param>
        /// <returns></returns>
        private bool RemoveMessage(int targetAccountId, bool remove, NotifyMessage notifyMessage)
        {
            try
            {
                // query su notifica personale o globale
                string queryKey = string.Empty;
                if ((targetAccountId == 0) && (notifyMessage.Text != string.Empty) && (notifyMessage.Subject!= string.Empty))
                {
                    queryKey = string.Format("SELECT ID FROM NoticeMessages WHERE ((Subject='{0}') AND (Text='{1}') AND (Status>0) AND (Private=0))", notifyMessage.Subject, notifyMessage.Text);
                }
                else if ((targetAccountId > 0) && (notifyMessage.Subject != string.Empty))
                {
                    queryKey = string.Format("SELECT ID FROM NoticeMessages WHERE ((TargetAccountID={0}) AND (Subject='{1}') AND (Status>0))", targetAccountId, notifyMessage.Subject);
                }
                var result = context.RawData.QueryDatabase(queryKey);
                for (int i = 0; i < result.Rows.Count; i++)
                {
                    if(remove)
                        // nasconde la notifica
                        context.SetNoticeMessageAsDeleted(result.Rows[i].Field<int>("ID"));
                }
                return (result.Rows.Count>0);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Rimozione notifica fallita!");
                return false;
            }
        }

        /// <summary>
        /// Insert Notify
        /// </summary>
        /// <param name="targetAccountID"></param>
        /// <param name="notifyMessage"></param>
        void PushMessage(int targetAccountID, NotifyMessage notifyMessage)
        {
            try
            {
                // Per creare un nuovo NoticeMessage privato
                var message = this.context.GetNoticeMessage(-1);
                // se TargetAccountID = 0 trattasi di messaggio pubblico 
                // e occorre impostare private e l'oggetto account
                if (targetAccountID > 0)
                {
                    message.IsPrivate = true;
                    // Imposta l'account destinazione
                    message.TargetAccount = new Account(targetAccountID);
                    // Poi quello mittente, che corrisponde a quello loggato nel context
                    message.SenderAccountID = context.CurrentPresentAccount.ID;
                }
                else
                {
                    message.TargetAccount = new Account(0);
                    // Poi quello mittente, che corrisponde a quello loggato nel context
                    message.SenderAccountID = 0;
                }

                // Idem per l'account di creazione
                message.CreatorAccount = new Account(this.context.CurrentPresentAccount.ID);

                // È una notifica
                message.Category = NoticeMessageCategory.Notification;
                // Setta le date di creazione e ultima modifica
                message.CreationDate = DateTime.Now;
                message.LastModifyDate = DateTime.Now;
                // Intervallo di visualizzazione per il destinatario: 20 anni da oggi (direi che può bastare...)
                message.StartDateTime = DateTime.Now;
                message.EndDateTime = DateTime.Now.AddYears(20);
                // Il messaggio ha il contrassegno di "letto"
                message.ReturnReceipt = true;
                // Esempio di recupero stringa multilingua semplice. Formato key = [ResourceType.]ResourceKey
                message.Subject = notifyMessage.Subject;
                // Esempio di recupero stringa multilingua con parametri.
                message.Text = notifyMessage.Text;
                // Lo pubblica immediatamente
                message.Status = NoticeMessageStatus.Published;
                context.SubmitNoticeMessage(message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Inserimento notifica fallita!");
            }

        }

        /// <summary>
        /// decodifica (dalle risorse) il soggetto ed il testo del messaggio 
        /// </summary>
        /// <param name="Message"></param>
        /// <param name="subject"></param>
        /// <param name="text"></param>
        /// <param name="oldText"></param>
        private NotifyMessage ResourceDecodeMessage(DocumentMonitorData Message)
        {

            var notifyMessage = new NotifyMessage();

            // messaggio pubblico / privato da decodificare in base alla lingua e opzionalmente alla data
            try
            {

                var paramLanguage = param.LanguageKey.FirstOrDefault(Index => Index.IndexFile == Message.IndexFile);

                string yearMonth = "";

                if (Message.ReadYear)
                    yearMonth = " {1:yyyy}";
                if (Message.ReadMonth)
                    yearMonth += " {1:MM}";

                // versione 1.0.0.2 tredicesima e/o quattordicesima inserisce la stringa aggiuntiva EXTRA
                var extra = "";
                switch (Message.ExtraMonth)
                {
                    case 1:
                        {
                            extra = " tredicesima";
                            break;
                        }
                    case 2:
                        {
                            extra = " quattordicesima";
                            break;
                        }
                }

                if (Message.ReadYear || Message.ReadMonth) 
                {
                    // messaggio con anno e mese
                    notifyMessage.Subject = string.Format("{0}" + extra + yearMonth, Resources.GetString(paramLanguage.Subject), Message.Period);
                    notifyMessage.Text = Resources.GetString(paramLanguage.Text, Message.FullFileName, Message.Period);
                    if (Message.OldFullFileName != "")
                    {
                        notifyMessage.OldText= Resources.GetString(paramLanguage.Text, Message.OldFullFileName, Message.Period);

                    }
                }
                else
                {
                    // messaggio senza periodo o messaggio globale
                    // qui il programma è x forza un po' inchiodato perché nel segnaposto {1} abbiamo il nome del file
                    notifyMessage.Subject = Resources.GetString(paramLanguage.Subject);
                    notifyMessage.Text = Resources.GetString(paramLanguage.Text, Message.FullFileName, GetFileName(Message.FullFileName));
                    if (Message.OldFullFileName != "")
                    {
                        notifyMessage.OldText = Resources.GetString(paramLanguage.Text, Message.OldFullFileName, GetFileName(Message.OldFullFileName));
                    }
                }

                return notifyMessage;
            }
                
            catch (Exception ex)
            {
                logger.Error(ex, string.Format("Errori nella decodifica del soggetto e/o del testo del messaggio da inserire"));
                return notifyMessage;
            }
        }

        /// <summary>
        /// se necessario effettua il refresh del login
        /// </summary>
        /// <param name="First"></param>
        private void CheckLoggedTime(bool First=false)
        {
            try
            {

                if (DateTime.Now.Subtract(LoggedTime).TotalHours >= param.MaxLoggedHours || !PresentDataContextIsAlive())
                {
                    context = null;
                    context = new PresentDataContext(param.UserName, param.Password, Environment.MachineName, true);
                    LoggedTime = DateTime.Now;
                    if (First)
                    {
                        logger.Info("PresentDataContext avviato");
                    }
                    else
                    {
                        logger.Info("PresentDataContext resettato e riavviato");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Errore durante l'avvio di PresentDataContext");
            }

        }

        /// <summary>
        /// verifica che presentdatacontext sia attivo
        /// </summary>
        /// <returns></returns>
        private bool PresentDataContextIsAlive()
        {
            try 
            {
                // verifica se è ancora vivo...
                if (context.RawData.QueryDatabase("select DipID from Dipendenti where dipid=-44").Rows.Count == 0)
                {
                    return true;
                }
                else
                {
                    // C'è l'ID -44? Strano... :) ma comunque ha risposto...
                    return true;
                }
            }
            catch (Exception)
            {
                context = null;
                return false;
            }
        
        }

        /// <summary>
        /// Estrae il nome del file dal percorso relativo interno
        /// </summary>
        /// <param name="fullFileName">percorso e nome del file completo</param>
        /// <returns>nome file</returns>
        private string GetFileName(string fullFileName) 
        {
            // estrae il nome del file dal percorso relativo 
            string fileName = fullFileName;
            int startFileName = fullFileName.LastIndexOf('\\') + 1;
            if (startFileName > 0)
            {
                fileName = fullFileName.Substring(startFileName, fullFileName.Length - startFileName);
            }
            return fileName;
        }

    }

}
