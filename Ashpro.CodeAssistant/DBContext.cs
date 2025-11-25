using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace Ashpro.CodeAssistant
{
    public class DBContext
    {
        string Connection = string.Empty;
        public DBContext(string sConnection)
        {
            Connection = sConnection;
        }
        public List<Header> Header
        {
            get
            {
                return AshproCommon.GetListMethod<Header>("Select * From TB_Header", Connection);
            }
        }
        public List<Details> Details(string Query)
        {
            return AshproCommon.GetListMethod<Details>(Query, Connection);
        }
        public List<string> TablesWithoutPrimaryKeys
        {
            get
            {
                return AshproCommon.GetStringListMethod("SELECT  t.name AS TableName FROM sys.tables t WHERE NOT EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id = t.object_id  AND i.is_primary_key = 1 ) ORDER BY  t.name;", Connection);
            }
        }
    }

}
