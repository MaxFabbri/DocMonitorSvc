
using System;

namespace PresentDocMonitorSvc
{
    
    
    public enum eFileState
    {
        CreatedOrChanged,
        Deleted,
        Renamed,
        None
    }


    /// <summary>
    /// classe che riporta i dati relativi al file letto
    /// </summary>
    public class DocumentMonitorData
    {
        /// <summary>
        /// Nome file o cartella coinvolta nell'operazione
        /// </summary>
        public string FullFileName { get; set; }

        /// <summary>
        /// Nome file o cartella vecchia coinvolta nell'operazione
        /// </summary>
        public string OldFullFileName { get; set; }

        /// <summary>
        /// Tipo operazione creato/modificato/cancellato 
        /// </summary>
        public eFileState FileState { get; set; }

        /// <summary>
        /// indice regola trovata
        /// </summary>
        public int IndexFile { get; set; }

        /// <summary>
        /// chiave coinvolta nell'operazione
        /// </summary>
        public string Value{ get; set; }

        /// <summary>
        /// Vecchia chiave coinvolta nell'operazione
        /// </summary>
        public string  OldValue { get; set; }

        /// <summary>
        /// Periodo relativo al file
        /// </summary>
        public DateTime Period { get; set; }

        /// <summary>
        /// inserisce il mese eccedente come tredicesima +1 e quattordicesima +2
        /// </summary>
        public int ExtraMonth{ get; set; }

        /// <summary>
        /// bisogna prelevare il mese
        /// </summary>
        public bool ReadMonth { get; set; }

        /// <summary>
        /// bisogna prelevare l'anno
        /// </summary>
        public bool ReadYear { get; set; }

        /// <summary>
        /// true se il messaggio è pubblico
        /// </summary>
        public bool isPublicMessage;

        public DocumentMonitorData()
        {
            IndexFile = 0;
            Period = DateTime.MinValue;
            FullFileName = string.Empty;
            OldFullFileName = string.Empty;
        }
    }
}
