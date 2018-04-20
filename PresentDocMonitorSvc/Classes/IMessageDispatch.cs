using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentDocMonitorSvc
{
    public interface IMessageDispatch
    {
        void SendsNotification(DocumentMonitorData Message);
    }
}
