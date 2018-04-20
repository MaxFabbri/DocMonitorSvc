using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentDocMonitorSvc
{
    // proprietà relative al singolo documento
    public class RegExFile
    {
        // espressione regolare 
        public string FullFileNameRegEx { get; set; }
        public string FileName { get; set; }
        public int IndexFile { get; set; }
        public int YearPosition { get; set; }
        public int MonthPosition { get; set; }
        public int KeyPosition { get; set; }
        /// <summary>
        /// versione 1.1 messaggio pubblico con relativa stringa
        /// </summary>
        public bool isPublicMessage { get; set; }
        
    }
}
