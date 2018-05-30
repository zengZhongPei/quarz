using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Web.Script.Serialization;

namespace AutoManage.Sql
{
    internal class SqlServer
    {
        public string ConnectionString;
        public SqlServer(string connStr)
        {
            this.ConnectionString = connStr;
        }
        public SqlConnection GetSqlConnection()
        {
            return new SqlConnection(this.ConnectionString);
        }
        public int ExecuteSql(string SQLString, params SqlParameter[] cmdParms)
        {
            string text = string.Empty;
            int result = 0;
            using (SqlConnection sqlConnection = this.GetSqlConnection())
            {
                using (SqlCommand SqlCommand = new SqlCommand())
                {
                    try
                    {
                        SqlServer.PrepareCommand(SqlCommand, sqlConnection, null, SQLString, cmdParms);
                        result = SqlCommand.ExecuteNonQuery();

                        RecordSqlCall();
                    }
                    catch (Exception ex)
                    {
                        text = string.Concat(new string[]
						{
							ex.Message,
							"  ",
							ex.StackTrace,
							"  \r\n",
							SQLString
						});
                    }
                    finally
                    {
                        SqlCommand.Parameters.Clear();
                        SqlCommand.Dispose();
                        sqlConnection.Close();
                    }
                }
            }
            if (text != string.Empty)
            {
                throw new Exception(text);
            }
            return result;
        }
        public object ExecuteScalar(string SQLString, params SqlParameter[] cmdParms)
        {
            string text = string.Empty;
            object result =null;
            using (SqlConnection sqlConnection = this.GetSqlConnection())
            {
                using (SqlCommand SqlCommand = new SqlCommand())
                {
                    try
                    {
                        SqlServer.PrepareCommand(SqlCommand, sqlConnection, null, SQLString, cmdParms);
                        result = SqlCommand.ExecuteScalar();

                        RecordSqlCall();
                    }
                    catch (Exception ex)
                    {
                        text = string.Concat(new string[]
                        {
                            ex.Message,
                            "  ",
                            ex.StackTrace,
                            "  \r\n",
                            SQLString
                        });
                    }
                    finally
                    {
                        SqlCommand.Parameters.Clear();
                        SqlCommand.Dispose();
                        sqlConnection.Close();
                    }
                }
            }
            if (text != string.Empty)
            {
                throw new Exception(text);
            }
            return result;
        }
        
        private static void PrepareCommand(SqlCommand cmd, SqlConnection conn, SqlTransaction trans, string cmdText, SqlParameter[] cmdParms)
        {
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }
            cmd.Connection = conn;
            cmd.CommandText = cmdText;
            if (trans != null)
            {
                cmd.Transaction = trans;
            }
            cmd.CommandType = CommandType.Text;
            if (cmdParms != null)
            {
                SqlParameter[] array = cmdParms;
                for (int i = 0; i < array.Length; i++)
                {
                    SqlParameter SqlParameter = array[i];
                    if (SqlParameter != null)
                    {
                        if ((SqlParameter.Direction == ParameterDirection.InputOutput || SqlParameter.Direction == ParameterDirection.Input) && SqlParameter.Value == null)
                        {
                            SqlParameter.Value = DBNull.Value;
                        }
                        if (SqlParameter.Value.GetType().FullName == "System.Collections.Hashtable")
                        {
                            Hashtable obj = (Hashtable)SqlParameter.Value;
                            SqlParameter.Value = new JavaScriptSerializer().Serialize(obj);
                        }
                        cmd.Parameters.Add(SqlParameter);
                    }
                }
                cmdParms = null;
            }
        }
        public object GetSingle(string SQLString, params SqlParameter[] cmdParms)
        {
            object result = null;
            string text = string.Empty;
            using (SqlConnection sqlConnection = this.GetSqlConnection())
            {
                using (SqlCommand SqlCommand = new SqlCommand())
                {
                    try
                    {
                        SqlServer.PrepareCommand(SqlCommand, sqlConnection, null, SQLString, cmdParms);
                        object obj = SqlCommand.ExecuteScalar();
                        if (!object.Equals(obj, null) && !object.Equals(obj, DBNull.Value))
                        {
                            result = obj;
                        }

                        RecordSqlCall();
                    }
                    catch (Exception ex)
                    {
                        text = string.Concat(new string[]
						{
							ex.Message,
							" ",
							ex.StackTrace,
							"\r\n",
							SQLString
						});
                    }
                    finally
                    {
                        SqlCommand.Parameters.Clear();
                        SqlCommand.Dispose();
                        sqlConnection.Close();
                        sqlConnection.Dispose();
                    }
                }
            }
            if (text != string.Empty)
            {
                throw new Exception(text);
            }
            return result;
        }
        public void ExecuteCommand(DbCommand cmd)
        {
            using (SqlConnection sqlConnection = this.GetSqlConnection())
            {
                sqlConnection.Open();
                cmd.Connection = sqlConnection;
                cmd.ExecuteNonQuery();
                sqlConnection.Close();
                sqlConnection.Dispose();

                RecordSqlCall();
            }
        }
        public void ExecTransation(string[] sql)
        {
            string text = string.Empty;
            using (SqlConnection sqlConnection = this.GetSqlConnection())
            {
                sqlConnection.Open();
                SqlTransaction SqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadCommitted);
                SqlCommand SqlCommand = new SqlCommand();
                try
                {
                    SqlCommand.Connection = sqlConnection;
                    SqlCommand.Transaction = SqlTransaction;
                    for (int i = 0; i < sql.Length; i++)
                    {
                        string commandText = sql[i];
                        SqlCommand.CommandText = commandText;
                        SqlCommand.ExecuteNonQuery();
                    }
                    SqlTransaction.Commit();

                    RecordSqlCall();
                }
                catch (Exception ex)
                {
                    SqlTransaction.Rollback();
                    text = ex.Message + "  " + ex.StackTrace + SqlCommand.CommandText;
                }
                finally
                {
                    SqlCommand.Dispose();
                    sqlConnection.Close();
                    sqlConnection.Dispose();
                }
            }
            if (text != string.Empty)
            {
                throw new Exception(text);
            }
        }
        public long InsertCommand(DbCommand cmd)
        {
            long result = 0L;
            using (SqlConnection sqlConnection = this.GetSqlConnection())
            {
                sqlConnection.Open();
                cmd.Connection = sqlConnection;
                if (cmd.CommandText.LastIndexOf("@@") > 0)
                {
                    SqlDataReader SqlDataReader = (SqlDataReader)cmd.ExecuteReader();
                    if (SqlDataReader.Read())
                    {
                        result = Convert.ToInt64(SqlDataReader[0]);
                    }
                    SqlDataReader.Close();

                    RecordSqlCall();
                }
                else
                {
                    cmd.ExecuteNonQuery();
                }
                sqlConnection.Close();
                sqlConnection.Dispose();
            }
            return result;
        }
        public DataTable GetDataTable(string sql, params SqlParameter[] param)
        {
            DataTable dataTable = new DataTable();
            using (SqlConnection sqlConnection = this.GetSqlConnection())
            {
                using (SqlCommand SqlCommand = new SqlCommand())
                {
                    SqlServer.PrepareCommand(SqlCommand, sqlConnection, null, sql, param);
                    using (SqlDataAdapter SqlDataAdapter = new SqlDataAdapter(SqlCommand))
                    {
                        new DataSet();
                        try
                        {
                            SqlDataAdapter.Fill(dataTable);
                            SqlCommand.Parameters.Clear();

                            RecordSqlCall();
                        }
                        catch (SqlException ex)
                        {
                            throw new Exception(string.Concat(new string[]
							{
								ex.Message,
								"  ",
								ex.StackTrace,
								"\r\n",
								SqlCommand.CommandText
							}));
                        }
                        finally
                        {
                            SqlCommand.Parameters.Clear();
                            SqlCommand.Dispose();
                            sqlConnection.Close();
                            sqlConnection.Dispose();
                        }
                    }
                }
            }
            return dataTable;
        }
        public DataTable GetSchemaTable(string tableName)
        {
            string cmdText = "select * from " + tableName + " where 1<>1";
            DataTable result = null;
            using (SqlConnection sqlConnection = this.GetSqlConnection())
            {
                sqlConnection.Open();
                SqlCommand SqlCommand = new SqlCommand(cmdText, sqlConnection);
                using (SqlDataReader SqlDataReader = SqlCommand.ExecuteReader())
                {
                    result = SqlDataReader.GetSchemaTable();
                    SqlDataReader.Close();
                    SqlDataReader.Dispose();

                   // RecordSqlCall();
                }
                sqlConnection.Close();
                sqlConnection.Dispose();
            }
            return result;
        }
        public bool CheckTableIsExist(string tableName)
        {
            bool result = true;
            string cmdText = "select count(*) from " + tableName + " where 1<>1";
            using (SqlConnection sqlConnection = this.GetSqlConnection())
            {
                sqlConnection.Open();
                SqlCommand SqlCommand = new SqlCommand(cmdText, sqlConnection);
                try
                {
                    SqlCommand.ExecuteNonQuery();

                    RecordSqlCall();
                }
                catch (SqlException)
                {
                    result = false;
                }
                sqlConnection.Close();
                sqlConnection.Dispose();
            }
            return result;
        }



        void RecordSqlCall()
        {
            
        }
    }
}
