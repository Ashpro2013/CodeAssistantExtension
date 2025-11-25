using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ashpro.CodeAssistant
{
    public class Details
    {
        public int Id { get; set; }
        public int FK_HeaderId { get; set; }
        public string ColumnName { get; set; }
        public string SQLDType { get; set; }
        public string CSharpDType { get; set; }
        public int ColumnLength { get; set; }
        public int ColumnSubLength { get; set; }
    }
}
