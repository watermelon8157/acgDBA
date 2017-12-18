using System.Data.Common;
using log4net;
using Oracle.ManagedDataAccess.Client;
using System.Data.SqlClient;

/// <summary>
/// 定義DBProvider
/// <para>Connector繼承DBConnector</para>
/// <para>Provider繼承IDBProvider</para>
/// </summary>
namespace acgDBA
{
    #region MSSQLDB Provider

    public class MSSQLDBConnector : DBConnector
    {
        public MSSQLDBConnector(string ConnectionString, ILog Logger = null, int? DefaultTimeout = null) : base(new MSSQLProvider(), ConnectionString, Logger, DefaultTimeout)
        {

        }
    }

    public class MSSQLProvider : IDBProvider
    {
        public DbConnection CreateConnectionObject()
        {
            return new SqlConnection();
        }

        public DbCommand CreateCommandObject()
        {
            return new SqlCommand();
        }

        public DbCommandBuilder CreateCommandBuilder()
        {
            return new SqlCommandBuilder();
        }

        public DbDataAdapter CreateDataAdapter()
        {
            return new SqlDataAdapter();
        }

        public enmDBAProvider enmDBType { get { return enmDBAProvider.MS; } }

    }

    #endregion

    #region OracleDB Provider

    public class OracleDBConnector : DBConnector
    {
        public OracleDBConnector(string ConnectionString, ILog Logger = null, int? DefaultTimeout = null) : base(new OracleDBProvider(), ConnectionString, Logger, DefaultTimeout)
        {

        }
    }

    public class OracleDBProvider : IDBProvider
    {
        public DbConnection CreateConnectionObject()
        {
            return new OracleConnection();
        }

        public DbCommand CreateCommandObject()
        {
            return new OracleCommand();
        }

        public DbCommandBuilder CreateCommandBuilder()
        {
            return new OracleCommandBuilder();
        }

        public DbDataAdapter CreateDataAdapter()
        {
            return new OracleDataAdapter();
        }

        public enmDBAProvider enmDBType { get { return enmDBAProvider.Oracle; } }

    }

    #endregion

    #region DB Provider

    #endregion
}
