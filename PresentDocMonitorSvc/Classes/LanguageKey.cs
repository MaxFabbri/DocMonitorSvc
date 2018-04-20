using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PresentDocMonitorSvc
{
    // relativo alla risorsa contenente il messaggio da inserire
    public class LanguageKey
    {
        public string Subject { get; set; }
        public string Text { get; set; }
        public int IndexFile { get; set; }
    }
}
