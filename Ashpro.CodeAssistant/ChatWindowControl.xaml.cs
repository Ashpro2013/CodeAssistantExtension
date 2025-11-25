using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using static System.Net.Mime.MediaTypeNames;

namespace Ashpro.CodeAssistant
{
    public partial class ChatWindowControl : System.Windows.Controls.UserControl
    {
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, bool wParam, int lParam);

        const int WM_SETREDRAW = 11;
        #region Variables
        DBContext  db;
        string SQLdataType;
        string CSharpDatatType;
        string sEntityName;
        public StringBuilder FinalQry;
        string sConnectionString;
        string sConnection;
        List<Details> entDetails = new List<Details>();
        DataTable dtTables = new DataTable();
        List<Header> headers = new List<Header>();
        #endregion

        public ChatWindowControl()
        {
            InitializeComponent();
        }

        #region Form Methods
        private void LoadDataBase()
        {
            try
            {
                using SqlConnection con = new SqlConnection(sConnectionString);
                using SqlCommand cmd = new SqlCommand("SELECT name from sys.databases order by name asc ", con);
                con.Open();
                using SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    cmbDatabases.Items.Add(dr[0].ToString());
                }
            }
            catch (Exception)
            {
                Properties.Settings.Default.ConnectionString = string.Empty;
                Properties.Settings.Default.Save();
            }
        }

        private List<Details> SQLColumnFindMethod(string TableName)
        {
            var detailsList = new List<Details>();
            string Query = $"select * from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = '{TableName}'";
            using (SqlConnection con = new SqlConnection(sConnectionString))
            {
                using (SqlDataAdapter da = new SqlDataAdapter(Query, con))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);
                    foreach (DataRow drw in dt.Rows)
                    {
                        TableColumnGenerateMethod(drw);
                        Details dtls = new Details();
                        dtls.ColumnName = drw["COLUMN_NAME"].ToString();
                        dtls.SQLDType = SQLdataType;
                        dtls.CSharpDType = CSharpDatatType;
                        detailsList.Add(dtls);
                    }
                }
            }
            return detailsList;
        }
        private void GetContext()
        {
            string sTable = sEntityName.ToLower();
            var sql = new StringBuilder();

            sql.AppendLine($"public static List<{sEntityName}> Get{sEntityName}s(string Query, object entity)");
            sql.AppendLine("{");
            sql.AppendLine($"    List<{sEntityName}> {sTable}s = new List<{sEntityName}>();");
            sql.AppendLine("    DataTable dt = AshproCommon.GetDataTable(Query, entity);");
            sql.AppendLine("    foreach (DataRow item in dt.Rows)");
            sql.AppendLine("    {");
            sql.AppendLine($"        var {sTable} = new {sEntityName}");
            sql.AppendLine("        {");

            foreach (Details item in entDetails)
            {
                string assignment = item.CSharpDType switch
                {
                    "int" => $"{item.ColumnName} = item[\"{item.ColumnName}\"].ToString().ToInt32(),",
                    "decimal" => $"{item.ColumnName} = item[\"{item.ColumnName}\"].ToString().ToDecimal(),",
                    "DateTime" => $"{item.ColumnName} = item[\"{item.ColumnName}\"].ToString().toDateTime(),",
                    "bool" => $"{item.ColumnName} = item[\"{item.ColumnName}\"].ToString().ToBool(),",
                    "byte[]" => $"{item.ColumnName} = item[\"{item.ColumnName}\"] == DBNull.Value ? null : (byte[])item[\"{item.ColumnName}\"],",
                    _ => $"{item.ColumnName} = item[\"{item.ColumnName}\"].ToString(),"
                };

                sql.AppendLine($"            {assignment}");
            }

            // Remove trailing comma from the last property (safely)
            int lastCommaIndex = sql.ToString().LastIndexOf(',');
            if (lastCommaIndex != -1)
            {
                sql.Remove(lastCommaIndex, 1);
            }

            sql.AppendLine("        };");
            sql.AppendLine($"        {sTable}s.Add({sTable});");
            sql.AppendLine("    }");
            sql.AppendLine($"    return {sTable}s;");
            sql.AppendLine("}");

            FinalQry.Append(sql.ToString());
        }
        public void CreateEntity()
        {
            var sql = new StringBuilder();
            sql.AppendLine($"public class {sEntityName}");
            sql.AppendLine("{");
            foreach (Details item in entDetails)
            {
                sql.AppendLine($"    public {item.CSharpDType} {item.ColumnName} {{ get; set; }}");
            }
            sql.AppendLine("}");
            FinalQry.Append(sql.ToString());
        }
        public void Knockout()
        {
            var sql = new StringBuilder();
            sql.AppendLine($"vm.{sEntityName} = {{");
            foreach (Details item in entDetails)
            {
                if (item.CSharpDType == "bool")
                    sql.AppendLine($"    {item.ColumnName}: ko.observable(false),");
                else if (item.CSharpDType == "int" || item.CSharpDType == "decimal")
                    sql.AppendLine($"    {item.ColumnName}: ko.observable(0),");
                else if (item.CSharpDType == "string")
                    sql.AppendLine($"    {item.ColumnName}: ko.observable(\"\"),");
                else
                    sql.AppendLine($"    {item.ColumnName}: ko.observable(),");
            }
            sql.AppendLine("}");
            FinalQry.Append(sql.ToString());
        }

        public void CreateMainForm()
        {
            string sFormName = sEntityName + "View";
            StringBuilder sql = new StringBuilder();
            sql.Length = 0;
        }
        public void CreateMasterFormCode()
        {
            string sClass = cmbTables.Text;
            string sObject = sClass.ToLower();
            string sFormName = sClass + "View";
            string sList = "ent" + sClass + "s";
            string sDgv = sObject + "DataGridView";
            StringBuilder sql = new StringBuilder();
            sql.Length = 0;
            sql.Append("public partial class " + sFormName + " : MasterForm" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("#region Constructor" + Environment.NewLine);
            sql.Append("public " + sFormName + "()" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("  InitializeComponent();" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("#endregion" + Environment.NewLine);
            sql.Append(Environment.NewLine);
            sql.Append("#region Private variables" + Environment.NewLine);
            sql.Append("DBContext db = new DBContext();" + Environment.NewLine);
            sql.Append("int? iMasterId;" + Environment.NewLine);
            sql.Append("List<" + sClass + "> " + sList + ";" + Environment.NewLine);
            sql.Append("#endregion" + Environment.NewLine);
            sql.Append(Environment.NewLine);
            sql.Append("#region Events" + Environment.NewLine);
            sql.Append(Environment.NewLine);
            sql.Append("#region Other Events" + Environment.NewLine);
            sql.Append("private void LastControl_KeyDown(object sender, KeyEventArgs e)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("if (e.KeyCode == Keys.Enter)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("btnSave.PerformClick();" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("private void " + sObject + "DataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("if (e.RowIndex >= 0)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append(sClass + " " + sObject + " = (" + sClass + ")" + sObject + "DataGridView.CurrentRow.DataBoundItem;" + Environment.NewLine);
            sql.Append(" if (" + sObject + " != null)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            foreach (Details dr in entDetails)
            {
                string sColumn = dr.ColumnName;
                if (sColumn == "Id")
                {
                    sql.Append("iMasterId = " + sObject + ".Id;" + Environment.NewLine);
                }
                else
                {
                    if (dr.CSharpDType == "DateTime")
                    {
                        sql.Append(sColumn.FirstLetterToLower() + "DateTimePicker.Value = " + sObject + "." + sColumn + ";" + Environment.NewLine);
                    }
                    else if (dr.CSharpDType == "decimal")
                    {
                        sql.Append(sColumn.FirstLetterToLower() + "TextBox.Value = " + sObject + "." + sColumn + ";" + Environment.NewLine);
                    }
                    else if (dr.CSharpDType == "bool")
                    {
                        sql.Append(sColumn.FirstLetterToLower() + "CheckBox.Checked = " + sObject + "." + sColumn + ";" + Environment.NewLine);
                    }
                    else
                    {
                        sql.Append(sColumn.FirstLetterToLower() + "TextBox.Text = " + sObject + "." + sColumn + ";" + Environment.NewLine);
                    }
                }
            }
            sql.Append("btnDelete.Enabled = true;" + Environment.NewLine);
            sql.Append("nameTextBox.Focus();" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("#endregion" + Environment.NewLine);
            sql.Append(Environment.NewLine);
            sql.Append("#region override Events" + Environment.NewLine);
            sql.Append("protected override void MasterForm_Load(object sender, EventArgs e)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("lblMasterTitle.Text = \"New " + sClass + "\";" + Environment.NewLine);
            sql.Append(sList + " = new List<" + sClass + ">();" + Environment.NewLine);
            sql.Append(sList + "= db." + sClass + "s.ToList();" + Environment.NewLine);
            sql.Append(sObject + "DataGridView.AutoGenerateColumns = false;" + Environment.NewLine);
            sql.Append(sObject + "DataGridView.DataSource = " + sList + ";" + Environment.NewLine);
            foreach (Details dr in entDetails)
            {
                if (dr.CSharpDType == "decimal")
                {
                    sql.Append(dr.ColumnName.FirstLetterToLower() + "TextBox.Format = \"N2\";" + Environment.NewLine);
                }
            }
            sql.Append("}" + Environment.NewLine);
            sql.Append("protected override void btnClear_Click(object sender, EventArgs e)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append(" try" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("AniHelper.ClearMethod(this);" + Environment.NewLine);
            sql.Append("btnDelete.Enabled = false;" + Environment.NewLine);
            sql.Append("iMasterId = null;" + Environment.NewLine);
            sql.Append("nameTextBox.Focus();" + Environment.NewLine);
            sql.Append("MasterForm_Load(null, null);" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("catch (Exception ex)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append(" Messages.ErrorMessage(ex.Message);" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("protected override void btnSave_Click(object sender, EventArgs e)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("try" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append(" if (nameTextBox.Text == string.Empty) { Messages.WarningMessage(); }" + Environment.NewLine);
            sql.Append("var " + sObject + " = new " + sClass + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            foreach (Details dr in entDetails)
            {
                string sColumn = dr.ColumnName;
                if (sColumn == "Id")
                {
                    sql.Append("Id = " + "iMasterId," + Environment.NewLine);
                }
                else
                {
                    if (dr.CSharpDType == "DateTime")
                    {
                        sql.Append(sColumn + " = " + sColumn.FirstLetterToLower() + "DateTimePicker.Value," + Environment.NewLine);
                    }
                    else if (dr.CSharpDType == "decimal")
                    {
                        sql.Append(sColumn + " = " + sColumn.FirstLetterToLower() + "TextBox.Value," + Environment.NewLine);
                    }
                    else if (dr.CSharpDType == "bool")
                    {
                        sql.Append(sColumn + " = " + sColumn.FirstLetterToLower() + "CheckBox.Checked," + Environment.NewLine);
                    }
                    else
                    {
                        sql.Append(sColumn + " = " + sColumn.FirstLetterToLower() + "TextBox.Text," + Environment.NewLine);
                    }
                }
            }
            sql.Append("};" + Environment.NewLine);
            sql.Append("if(iMasterId==null)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("if (AshproCommon.InsertToDatabaseObj(" + sObject + ")) { Messages.SavedMessage();btnClear.PerformClick(); }" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("else" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("if (AshproCommon.UpdateToDatabaseObj(" + sObject + ", \"" + sClass + "\", \"Id\", iMasterId.ToInt32())) { Messages.UpdateMessage(); btnClear.PerformClick(); }" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("catch (Exception ex)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("MessageBox.Show(ex.Message);" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("protected override void btnDelete_Click(object sender, EventArgs e)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("if (Messages.DeleteConfirmationMessage())" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("if (AshproCommon.DeleteFromDatabase(\"" + sClass + "\", \"Id\", iMasterId.toInt32())) { btnClear_Click(null, null); }" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("#endregion" + Environment.NewLine);
            sql.Append(Environment.NewLine);
            sql.Append("#endregion" + Environment.NewLine);
            sql.Append(Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            FinalQry.Append(sql.ToString());
        }
        private void GetDataSources()
        {
            txtServer.Text = string.Empty;
            txtServer.Text = Environment.MachineName;
        }
        private void TableColumnGenerateMethod(DataRow drw)
        {
            switch (drw["DATA_TYPE"].ToString())
            {
                case "int":
                    SQLdataType = "int";
                    CSharpDatatType = "int";
                    break;

                case "bigint":
                    SQLdataType = "bigint";
                    CSharpDatatType = "long";
                    break;

                case "smallint":
                    SQLdataType = "smallint";
                    CSharpDatatType = "short";
                    break;

                case "tinyint":
                    SQLdataType = "tinyint";
                    CSharpDatatType = "byte";
                    break;

                case "nvarchar":
                    SQLdataType = drw["CHARACTER_MAXIMUM_LENGTH"].ToString() != "-1"
                        ? $"nvarchar({drw["CHARACTER_MAXIMUM_LENGTH"]})"
                        : "nvarchar(max)";
                    CSharpDatatType = "string";
                    break;

                case "nchar":
                    SQLdataType = $"nchar({drw["CHARACTER_MAXIMUM_LENGTH"]})";
                    CSharpDatatType = "string";
                    break;

                case "varchar":
                    SQLdataType = drw["CHARACTER_MAXIMUM_LENGTH"].ToString() != "-1"
                        ? $"varchar({drw["CHARACTER_MAXIMUM_LENGTH"]})"
                        : "varchar(max)";
                    CSharpDatatType = "string";
                    break;

                case "char":
                    SQLdataType = $"char({drw["CHARACTER_MAXIMUM_LENGTH"]})";
                    CSharpDatatType = "string";
                    break;

                case "decimal":
                case "numeric":
                    SQLdataType = $"decimal({drw["NUMERIC_PRECISION"]},{drw["NUMERIC_SCALE"]})";
                    CSharpDatatType = "decimal?";
                    break;

                case "float":
                    SQLdataType = "float";
                    CSharpDatatType = "double?";
                    break;

                case "money":
                case "smallmoney":
                    SQLdataType = drw["DATA_TYPE"].ToString();
                    CSharpDatatType = "decimal";
                    break;

                case "datetime":
                case "smalldatetime":
                    SQLdataType = drw["DATA_TYPE"].ToString();
                    CSharpDatatType = "DateTime?";
                    break;

                case "date":
                    SQLdataType = "date";
                    CSharpDatatType = "DateOnly?";
                    break;

                case "time":
                    SQLdataType = "time";
                    CSharpDatatType = "TimeSpan?";
                    break;

                case "bit":
                    SQLdataType = "bit";
                    CSharpDatatType = "bool";
                    break;

                case "uniqueidentifier":
                    SQLdataType = "uniqueidentifier";
                    CSharpDatatType = "Guid";
                    break;

                case "varbinary":
                case "binary":
                case "image":
                    SQLdataType = drw["DATA_TYPE"].ToString();
                    CSharpDatatType = "byte[]";
                    break;

                default:
                    SQLdataType = drw["DATA_TYPE"].ToString();
                    CSharpDatatType = "object";
                    break;
            }

        }
        private void ScriptWithTableData(bool isIncludeSchema, bool isIncludeData, bool isIncluedFK)
        {
            StringBuilder sql = new StringBuilder();
            sql.Length = 0;
            sConnectionString = "Data Source=" + txtServer.Text + ";Initial Catalog=" + cmbDatabases.Text + "; Integrated Security=FALSE;User Id=" + "sa" + ";Password=" + "ig79891";
            DataTable dt = new DataTable();
            using (SqlConnection connection = new SqlConnection(sConnectionString))
            {
                connection.Open();
                dt = connection.GetSchema("Tables");
            }
            if (isIncludeSchema)
            {
                foreach (DataRow drw in dt.Rows)
                {
                    TableCreateMethod(sql, drw["TABLE_NAME"].ToString(), isIncluedFK);
                }
            }
            if (isIncludeData)
            {
                foreach (DataRow drw in dt.Rows)
                {
                    DataInsertMethod(sql, drw["TABLE_NAME"].ToString());
                }
            }
            FinalQry.Append(sql.ToString());
        }
        private void DataInsertMethod(StringBuilder sql, string sTable)
        {
            sql.Append("SET IDENTITY_INSERT [" + sTable + "] ON" + Environment.NewLine);
            sql.Append("GO" + Environment.NewLine);
            string Query = "select * from [" + sTable + "]";
            try
            {

                using (SqlConnection con = new SqlConnection(sConnectionString))
                {
                    using (SqlDataAdapter da = new SqlDataAdapter(Query, con))
                    {
                        DataTable data = new DataTable();
                        da.Fill(data);
                        foreach (DataRow dr in data.Rows)
                        {
                            string statement = "Insert [" + sTable + "] (";
                            statement = ColumnAddToInsertMethod(sTable, statement);
                            statement = statement + ") VALUES (";
                            statement = ValueofDatatableAddMethod(dr, statement);
                            sql.Append(statement + ")" + Environment.NewLine);
                            sql.Append("GO" + Environment.NewLine);
                        }
                    }
                }
            }
            catch (Exception)
            {
                string s = Query;
                return;
            }
            sql.Append("SET IDENTITY_INSERT [" + sTable + "] OFF" + Environment.NewLine);
            sql.Append("GO" + Environment.NewLine);
        }
        private string ValueofDatatableAddMethod(DataRow dr, string _statement)
        {
            string statement = _statement;
            try
            {
                for (int i = 0; i < dr.Table.Columns.Count; i++)
                {
                    if (dr[i] == DBNull.Value)
                    {
                        statement = statement + "NULL, ";
                    }
                    else if (dr.Table.Columns[i].DataType == typeof(System.Int32))
                    {
                        statement = statement + Convert.ToInt32(dr[i].ToString()) + ", ";
                    }
                    else if (dr.Table.Columns[i].DataType == typeof(System.String))
                    {
                        statement = statement + "N'" + dr[i].ToString() + "', ";
                    }
                    else if (dr.Table.Columns[i].DataType == typeof(System.Decimal))
                    {
                        statement = statement + Convert.ToDecimal(dr[i].ToString()) + ", ";
                    }
                    else if (dr.Table.Columns[i].DataType == typeof(System.DateTime))
                    {
                        statement = statement + Convert.ToDateTime(dr[i].ToString()) + ", ";
                    }
                    else if (dr.Table.Columns[i].DataType == typeof(System.Boolean))
                    {
                        statement = statement + Convert.ToBoolean(dr[i].ToString()) + ", ";
                    }
                    else
                    {
                        statement = statement + dr[i].ToString() + ", ";
                    }
                }
            }
            catch (Exception ex)
            {

               string s = ex.Message;
            }
            statement = statement.Remove(statement.Length - 2, 1);
            return statement;
        }
        private string ColumnAddToInsertMethod(string sTable, string statement)
        {
            string Query = "select * from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = '" + sTable + "'";
            using (SqlConnection con = new SqlConnection(sConnectionString))
            {
                using (SqlDataAdapter da = new SqlDataAdapter(Query, con))
                {
                    DataTable data = new DataTable();
                    da.Fill(data);
                    foreach (DataRow dr in data.Rows)
                    {
                        statement = statement + "[" + dr["COLUMN_NAME"].ToString() + "],";
                    }
                }
            }
            statement = statement.Remove(statement.Length - 1, 1);
            return statement;
        }
        private void TableCreateMethod(StringBuilder sql, string sTable, bool isIncluedFK)
        {
            sql.Append("Create Table " + sTable + Environment.NewLine);
            sql.Append("(" + Environment.NewLine);
            string Query = "select * from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = '" + sTable + "'";
            using (SqlConnection con = new SqlConnection(sConnectionString))
            {
                using (SqlDataAdapter da = new SqlDataAdapter(Query, con))
                {
                    bool isPKEntered = false;
                    DataTable data = new DataTable();
                    da.Fill(data);
                    foreach (DataRow dr in data.Rows)
                    {
                        TableColumnGenerateMethod(dr);
                        if (!isPKEntered)
                        {
                            sql.Append(dr["COLUMN_NAME"].ToString() + " Int  IDENTITY(1, 1) NOT NULL primary key");
                            sql.Append("," + Environment.NewLine);
                            isPKEntered = true;
                        }
                        else
                        {
                            sql.Append(dr["COLUMN_NAME"].ToString() + " " + SQLdataType + " NULL");
                            sql.Append("," + Environment.NewLine);
                        }
                    }
                    isPKEntered = false;
                    sql.Remove(sql.Length - 3, 1);
                    sql.Append(")");
                    if (isIncluedFK)
                    {
                        foreach (DataRow dr in data.Rows)
                        {
                            TableColumnGenerateMethod(dr);
                            if (dr["COLUMN_NAME"].ToString().Contains("Id"))
                            {
                                if (isPKEntered == false)
                                {
                                    sql.Append(Environment.NewLine);
                                    isPKEntered = true;
                                    continue;
                                }
                                else
                                {
                                    string table = dr["COLUMN_NAME"].ToString();
                                    table = table.Replace("Id", "");
                                    sql.Append("GO" + Environment.NewLine);
                                    sql.Append("Alter Table " + sTable + " ADD CONSTRAINT " + sTable + "_" + table + "_Id" + Environment.NewLine);
                                    sql.Append("FOREIGN KEY (" + dr["COLUMN_NAME"].ToString() + ") REFERENCES " + table + " (Id)" + Environment.NewLine);
                                }
                            }
                        }
                        isPKEntered = false;
                    }
                }
            }
            sql.Append("GO" + Environment.NewLine);
            sql.Append(Environment.NewLine);
        }
        private void DBContexGenerationMethod()
        {
            try
            {
                string sTable = string.Empty;
                StringBuilder sql = new StringBuilder();
                sql.Length = 0;
                sConnectionString = "Data Source=" + txtServer.Text + ";Initial Catalog=" + cmbDatabases.Text + "; Integrated Security=FALSE;User Id=" + "sa" + ";Password=" + "ig79891";
                DataTable dt = new DataTable();
                using (SqlConnection connection = new SqlConnection(sConnectionString))
                {
                    connection.Open();
                    dt = connection.GetSchema("Tables");
                }
                foreach (DataRow drw in dt.Rows)
                {
                    sTable = drw["TABLE_NAME"].ToString();
                    if (sTable.Contains("TB_"))
                    {
                        sTable = sTable.Replace("TB_", "");
                    }
                    sql.Append("public List<" + sTable + "> " + sTable + "s" + Environment.NewLine);
                    sql.Append("{" + Environment.NewLine);
                    sql.Append("get" + Environment.NewLine);
                    sql.Append("{" + Environment.NewLine);
                    sql.Append("return AshproORM.GetList<" + sTable + "> (\"Select * From " + drw["TABLE_NAME"].ToString() + "\");" + Environment.NewLine);
                    sql.Append("}" + Environment.NewLine);
                    sql.Append("}" + Environment.NewLine);
                }
                FinalQry.Append(sql.ToString());
            }
            catch (Exception ex)
            {
                string s = ex.Message;
            }
        }
        private void DBContextSingleMethod(string sTable)
        {
            StringBuilder sql = new StringBuilder();
            sql.Length = 0;
            if (sTable.Contains("TB_"))
            {
                sTable = sTable.Replace("TB_", "");
            }
            sql.Append("public List<" + sTable + "> " + sTable + "s(object entity = null)" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("return ContextHelper.Get" + sTable + "s (\"" + sTable + "\", entity);" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            FinalQry.Append(sql.ToString());
        }
        private void DBContexGenerationMethodWithSP()
        {
            string sTable = string.Empty;
            StringBuilder sql = new StringBuilder();
            sql.Length = 0;
            sConnectionString = "Data Source=" + txtServer.Text + ";Initial Catalog=" + cmbDatabases.Text + "; Integrated Security=FALSE;User Id=" + "sa" + ";Password=" + "ig79891";
            DataTable dt = new DataTable();
            using (SqlConnection connection = new SqlConnection(sConnectionString))
            {
                connection.Open();
                dt = connection.GetSchema("Tables");
            }
            foreach (DataRow drw in dt.Rows)
            {
                sTable = drw["TABLE_NAME"].ToString();
                if (sTable.Contains("TB_"))
                {
                    sTable = sTable.Replace("TB_", "");
                }
                DBContextSingleMethod(sTable);
            }
            FinalQry.Append(sql.ToString());
        }
        private void DBContextSingleWithSPMethod()
        {
            string sTable = string.Empty;
            StringBuilder sql = new StringBuilder();
            sql.Length = 0;
            sTable = cmbTables.Text;
            if (sTable.Contains("TB_"))
            {
                sTable = sTable.Replace("TB_", "");
            }
            sql.Append("public List<" + sTable + "> " + sTable + "s" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("get" + Environment.NewLine);
            sql.Append("{" + Environment.NewLine);
            sql.Append("return AshproORM.GetList<" + sTable + "> (\"spGet" + cmbTables.Text + "\");" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            sql.Append("}" + Environment.NewLine);
            FinalQry.Append(sql.ToString());
        }
        private void MultiEntityCreationMethod()
        {
            string sTable = string.Empty;
            StringBuilder sql = new StringBuilder();
            sql.Length = 0;
            sConnectionString = "Data Source=" + txtServer.Text + ";Initial Catalog=" + cmbDatabases.Text + "; Integrated Security=FALSE;User Id=" + "sa" + ";Password=" + "ig79891";
            DataTable dt = new DataTable();
            using (SqlConnection connection = new SqlConnection(sConnectionString))
            {
                connection.Open();
                dt = connection.GetSchema("Tables");
            }
            foreach (DataRow drw in dt.Rows)
            {
                sTable = drw["TABLE_NAME"].ToString();
                if (sTable.Contains("TB_"))
                {
                    sTable = sTable.Replace("TB_", "");
                }
                sql.Append("public class " + sTable + Environment.NewLine);
                sql.Append("{" + Environment.NewLine);
                ClassPropertiesGenerateMethod(sTable, sql);
                sql.Append("}" + Environment.NewLine);
            }
            FinalQry.Append(sql.ToString());
        }
        private void ClassPropertiesGenerateMethod(string sTable, StringBuilder sql)
        {
            string sColumn = string.Empty;
            string Query = "select * from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = '" + sTable + "'";
            using (SqlConnection con = new SqlConnection(sConnectionString))
            {
                using (SqlDataAdapter da = new SqlDataAdapter(Query, con))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);
                    foreach (DataRow drw in dt.Rows)
                    {
                        sColumn = drw["COLUMN_NAME"].ToString();
                        TableColumnGenerateMethod(drw);
                        if (sColumn == "Id" && CSharpDatatType == "int")
                        {
                            sql.Append("public " + CSharpDatatType + "? " + sColumn + " { get; set; }" + Environment.NewLine);
                        }
                        else
                        {
                            sql.Append("public " + CSharpDatatType + " " + sColumn + " { get; set; }" + Environment.NewLine);
                        }
                    }
                }
            }
        }
        private void LoadSubTables()
        {
            dgSubItems.AutoGenerateColumns = false;
            dgSubItems.ItemsSource = null;
            dgSubItems.ItemsSource = headers;
        }

        #endregion

        private void frmMain_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ConnectionString))
            {
                txtServer.Text = Properties.Settings.Default.Server;
                sConnectionString = Properties.Settings.Default.ConnectionString;
                LoadDataBase();
                cmbDatabases.Text = Properties.Settings.Default.Database;
            }
            else
            {
                var name = Environment.MachineName;
                if (name.Length == 3) { name = name + "\\SQLEXPRESS"; }
                txtServer.Text = name;
            }
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
           
            dgSubItems.Items.Refresh();
        }
        private void cmbVariables_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string typedText = cmbVariables.Text;
                e.Handled = true;
            }
        }

        private void OnServerClick(object sender, RoutedEventArgs e)
        {
            if (txtServer.Text != String.Empty)
            {
                sConnectionString = $@"Data Source={txtServer.Text}; Initial Catalog=master;User ID=sa;Password=ig79891; Integrated Security=False; MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;Persist Security Info=False;";
            }
            else
            {
                sConnectionString = $@"Data Source={Environment.MachineName}; Initial Catalog=master;User ID=sa;Password=ig79891; Integrated Security=False; MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;Persist Security Info=False;";
            }
            Properties.Settings.Default.ConnectionString = sConnectionString;
            Properties.Settings.Default.Server = txtServer.Text;
            Properties.Settings.Default.Save();
            LoadDataBase();
        }

        private void OnDatabaseClick(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Database = cmbDatabases.Text;
            Properties.Settings.Default.Save();
        }

        private void OnTableClick(object sender, RoutedEventArgs e)
        {
            headers.Add(new Header { Id = headers.Count + 1, Name = cmbTables.Text });
            LoadSubTables();
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            if (cmbVariables.Text == string.Empty) { return; }
            string sText = cmbVariables.Text.Substring(0, 1);
            string Text = cmbVariables.Text.Remove(0, 1);
            switch (sText)
            {
                case "i":
                    SQLdataType = "int";
                    CSharpDatatType = "int";
                    break;
                case "n":
                    if (txtSize.Text != string.Empty)
                    {
                        SQLdataType = "nvarchar(" + txtSize.Text.Trim() + ")";
                    }
                    else
                    {
                        SQLdataType = "nvarchar(100)";
                    }

                    CSharpDatatType = "string";
                    break;
                case "d":
                    SQLdataType = "decimal(24,4)";
                    CSharpDatatType = "decimal";
                    break;
                case "t":
                    SQLdataType = "datetime";
                    CSharpDatatType = "DateTime";
                    break;
                case "b":
                    SQLdataType = "bit";
                    CSharpDatatType = "bool";
                    break;
                case "m":
                    SQLdataType = "image";
                    CSharpDatatType = "byte[]";
                    break;
                case "f":
                    SQLdataType = "int";
                    CSharpDatatType = "int";
                    Text = "FK_" + Text;
                    break;
                case "p":
                    SQLdataType = "int";
                    CSharpDatatType = "int";
                    break;
                default:
                    return;
            }
            Details dtls = new Details();
            dtls.ColumnName = Text;
            dtls.SQLDType = SQLdataType;
            dtls.CSharpDType = CSharpDatatType;
            entDetails.Add(dtls);
            dgvVariables.AutoGenerateColumns = false;
            dgvVariables.ItemsSource = null;
            dgvVariables.ItemsSource = entDetails;
            cmbVariables.Text = string.Empty;
            txtSize.Text = string.Empty;
            cmbVariables.Focus();
        }

        private void OnGenerateClick(object sender, RoutedEventArgs e)
        {
            if (cmbTables.Text.Contains("TB_"))
            {
                sEntityName = cmbTables.Text.Trim().Remove(0, 3);
            }
            else
            {
                sEntityName = cmbTables.Text.Trim();
            }
            FinalQry = new StringBuilder();
            FinalQry.Length = 0;
            switch (cmbType.Text)
            {
                case "Entity Creation":
                    CreateEntity();
                    break;
                case "ContextHelper":
                    GetContext();
                    break;
                case "Script Generation":
                    ScriptWithTableData(true, true, false);
                    break;
                case "Script Schema":
                    ScriptWithTableData(true, false, false);
                    break;
                case "Script Generation with FK":
                    ScriptWithTableData(true, true, true);
                    break;
                case "Script Schema with FK":
                    ScriptWithTableData(true, false, true);
                    break;
                case "Script Table Data":
                    ScriptWithTableData(false, true, false);
                    break;
                case "DBContext":
                    DBContexGenerationMethod();
                    break;
                case "DBContext With SP":
                    DBContexGenerationMethodWithSP();
                    break;
                case "Multi Entity Creation":
                    MultiEntityCreationMethod();
                    break;
                case "DBContext Single":
                    DBContextSingleMethod(cmbTables.Text);
                    break;
                case "DBContext Single With SP":
                    DBContextSingleWithSPMethod();
                    break;
                case "Master Form Code":
                    CreateMasterFormCode();
                    break;
                default:
                    break;
            }
            txtGeneratedCode.Text = FinalQry.ToString();
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Clipboard.SetText(txtGeneratedCode.Text);
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            try
            {
                txtGeneratedCode.Text = string.Empty;
                cmbTables.Text = string.Empty;
                cmbVariables.Text = string.Empty;
                txtSize.Text = string.Empty;
                cmbType.Text = string.Empty;
                dgvVariables.Items.Clear();
                cmbTables.Focus();
            }
            catch (Exception ex)
            {

                string s  = ex.Message;
            }
        }

        private void DatabaseSelected(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (txtSize.Text == String.Empty)
                {
                    if (txtServer.Text != String.Empty)
                    {
                        sConnectionString = $@"Data Source={txtServer.Text}; Initial Catalog={cmbDatabases.SelectedValue.ToString()};User ID=sa;Password=ig79891; Integrated Security=False; MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;Persist Security Info=False;";
                    }
                    else
                    {
                        sConnectionString = $@"Data Source={Environment.MachineName}; Initial Catalog={cmbDatabases.SelectedValue.ToString()};User ID=sa;Password=ig79891; Integrated Security=False; MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;Persist Security Info=False;";
                    }
                }
                using (SqlConnection connection = new SqlConnection(sConnectionString))
                {
                    connection.Open();
                    dtTables = connection.GetSchema("Tables");
                }

                dtTables.DefaultView.Sort = "TABLE_NAME";

                cmbTables.ItemsSource = dtTables.DefaultView;
                cmbTables.DisplayMemberPath = "TABLE_NAME";
                cmbTables.SelectedValuePath = "TABLE_NAME";


            }
            catch (Exception ex)
            {
               string s = ex.Message;
            }

        }

        private void TableSelected(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTables.SelectedValue != null)
            {
                try
                {

                    string Query = @"
SELECT 
    COLUMN_NAME AS ColumnName, 
    DATA_TYPE AS SQLDType, 
    CASE 
        WHEN CHARACTER_MAXIMUM_LENGTH IS NULL THEN 0 
        ELSE CAST(CHARACTER_MAXIMUM_LENGTH AS INT) 
    END AS ColumnLength,
    CASE 
        WHEN NUMERIC_SCALE IS NULL THEN 0 
        ELSE CAST(NUMERIC_SCALE AS INT) 
    END AS ColumnSubLength,
    IS_NULLABLE AS IsNullable,
    CASE 
        WHEN DATA_TYPE = 'int' THEN 
            CASE WHEN IS_NULLABLE = 'YES' THEN 'int?' ELSE 'int' END
        WHEN DATA_TYPE = 'decimal' THEN 
            CASE WHEN IS_NULLABLE = 'YES' THEN 'decimal?' ELSE 'decimal' END
        WHEN DATA_TYPE = 'bit' THEN 
            CASE WHEN IS_NULLABLE = 'YES' THEN 'bool?' ELSE 'bool' END
        WHEN DATA_TYPE IN ('datetime', 'date', 'smalldatetime', 'datetime2') THEN 
            CASE WHEN IS_NULLABLE = 'YES' THEN 'DateTime?' ELSE 'DateTime' END
        WHEN DATA_TYPE = 'image' THEN 
            'byte[]'
        ELSE 
            CASE WHEN IS_NULLABLE = 'YES' THEN 'string?' ELSE 'string' END
    END AS CSharpDType
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = '" + cmbTables.SelectedValue.ToString() + @"'
ORDER BY ORDINAL_POSITION;
";

                    db = new DBContext(sConnectionString);
                    entDetails = new List<Details>();
                    entDetails = db.Details(Query);
                    cmbVariables.ItemsSource = entDetails;
                    cmbVariables.DisplayMemberPath = "ColumnName";
                    cmbVariables.SelectedValuePath = "ColumnName";
                    dgvVariables.AutoGenerateColumns = false;
                    dgvVariables.ItemsSource = null;
                    dgvVariables.ItemsSource = entDetails;
                }
                catch (Exception ex)
                {

                    string s = ex.Message;
                }
            }
            
        }
        private void cmbVariables_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnAddClick(null, null);
            }
        }

        private async void OnApply(object sender, RoutedEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DTE2 dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

            Project project = dte?.ActiveDocument?.ProjectItem?.ContainingProject
                              ?? dte?.Solution?.Projects?.Item(1);

            if (project == null)
            {
                System.Windows.Forms.MessageBox.Show("No active project found.");
                return;
            }

            // Class template
            string classCode = @"
using System;

namespace " + project.Name + @"
{
    "+ txtGeneratedCode.Text + @"
}";
            string projectDir = Path.GetDirectoryName(project.FullName);
            string filePath = Path.Combine(projectDir, $"{cmbTables.Text}.cs");

            File.WriteAllText(filePath, classCode);

            project.ProjectItems.AddFromFile(filePath);
        }
    }
}
