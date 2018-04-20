using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CustomResourceProviders;
using System.Collections;

namespace PresentDocMonitorSvc
{
    class Resources
    {
        private static IDictionary resourceDict;
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static void SetLocation(string path)
        {
            AccessResourceHelper.SetDatabasePath(path);
        }

        public static string GetString(string key, params object[] args)
        {
            try
            {
                // First-time init
                if (resourceDict == null)
                {
                    // se ti dà errore devi eseguirlo come amministratore!
                    resourceDict = AccessResourceHelper.GetResources(Properties.Settings.Default.CultureName);
                }

                var resource = resourceDict[key];
                string resourceString = (resource == null ? "" : (string)resource);
                if (!string.IsNullOrEmpty(resourceString))
                {
                    // Se parametrizzata, sostituisce gli argomenti
                    if (args != null && args.Length > 0)
                        return string.Format(resourceString, args);
                    else
                        return resourceString;
                }
                else
                    return key;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Errore nel recupero delle risorse del linguaggio");
                return "";
            }
        }
    }
}
