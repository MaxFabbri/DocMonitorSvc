using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PresentDocMonitorSvc.Classes
{
    // poco class contiene il testo del messaggio decodificato
    public class NotifyMessage
    {
        public string Subject { get; set; }
        public string Text { get; set; }
        public string OldText { get; set; }
    }
}
