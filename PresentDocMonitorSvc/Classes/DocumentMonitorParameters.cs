using System.Collections;
using System.Collections.Generic;

namespace PresentDocMonitorSvc
{
    public class DocumentMonitorParameters
    {
        /// <summary>
        /// Percorso da controllare
        /// </summary>
        public string PathToMonitoring { get; set; }

        /// <summary>
        /// Formattazione campo chiave
        /// </summary>
        public string FormatDipKey { get; set; }

        /// <summary>
        /// Mese di retribuzione tredicesima
        /// </summary>
        public int ThirteenthMonth { get; set; }

        /// <summary>
        /// Mese di retribuzione quattordicesima
        /// </summary>
        public int FourteenthMonth { get; set; }

        /// <summary>
        /// elenco regole
        /// </summary>
        public List<RegExFile> RegExFiles { get; set; }


    }
}
