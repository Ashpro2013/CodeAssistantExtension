using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ashpro.CodeAssistant
{
    public static class AutomationHelper
    {
        public static string FirstLetterToLower(this string sData)
        {
            string sReturn = string.Empty;
            string sChar = sData.Substring(0, 1);
            sChar = sChar.ToLower();
            sData = sData.Remove(0, 1);
            sReturn = sChar + sData;
            return sReturn;
        }
    }
}
