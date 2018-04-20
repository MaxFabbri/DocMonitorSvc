using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentDocMonitorSvc
{
    public class MessageDispatchParameters
    {
        // parametri globali 
        public string MonitoredFolderPath { get; set; }
        // matricola badge ecc...
        public string PersonKeyField { get; set; }
        // web service
        public string UserName { get; set; }
        public string Password { get; set; }
        // se trova già lo stesso documento inserito indica se aggiornarlo comunque o lasciare il precedente
        public bool UpdateMessage { get; set; }
        // ogni quante ore fare refresh login
        public int MaxLoggedHours { get; set; }

        /// <summary>
        /// Elenco chiavi messaggi
        /// </summary>
        public List<LanguageKey> LanguageKey { get; set; }

    }
}
