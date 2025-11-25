using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ashpro.CodeAssistant
{
    public class AshproCommon
    {
        public static DataTable GetDataTable(string query, string connectionString)
        {
            DataTable dt = new DataTable();
            using (SqlConnection con = new SqlConnection(connectionString))
            {
                using (SqlDataAdapter da = new SqlDataAdapter(query, con))
                {
                    da.Fill(dt);
                }
            }
            return dt;
        }
        public static List<T> GetListMethod<T>(string Query, string sConnection)
        {
            List<T> dsList = new List<T>();
            DataTable dt = new DataTable();
            dt = GetDataTable(Query, sConnection);
            dsList = ConvertDataTable<T>(dt);
            return dsList;

        }
        public static List<T> ConvertDataTable<T>(DataTable dt)
        {
            List<T> data = new List<T>();
            foreach (DataRow row in dt.Rows)
            {
                T item = GetItem<T>(row);

                data.Add(item);
            }
            return data;
        }
        public static T GetItem<T>(DataRow dr)
        {
            Type temp = typeof(T);
            T obj = Activator.CreateInstance<T>();

            foreach (DataColumn column in dr.Table.Columns)
            {
                foreach (PropertyInfo pro in temp.GetProperties())
                {
                    if (pro.Name == column.ColumnName)

                        if (dr[column.ColumnName] != DBNull.Value)
                        {
                            if (pro.PropertyType.Name == "Boolean")
                            {
                                pro.SetValue(obj, Convert.ToBoolean(dr[column.ColumnName].ToString()), null);
                            }
                            else
                            {
                                pro.SetValue(obj, dr[column.ColumnName], null);
                            }
                        }
                        else
                        {
                            pro.SetValue(obj, null, null);
                        }
                    else
                        continue;
                }
            }
            return obj;
        }
        public static bool InsertToDatabase(List<object> datas, string table, string sConnection)
        {
            bool result = false;
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>();
            SqlConnection con = new SqlConnection(sConnection);
            con.Open();
            try
            {
                foreach (var data in datas)
                {

                    values.Clear();
                    foreach (var item in data.GetType().GetProperties())
                    {
                        if (item.Name != "Id")
                            values.Add(new KeyValuePair<string, string>(item.Name, item.GetValue(data, null).ToString()));
                    }
                    string Query = getInsertCommand(table, values);
                    using (SqlCommand cmd = new SqlCommand(Query, con))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                result = true;
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                con.Close();
            }

            return result;
        }
        public static bool InsertToDatabaseObj(object obj, string table, string sConnection)
        {
            List<object> objects = new List<object>();
            objects.Add(obj);
            return InsertToDatabase(objects, table, sConnection);
        }
        public static bool UpdateToDatabase(List<object> datas, string table, string column, string sValue, string sConnection)
        {
            bool result = false;
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>();
            SqlConnection con = new SqlConnection(sConnection);
            con.Open();
            try
            {
                foreach (var data in datas)
                {

                    values.Clear();
                    foreach (var item in data.GetType().GetProperties())
                    {
                        if (item.Name != "Id")
                            values.Add(new KeyValuePair<string, string>(item.Name, item.GetValue(data, null).ToString()));
                    }
                    string Query = getUpdateCommand(table, values, column, sValue);
                    using (SqlCommand cmd = new SqlCommand(Query, con))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                result = true;
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                con.Close();
            }

            return result;
        }
        public static bool UpdateToDatabaseObj(object data, string table, string column, string sValue, string sConnection)
        {
            bool result = false;
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>();
            SqlConnection con = new SqlConnection(sConnection);
            con.Open();
            try
            {
                values.Clear();
                foreach (var item in data.GetType().GetProperties())
                {
                    if (item.Name != "Id")
                        values.Add(new KeyValuePair<string, string>(item.Name, item.GetValue(data, null).ToString()));
                }
                string Query = getUpdateCommand(table, values, column, sValue);
                using (SqlCommand cmd = new SqlCommand(Query, con))
                {
                    cmd.ExecuteNonQuery();
                }

                result = true;
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                con.Close();
            }
            return result;
        }
        public static bool DeleteFromDatabase(string table, string column, string sValue, string sConnection)
        {
            bool result = false;
            string Query = "Delete From  " + table + " Where " + column + " = @" + column + "";
            try
            {
                using (SqlConnection con = new SqlConnection(sConnection))
                {
                    using (SqlCommand cmd = new SqlCommand(Query, con))
                    {

                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@" + column, sValue);
                        con.Open();
                        cmd.ExecuteNonQuery();
                    }

                }
                result = true;
            }
            catch (Exception)
            {

                throw;
            }

            return result;
        }
        private static string getInsertCommand(string table, List<KeyValuePair<string, string>> values)
        {
            string query = null;
            query += "INSERT INTO " + table + " ( ";
            foreach (var item in values)
            {
                query += item.Key;
                query += ", ";
            }
            query = query.Remove(query.Length - 2, 2);
            query += ") VALUES ( ";
            foreach (var item in values)
            {
                if (item.Key.GetType().Name == "System.Int") // or any other numerics
                {
                    query += item.Value;
                }
                else
                {
                    query += "'";
                    query += item.Value;
                    query += "'";
                }
                query += ", ";
            }
            query = query.Remove(query.Length - 2, 2);
            query += ")";
            return query;
        }
        private static string getUpdateCommand(string table, List<KeyValuePair<string, string>> values, string column, string sValue)
        {
            string query = null;
            query += "Update  " + table + " Set ";
            foreach (var item in values)
            {
                query += item.Key;
                query += "=";
                if (item.Key.GetType().Name == "System.Int") // or any other numerics
                {
                    query += item.Value;
                }
                else
                {
                    query += "'";
                    query += item.Value;
                    query += "'";
                }
                query += ", ";
            }
            query = query.Remove(query.Length - 2, 2);
            query += " Where " + column + " = '" + sValue + "'";
            return query;
        }
        public static List<string> GetStringListMethod(string query, string sConnection)
        {
            List<string> result = new List<string>();

            try
            {
                using (SqlConnection conn = new SqlConnection(sConnection))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Assuming first column is the string you want
                            result.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw; // optional: or log ex and rethrow
            }

            return result;
        }

    }

}
