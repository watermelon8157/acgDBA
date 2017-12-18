using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using Dapper;
using System.Collections;
using Oracle.ManagedDataAccess.Client;

namespace acgDBA
{

    /// <summary>
    /// 資料庫連線方式
    /// </summary>
    public abstract class DBConnector
    {
        /// <summary>
        /// 有最後錯誤
        /// </summary>
        public bool hasLastError { get { return !string.IsNullOrWhiteSpace(this.RESPONSE_MSG.message); } }
        /// <summary>
        /// 最後錯誤訊息
        /// </summary>
        public string lastError { get { return this.RESPONSE_MSG.message; } }

        /// <summary>
        /// DBConnector 回傳訊息
        /// </summary>
        public RESPONSE_MSG RESPONSE_MSG { get; set; }

        #region 參數 
        /// <summary>
        /// 是否正在執行交易
        /// </summary>
        private bool dbBeginTrans { get; set; }

        /// <summary>
        /// DB交易狀態狀態，是否正在交易
        /// </summary>
        private bool dbTransUsing
        {
            get
            {
                return this.dbTrans != null;
            }
        }
        /// <summary>
        /// DB交易
        /// </summary>
        private DbTransaction dbTrans { get; set; }

        /// <summary>
        /// DB連線狀況
        /// </summary>
        public ConnectionState dbState
        {
            get
            {
                if (this.dbConnection != null)
                {
                    return this.dbConnection.State;
                }
                else
                {
                    return ConnectionState.Closed;
                }
            }
        }

        /// <summary>
        /// DbConnection
        /// </summary>
        public DbConnection dbConnection { get; set; }

        /// <summary>
        /// 設定連線Provider
        /// </summary>
        public IDBProvider dbProvider
        {
            get;
            private set;
        }

        /// <summary>
        /// 設定DB連線字串
        /// </summary>
        public string connectionString
        {
            get;
            private set;
        }

        /// <summary>
        /// 預設Timeout
        /// </summary>
        public int? defaultTimeout
        {
            get;
            private set;
        }

        /// <summary>
        /// 記錄DBlog
        /// </summary>
        private ILog logger { get; set; }
        #endregion


        /// <summary>
        /// 設定DB連線方式
        /// </summary>
        /// <param name="DBProvider"></param>
        /// <param name="ConnectionString"></param>
        /// <param name="Logger"></param>
        /// <param name="DefaultTimeout"></param>
        protected DBConnector(IDBProvider pDBProvider, string pConnectionString, ILog pLogger, int? pDefaultTimeout)
        {
            this.RESPONSE_MSG = new RESPONSE_MSG();
            if (pDBProvider == null)
            {
                throw new ArgumentNullException("DBProvider");
            }
            this.dbProvider = pDBProvider;
            this.connectionString = pConnectionString;
            this.defaultTimeout = pDefaultTimeout;
            this.logger = pLogger;
        }

        #region 工作
        /// <summary>
        /// 關閉目前的資料連線
        /// </summary>
        public void Close()
        {
            if (this.dbConnection == null)
            {
                return;
            }
            if (this.dbTransUsing)
            {
                if (this.RESPONSE_MSG.status == RESPONSE_STATUS.SUCCESS)
                {
                    this.dbTrans.Commit();
                }
                else
                {
                    this.dbTrans.Rollback();
                }
                checkDbTrans();
            }
            this.dbBeginTrans = false;
            if (this.dbConnection.State != ConnectionState.Closed)
            {
                if (logger != null)
                {
                    logger.Info("Try Open Connection.");
                }
                this.dbConnection.Close();
            }
        }

        /// <summary>
        /// 開啟資料庫連接。
        /// </summary>
        /// <param name="open_transcation">開啟交易</param>
        public void Open()
        {
            try
            {
                //如果沒有連線，建立連線
                if (this.dbConnection == null)
                {
                    if (logger != null)
                    {
                        logger.Info(string.Concat("Create Connection Object.  ConnectionString = \"", this.connectionString, "\""));
                    }
                    this.dbConnection = this.dbProvider.CreateConnectionObject();
                    this.dbConnection.ConnectionString = this.connectionString;

                }
                //如果資料庫關閉，開啟資料庫
                if (this.dbConnection.State == ConnectionState.Closed)
                {
                    if (logger != null)
                    {
                        logger.Info("Try Open Connection.");
                    }
                    this.dbConnection.Open();
                }
            }
            catch (Exception ex)
            {
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
                if (logger != null)
                {
                    logger.Error(ex);
                }
                checkIsCanClose();
            }

        }

        /// <summary>
        /// 開始交易程序
        /// </summary>
        /// <param name="IsolationLevel"></param>
        public void BeginTrans(IsolationLevel IsolationLevel = IsolationLevel.Serializable)
        {
            try
            {
                //檢查是否交易正在使用
                if (this.dbTransUsing)
                {
                    if (logger != null)
                    {
                        logger.Error("Transaction 正在使用中");
                    }
                    throw new NotSupportedException("Transaction 正在使用中");
                }
                this.Open();//開啟DB
                if (!this.dbBeginTrans && !this.dbTransUsing)
                {
                    this.dbTrans = this.dbConnection.BeginTransaction(IsolationLevel);
                    this.dbBeginTrans = true;
                }
            }
            catch (Exception ex)
            {
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
                if (logger != null)
                {
                    logger.Error(ex);
                }
            }
            finally
            {
                checkIsCanClose();
            }
        }

        /// <summary>
        /// RollBack交易程序
        /// </summary>
        public void Rollback()
        {
            try
            {
                if (this.dbTransUsing)
                {
                    this.dbTrans.Rollback();
                    checkDbTrans();
                }
                else
                {
                    if (logger != null)
                    {
                        logger.Error("尚未開啟 Transaction");
                    }
                    throw new NotSupportedException("尚未開啟 Transaction");
                }
            }
            catch (Exception ex)
            {
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
                if (logger != null)
                {
                    logger.Error(ex);
                }
            }
            finally
            {
                checkIsCanClose();
            }


        }

        /// <summary>
        /// Commit交易程序
        /// </summary>
        public void Commit()
        {
            try
            {
                if (this.dbTransUsing)
                {
                    this.dbTrans.Commit();
                    checkDbTrans();
                }
                else
                {
                    if (logger != null)
                    {
                        logger.Error("尚未開啟 Transaction");
                    }
                    throw new NotSupportedException("尚未開啟 Transaction");
                }
            }
            catch (Exception ex)
            {
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
                if (logger != null)
                {
                    logger.Error(ex);
                }
            }
            finally
            {
                checkIsCanClose();
            }

        }

        #endregion

        #region 結果

        #region 取得資料

        /// <summary>
        /// 取得強行別資料
        /// </summary>
        /// <typeparam name="T">傳入物件</typeparam>
        /// <param name="varSQL">sql字串</param>
        /// <param name="varParam">傳入值</param>
        /// <returns></returns>
        public List<T> getSqlDataTable<T>(string varSQL, DynamicParameters varParameter = null)
        {
            List<T> list = new List<T>();
            try
            {
                if (this.RESPONSE_MSG.status == RESPONSE_STATUS.SUCCESS)
                {
                    this.RESPONSE_MSG.message = "";
                    checkGetSqlDataTable(varSQL);
                    if (string.IsNullOrWhiteSpace(this.RESPONSE_MSG.message))
                    {
                        this.Open();
                        if (!this.dbTransUsing) this.dbTrans = this.dbConnection.BeginTransaction();
                        list = this.dbConnection.Query<T>(varSQL, varParameter, this.dbTrans).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex);
                }
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
            }
            finally
            {
                checkIsCanClose();
            }
            return list;
        }

        /// <summary>
        /// 取得DataTable弱行別資料
        /// </summary>
        /// <param name="varSQL"></param>
        /// <param name="varParam"></param>
        /// <returns></returns>
        public DataTable getSqlDataTable(string varSQL, DynamicParameters varParameter = null)
        {
            DataTable _dataTable = new DataTable();
            try
            {
                if (this.RESPONSE_MSG.status == RESPONSE_STATUS.SUCCESS)
                {
                    this.RESPONSE_MSG.message = "";
                    checkGetSqlDataTable(varSQL);
                    if (string.IsNullOrWhiteSpace(this.RESPONSE_MSG.message))
                    {
                        this.Open();
                        if (!this.dbTransUsing) this.dbTrans = this.dbConnection.BeginTransaction();
                        _dataTable.Load(this.dbConnection.ExecuteReader(varSQL, varParameter, this.dbTrans));
                    }
                }
            }
            catch (Exception ex)
            {

                if (logger != null)
                {
                    logger.Error(ex);
                }
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
            }
            finally
            {
                checkIsCanClose();
            }
            return _dataTable;
        }

        #endregion

        /// <summary>
        /// DataTable資料表(弱型別)更新資料方式
        /// </summary>
        /// <param name="varDataTable">資料表</param>
        /// <param name="varTableName">資料表名稱</param>
        /// <returns></returns>
        public int Update(DataTable varDataTable, string varTableName)
        {
            RESPONSE_MSG.message = "";
            int cnt = 0;
            try
            {
                if (!string.IsNullOrWhiteSpace(varDataTable.TableName))
                {
                    varTableName = varDataTable.TableName;
                }
                if (string.IsNullOrWhiteSpace(varTableName))
                {
                    RESPONSE_MSG.status = RESPONSE_STATUS.ERROR;
                    RESPONSE_MSG.message = "無法取得資料表名稱，無法更新";
                    return cnt;
                }
                varDataTable.TableName = varTableName;
                DbCommand _dbCommand = this.getDBCommand(string.Concat("Select * from ", varTableName));
                if (!this.dbTransUsing) this.dbTrans = this.dbConnection.BeginTransaction();
                _dbCommand.Transaction = this.dbTrans;
                DbDataAdapter _da = this.getDBDataAdapter(_dbCommand);
                DbCommandBuilder dbCommandBuilder = dbProvider.CreateCommandBuilder();
                dbCommandBuilder.DataAdapter = _da;
                try
                {
                    cnt = _da.Update(varDataTable);
                }
                catch (Exception ex)
                {
                    if (logger != null)
                    {
                        logger.Error(ex);
                    }
                    RESPONSE_MSG.status = RESPONSE_STATUS.ERROR;
                    RESPONSE_MSG.message = ex.Message;
                }
                finally
                {
                    this.releaseAdapter(_da);
                    try
                    {
                        dbCommandBuilder.DataAdapter = null;
                        dbCommandBuilder.Dispose();
                        dbCommandBuilder = null;
                    }
                    catch (Exception ex)
                    {
                        if (logger != null)
                        {
                            logger.Error(ex);
                        }
                        RESPONSE_MSG.status = RESPONSE_STATUS.ERROR;
                        RESPONSE_MSG.message = ex.Message;
                    }
                }

            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex);
                }
                RESPONSE_MSG.status = RESPONSE_STATUS.ERROR;
                RESPONSE_MSG.message = ex.Message;
            }
            finally
            {
                checkIsCanClose();
            }
            return cnt;
        }

        /// <summary>
        /// Execute-自訂
        /// </summary>
        /// <param name="varInsertSql"></param>
        /// <param name="varInsertParameter"></param>
        /// <returns></returns>
        public int DBExecute(string varInsertSql, DynamicParameters varParameter)
        {
            int eff_row = 0;
            try
            {
                if (RESPONSE_MSG.status == RESPONSE_STATUS.SUCCESS)
                {
                    RESPONSE_MSG.message = "";
                    if (string.IsNullOrWhiteSpace(RESPONSE_MSG.message))
                    {
                        this.Open();
                        if (!this.dbTransUsing) this.dbTrans = this.dbConnection.BeginTransaction();
                        eff_row = this.dbConnection.Execute(varInsertSql, varParameter, transaction: dbTrans);
                    }
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex);
                }
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
            }
            finally
            {
                checkIsCanClose();
            }
            return eff_row;
        }
        /// <summary>
        /// Execute-強型別多筆
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="varInsertSql"></param>
        /// <param name="varParameter"></param>
        /// <returns></returns>
        public int DBExecute<T>(string varInsertSql, List<T> varParameter)
        {
            int eff_row = 0;
            try
            {
                if (this.RESPONSE_MSG.status == RESPONSE_STATUS.SUCCESS)
                {
                    this.RESPONSE_MSG.message = "";
                    if (string.IsNullOrWhiteSpace(this.RESPONSE_MSG.message))
                    {
                        this.Open();
                        if (!this.dbTransUsing) this.dbTrans = this.dbConnection.BeginTransaction();
                        eff_row = this.dbConnection.Execute(varInsertSql, varParameter, transaction: dbTrans);
                    }
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex);
                }
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
            }
            finally
            {
                checkIsCanClose();
            }
            return eff_row;
        }

        #region 尚未測試


        #region 新增資料

        /// <summary>
        /// 新增-單筆資料
        /// </summary>
        /// <param name="varTableName">資料表名稱</param>
        /// <param name="varInsertParameter">新增的具名參數</param>
        /// <returns></returns>
        public int DBExecInsert(string varTableName, List<dataParameters> varInsertParameter)
        {
            int eff_row = 0;
            try
            {
                if (this.RESPONSE_MSG.status == RESPONSE_STATUS.SUCCESS)
                {
                    string sql_statment = "insert into {0} ({1}) values ({2});";
                    List<string> field_string = new List<string>(), value_string = new List<string>();
                    DynamicParameters varParameter = new DynamicParameters();
                    foreach (dataParameters item in varInsertParameter)
                    {
                        string NamedArguments = this.getNamedArguments();
                        string ParameterName = item.ParameterName.Trim('@').Trim(':');
                        string NamedArgumentsParameterName = string.Concat(NamedArguments, ParameterName);
                        field_string.Add(ParameterName);
                        value_string.Add(NamedArgumentsParameterName);
                        varParameter.Add(NamedArgumentsParameterName, item.Value, item.DbType);
                    }
                    sql_statment = string.Format(sql_statment, varTableName, string.Join(",", field_string), string.Join(",", value_string));

                    this.RESPONSE_MSG.message = "";
                    if (string.IsNullOrWhiteSpace(this.RESPONSE_MSG.message))
                    {
                        this.Open();
                        if (!this.dbTransUsing) this.dbTrans = this.dbConnection.BeginTransaction();
                        eff_row = this.dbConnection.Execute(sql_statment, varParameter, transaction: dbTrans);
                    }
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex);
                }
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
            }
            finally
            {
                checkIsCanClose();
            }
            return eff_row;
        }

        /// <summary>
        /// 新增-指定強型別多筆資料
        /// </summary>
        /// <typeparam name="T">指定強型別</typeparam>
        /// <param name="varInsertSql">SQL語法</param>
        /// <param name="varInsertParameter">強型別多筆新增</param>
        /// <returns></returns>
        public int DBExecInsert<T>(string varInsertSql, List<T> varInsertParameter)
        {
            int eff_row = 0;
            try
            {
                if (this.RESPONSE_MSG.status == RESPONSE_STATUS.SUCCESS)
                {
                    this.RESPONSE_MSG.message = "";
                    if (string.IsNullOrWhiteSpace(this.RESPONSE_MSG.message))
                    {
                        this.Open();
                        if (!this.dbTransUsing) this.dbTrans = this.dbConnection.BeginTransaction();
                        eff_row = this.dbConnection.Execute(varInsertSql, varInsertParameter, transaction: dbTrans);
                    }
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex);
                }
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
            }
            finally
            {
                checkIsCanClose();
            }
            return eff_row;
        }

        #endregion

        #region 修改資料

        /// <summary>
        /// 修改-單筆資料
        /// </summary>
        /// <param name="varTableName">資料表名稱</param>
        /// <param name="varUpdateParameter">修改的具名參數</param>
        /// <param name="varWhereSql">WhereSQL</param>
        /// <param name="varWhereParameter">Where具名參數</param>
        /// <returns></returns>
        public int DBExecUpdate(string varTableName, List<dataParameters> varUpdateParameter, string varWhereSql, DynamicParameters varWhereParameter)
        {
            int eff_row = 0;
            try
            {
                if (this.RESPONSE_MSG.status == RESPONSE_STATUS.SUCCESS)
                {
                    DynamicParameters varParameter = null;
                    string sql_statment = "update {0} set {1} where {2}";
                    List<string> update_str = new List<string>();
                    if (varWhereParameter == null)
                    {
                        varParameter = new DynamicParameters();
                    }
                    else
                    {
                        varParameter = varWhereParameter;
                    }
                    foreach (dataParameters item in varUpdateParameter)
                    {
                        string NamedArguments = this.getNamedArguments();
                        string ParameterName = item.ParameterName.Trim('@').Trim(':');
                        string NamedArgumentsParameterName = string.Concat(NamedArguments, "Update", ParameterName);
                        update_str.Add(string.Format("{0}={1}", ParameterName, NamedArgumentsParameterName));
                        varParameter.Add(NamedArgumentsParameterName, item.Value, item.DbType);
                    }
                    sql_statment = string.Format(sql_statment, varTableName, string.Join(",", update_str), varWhereSql);

                    this.RESPONSE_MSG.message = "";
                    if (string.IsNullOrWhiteSpace(this.RESPONSE_MSG.message))
                    {
                        this.Open();
                        if (!this.dbTransUsing) this.dbTrans = this.dbConnection.BeginTransaction();
                        eff_row = this.dbConnection.Execute(sql_statment, varParameter, transaction: dbTrans);

                    }
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex);
                }
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
            }
            finally
            {
                checkIsCanClose();
            }
            return eff_row;
        }

        #endregion

        #region 刪除資料

        /// <summary>
        /// 刪除-單筆資料
        /// </summary>
        /// <param name="varTableName">資料表名稱</param>
        /// <param name="varWhereSql">WhereSQL</param>
        /// <param name="varWhereParameter">Where具名參數</param>
        /// <returns></returns>
        public int DBExecDelete(string varTableName, string varWhereSql, DynamicParameters varWhereParameter)
        {
            int eff_row = 0;
            try
            {
                if (this.RESPONSE_MSG.status == RESPONSE_STATUS.SUCCESS)
                {
                    string sql_statment = string.Concat("delete from ", varTableName, " where ", varWhereSql);

                    this.RESPONSE_MSG.message = "";
                    if (string.IsNullOrWhiteSpace(this.RESPONSE_MSG.message))
                    {
                        this.Open();
                        if (!this.dbTransUsing) this.dbTrans = this.dbConnection.BeginTransaction();
                        eff_row = this.dbConnection.Execute(sql_statment, varWhereParameter, transaction: dbTrans);
                    }
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex);
                }
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
            }
            finally
            {
                checkIsCanClose();
            }
            return eff_row;
        }

        /// <summary>
        /// 刪除-指定強型別多筆資料
        /// </summary>
        /// <typeparam name="T">指定強型別</typeparam>
        /// <param name="varDelSql">SQL語法</param>
        /// <param name="varDelParameter">強型別多筆新增</param>
        /// <returns></returns>
        public int DBExecDelete<T>(string varDelSql, List<T> varDelParameter)
        {
            int eff_row = 0;
            try
            {
                if (this.RESPONSE_MSG.status == RESPONSE_STATUS.SUCCESS)
                {
                    this.RESPONSE_MSG.message = "";
                    if (string.IsNullOrWhiteSpace(this.RESPONSE_MSG.message))
                    {
                        this.Open();
                        if (!this.dbTransUsing) this.dbTrans = this.dbConnection.BeginTransaction();
                        eff_row = this.dbConnection.Execute(varDelSql, varDelParameter, transaction: dbTrans);
                    }
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex);
                }
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
            }
            finally
            {
                checkIsCanClose();
            }
            return eff_row;
        }
        #endregion


        #endregion

        #endregion

        #region 私有方法

        /// <summary>
        /// 檢查DbTrans
        /// </summary>
        private void checkDbTrans()
        {
            if (this.dbTransUsing)
            {
                this.dbTrans.Dispose();
                this.dbTrans = null;
            }
        }

        /// <summary>
        /// 檢查是否可以關閉
        /// </summary>
        private void checkIsCanClose()
        {
            if (!this.dbBeginTrans)
            {
                this.Close();
            }
        }

        /// <summary>
        /// 取得具名參數
        /// </summary>
        /// <returns></returns>
        private string getNamedArguments()
        {
            switch (this.dbProvider.enmDBType)
            {
                case enmDBAProvider.Oracle:
                    return ":";
                case enmDBAProvider.MS:
                    return "@";
                default:
                    return "";
            }
        }

        private DbCommand getDBCommand(string varSql)
        {
            DbCommand dbCommand = this.dbProvider.CreateCommandObject();
            dbCommand.Connection = this.dbConnection;
            dbCommand.CommandText = varSql;
            if (this.dbTransUsing)
            {
                dbCommand.Transaction = dbTrans;
            }
            return dbCommand;
        }

        private DbDataAdapter getDBDataAdapter(DbCommand pDBCommand)
        {
            DbDataAdapter dbDataAdapter = this.dbProvider.CreateDataAdapter();
            if (dbDataAdapter != null)
            {
                dbDataAdapter.SelectCommand = pDBCommand;
            }
            return dbDataAdapter;
        }

        private void releaseAdapter(DbDataAdapter pAdapter, bool release = false)
        {
            try
            {
                if (pAdapter != null)
                {
                    if (pAdapter.SelectCommand != null)
                    {
                        releaseCommand(pAdapter.SelectCommand);
                        pAdapter.SelectCommand = null;
                    }
                    if (pAdapter.InsertCommand != null)
                    {
                        releaseCommand(pAdapter.InsertCommand);
                        pAdapter.InsertCommand = null;
                    }
                    if (pAdapter.UpdateCommand != null)
                    {
                        releaseCommand(pAdapter.UpdateCommand);
                        pAdapter.UpdateCommand = null;
                    }
                    if (pAdapter.DeleteCommand != null)
                    {
                        releaseCommand(pAdapter.DeleteCommand);
                        pAdapter.DeleteCommand = null;
                    }
                    pAdapter.Dispose();
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex);
                }
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
            }
        }

        private void releaseCommand(DbCommand pCommand, bool release = false)
        {
            try
            {
                if (pCommand != null)
                {
                    if (pCommand.Transaction != null)
                    {
                        pCommand.Transaction = null;
                    }
                    if (pCommand.Connection != null)
                    {
                        pCommand.Connection = null;
                    }
                    pCommand.Dispose();
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(ex);
                }
                this.RESPONSE_MSG.status = RESPONSE_STATUS.EXCEPTION;
                this.RESPONSE_MSG.message = ex.Message;
            }
        }
        /// <summary>
        /// 檢查getSqlDataTable的sql字串
        /// </summary>
        /// <param name="pVarSQL"></param>
        private void checkGetSqlDataTable(string pVarSQL)
        {
            //檢查sql
            if (string.IsNullOrWhiteSpace(pVarSQL))
            {
                this.RESPONSE_MSG.message = "傳入SQL參數是NUL";
            }
            //檢查select
            if (!pVarSQL.ToLower().Trim().StartsWith("select "))
            {
                this.RESPONSE_MSG.message = "錯誤SQL語法[查無Select關鍵字]";
            }
            if (!string.IsNullOrWhiteSpace(this.RESPONSE_MSG.message))
            {
                this.RESPONSE_MSG.status = RESPONSE_STATUS.ERROR;
            }
        }
        #endregion


    }

    #region 基本設定

    public enum enmDBAProvider
    {
        MS,
        Oracle
    }

    public interface IDBProvider
    {
        /// <summary>
        /// 建立DB連線Object
        /// </summary>
        /// <returns></returns>
        DbConnection CreateConnectionObject();

        /// <summary>
        /// 建立CommandObject
        /// </summary>
        /// <returns></returns>
        DbCommand CreateCommandObject();

        /// <summary>
        /// 建立CommandBuilder
        /// </summary>
        /// <returns></returns>
        DbCommandBuilder CreateCommandBuilder();

        /// <summary>
        /// 建立DataAdapter
        /// </summary>
        /// <returns></returns>
        DbDataAdapter CreateDataAdapter();

        /// <summary>
        /// 資料庫類型
        /// </summary>
        enmDBAProvider enmDBType { get; }
    }

    public class dataParameters
    {
        /// <summary>
        /// SQL Server 中處理 Unicode 字串常數時，必需為所有的 Unicode 字串加上前置詞 N
        /// </summary>
        public bool isUnicode { set; get; }
        /// <summary>
        /// 欄位名稱
        /// </summary>
        public string ParameterName { set; get; }

        /// <summary>
        /// 值
        /// </summary>
        public object Value { set; get; }

        /// <summary>
        /// 資料型態
        /// </summary>
        public DbType DbType { set; get; }
    }

    #region MultiDataParameters(尚未完成)

    /// <summary>
    /// DataParameters類型
    /// </summary>
    public enum enmDataParametersType
    {
        //
        // 摘要:
        //     已經建立資料列，但不是任何 System.Data.DataRowCollection 的一部分。System.Data.DataRow 在已經建立後、加入至集合前，或如果已經從集合移除後，會立即處在這個狀態中。
        Detached = 1,
        //
        // 摘要:
        //     自從上次呼叫 System.Data.DataRow.AcceptChanges 之後，資料列尚未變更。
        Unchanged = 2,
        //
        // 摘要:
        //     資料列已經加入至 System.Data.DataRowCollection，並且尚未呼叫 System.Data.DataRow.AcceptChanges。
        Added = 4,
        //
        // 摘要:
        //     使用 System.Data.DataRow 的 System.Data.DataRow.Delete 方法來刪除資料列。
        Deleted = 8,
        //
        // 摘要:
        //     已經修改資料列，並且尚未呼叫 System.Data.DataRow.AcceptChanges。
        Modified = 16
    }


    /// <summary>
    /// 多種DataParameters
    /// <para>DataParametersType判斷資料類別</para>
    /// </summary>
    public class MultiDataParameters<T>
    {
        public T DataParameters { get; set; }
        /// <summary>
        /// DataParameters類型
        /// </summary>
        public enmDataParametersType DataParametersType { get; set; }
    }

    public class MultiDataParametersCollection<T>
    {
        public MultiDataParametersCollection()
        {
            //DataTable dt = new DataTable();
            //dt.Rows.Add();
        }
    }

    public class DataParametersList<T>
    {
        public List<MultiDataParameters<T>> DataParametersItem { get; set; }

        /// <summary>
        /// 子建構式
        /// </summary>
        public DataParametersList()
        {
            DataParametersItem = new List<MultiDataParameters<T>>();
        }

        /// <summary>
        /// 新增預設資料
        /// </summary>
        /// <param name="pList"></param>
        public DataParametersList(List<T> pList)
        {
            DataParametersItem = new List<MultiDataParameters<T>>();
            foreach (T item in pList)
            {
                DataParametersItem.Add(new MultiDataParameters<T>() { DataParameters = item, DataParametersType = enmDataParametersType.Unchanged });
            }
        }

        /// <summary>
        /// 新增一筆資料
        /// </summary>
        /// <param name="pItem"></param>
        /// <returns></returns>
        public MultiDataParameters<T> NewItem(T pItem)
        {
            return new MultiDataParameters<T>() { DataParameters = pItem, DataParametersType = enmDataParametersType.Detached };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pItem"></param>
        public void Add(MultiDataParameters<T> pItem)
        {
            pItem.DataParametersType = enmDataParametersType.Added;
            DataParametersItem.Add(pItem);
        }

        public void DeleteAt(MultiDataParameters<T> pItem)
        {
            if (DataParametersItem.IndexOf(pItem) >= 0)
            {
                DataParametersItem[DataParametersItem.IndexOf(pItem)].DataParametersType = enmDataParametersType.Deleted;
            }
        }
    }
    #endregion

    #endregion
}
