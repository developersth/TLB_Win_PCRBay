using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oracle.DataAccess.Client;
using System.Threading;
using System.Data;

namespace PCRBay
{
    public delegate void EventHandler();


    public class cOracle : IDisposable
    {
        frmCRBay mFMercury;
        private String mPathIni = "D:\\SAKCTAS\\SAKCTASConfig.ini";
        private IniLib.CINI mIni;

        #region Enum Database
        public enum DB_TYPE
        {
            DB_None = -1,
            DB_MASTER = 0,
            DB_SUBMASTER = 1
        }

        public enum _OracleDbDirection
        {
            OraInput,
            OraOutput
        }

        public enum _OracleDbType
        {
            OraVarchar2,
            OraInt16,
            OraInt32,
            OraInt64,
            OraDate,
            OraLong,
            OraDouble,
            OraSingle,
            OraByte,
            OraDecimal,
            OraBlob
        }
        #endregion

        public sealed class _ParamMember 
        {
            public string Name;
            public object Value;
            public Int32 Size;
            public _OracleDbDirection Direction;
            public _OracleDbType DbType;
        }

        //public sealed OracleParameter[] OParamMember;
        //_ParamMember[] _OraParam;
        DB_TYPE mCurrentDB;
        bool mIsConnectedDB;
        bool mIsMasterDB;

        int mCount;
        bool mConnect;
        bool mShutdown;
        string mCnnStrMaster = "User Id=tas;Password=gtas;Data Source=SAKCTASA";
        string mCnnStrSubMaster = "User Id=tas;Password=gtas;Data Source=SAKCTASB";
        public OracleConnection mConnOracle;
        Thread mThread;
        bool mRunning;
        bool mRunn;
        clogfile mLog;
        
        int mTotalParam;

        public OracleDataReader mOraDysReader;

        #region construct and deconstruct
            private bool IsDisposed = false;
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
                mRunn = false;
                mIni = null;
            }
            protected void Dispose(bool Diposing)
            {
                if (!IsDisposed)
                {
                    if (Diposing)
                    {
                        //Clean Up managed resources
                        Close();
                        mIni = null;
                        mRunn = false;
                        //PLog = null;

                        //fMain = null;
                    }
                    //Clean up unmanaged resources
                    mRunn = false;
                    mThread.Abort();
                }
                IsDisposed = true;
            }
            public cOracle(frmCRBay f)
            {
                //fMain = new frmMain();
                mFMercury = f;
                //mConnOracle = new OracleConnection();
                mLog = new clogfile();
                mIni = new IniLib.CINI();
                mCurrentDB = DB_TYPE.DB_None;
                //ScanDatabase();
                StartThread();

            }
            public cOracle()
            { }
            ~cOracle()
            {
                //Dispose(false);
                //mConnOracle = null;

                //Close();
                //PLog = null;
                //fMain = null; 
            }
        #endregion

        public bool ConnectStatus()
        {
            return mConnect;
        }

        private void StartThread()
        {
            mRunn = true;
            try
            {
                if (mRunning)
                {
                    return; 
                }
                //if (mThread != null)
                //    mThread = null;
                
                mThread = new Thread(this.ScanDatabase);
                mRunning = true;
                mThread.Name = "cOracle";
                mThread.Start();
            }
            catch (Exception exp)
            {
                mRunning = false;
            }
            
        }

        private void RunProcess()
        {
            //Thread.Sleep(500);
            while (true)
            {
                if (mCurrentDB == DB_TYPE.DB_None)
                {
                    mCurrentDB = GetSelectServerDb();
                }
                if (mCurrentDB != DB_TYPE.DB_None)
                {
                    //Reconnect();
                    //if (mConnect || mShutdown)
                    if (mConnect || !mRunn)
                    {
                        break;
                    }
                    Reconnect();
                }
                System.Threading.Thread.Sleep(10000);
            }
            Thread.Sleep(1000);
            mRunning = false;
        }

        private void Addlistbox(string pMsg)
        {
            try
            {
                mFMercury.AddListBox = (object)DateTime.Now + pMsg;
            }
            catch (Exception exp)
            { }
        }

        void Reconnect()
        {
            if (!mConnect)
            {
                try
                {
                    mConnOracle.Close();
                }
                catch (Exception exp)
                { }
                Connect();
            }
        }

        bool Connect()
        {
            string vMsg;
            //mConnOracle = new OracleConnection(mCnnStrMaster);
            switch (mCurrentDB)
            {
                case DB_TYPE.DB_MASTER:
                    //vMsg = ">Database Connect-> Master";
                    mConnOracle = new OracleConnection(mCnnStrMaster);
                    break;
                case DB_TYPE.DB_SUBMASTER:
                    //vMsg = ">Database Connect-> Submaster";
                    mConnOracle = new OracleConnection(mCnnStrSubMaster);
                    break;
                default:
                    //vMsg = ">Database Connect-> NONE";
                    break;
            }
            //Addlistbox(vMsg);
            try
            {
                mConnOracle.Open();
                mConnect = true;
                mLog = new clogfile();
                Addlistbox(">" + GetConnectDBName());
                Addlistbox(">Database connect successful.");
                return  true;
            }
            catch (Exception exp)
            {
                mConnect = false;
                mRunn = false;
                StartThread();
                return false;
            } 
        }

        public void Close()
        {
            mShutdown = true;
            Thread.Sleep(500);
            if (mConnOracle != null)
            {
                try
                {
                    mConnOracle.Close();
                    mConnOracle = null;
                    //fMercury = null;
                    mLog = null;
                }
                catch (Exception exp) { }
            }
        }

        void CheckExecute(bool pExe)
        {
            if (pExe)
            {
                mCount = 0;
                if (!mConnect)
                    mConnect = true;
            }
            else
            {
                mCount += 1;
                if (mCount >= 3)
                {
                    if (mConnect)
                    {
                        mConnect = false;
                        StartThread();
                    }
                    mCount = 0;
                }
            }
        }

        public bool OpenDyns(string pStrSQL, string pTableName, ref DataSet pDataSet)
        {
            OracleDataAdapter oda;
            DataSet ds = new DataSet();
            bool vCheck = false;
            if (mConnect)
            {
                try
                {
                    oda = new OracleDataAdapter(pStrSQL, mConnOracle);
                    oda.Fill(ds, pTableName);
                    pDataSet = ds;
                    CheckExecute(true);
                    vCheck = true;
                }
                catch (Exception exp)
                {
                    CheckExecute(vCheck);
                    clogfile p = new clogfile();
                    mLog.WriteErrLog("Cannot open [" + pStrSQL + "]");
                    mLog.WriteErrLog(exp.Message);

                }
                ds = null;
                oda = null;
            }
            return vCheck;
        }

        public bool OpenDyns(string pStrSQL,int pMaxRecord, string pTableName, ref DataSet pDataSet)
        {
            OracleDataAdapter oda;
            DataSet ds = new DataSet();
            bool bCheck = false;
            if (mConnect)
            {
                try
                {
                    oda = new OracleDataAdapter(pStrSQL, mConnOracle);
                    oda.Fill(ds,0,pMaxRecord, pTableName);
                    pDataSet = ds;
                    CheckExecute(true);
                    bCheck = true;
                }
                catch (Exception exp)
                {
                    bCheck = false;
                    CheckExecute(bCheck);
                    mLog.WriteErrLog("[OpenDyns]" + pStrSQL);
                    mLog.WriteErrLog("[OpenDyns]" + exp.Message);
                }
                ds = null;
                oda = null;
            }
            return bCheck;
        }

        public bool ExecuteSQL(string pStrSQL)
        {
            OracleCommand oCommand;
            bool bCheck = false;

            if (mConnect)
            {
                try
                {
                    bCheck = true;
                    oCommand = new OracleCommand(pStrSQL, mConnOracle);
                    oCommand.ExecuteNonQuery();
                    CheckExecute(true);
                }
                catch (Exception exp)
                {
                    bCheck = false;
                    CheckExecute(false);
                    mLog.WriteErrLog("[ExecuteSQL]" + pStrSQL);
                    mLog.WriteErrLog("[ExecuteSQL]" + exp.Message);
                }
            }
            return bCheck;
        }

        public bool ExecuteSQL_PROC(string pStrSQL, cOracleParameter pParam)
        {
            OracleCommand oCommand;
            bool vCheck = false;
            int vParamNo;
            if (mConnect)
            {
                try
                {
                    oCommand = new OracleCommand();
                    if (pParam == null)
                        return vCheck;

                    foreach (OracleParameter p in pParam._OraParam)
                    {
                        oCommand.Parameters.Add(p);
                    }
                    oCommand.CommandText = pStrSQL;
                    oCommand.CommandType = CommandType.StoredProcedure;
                    oCommand.Connection = mConnOracle;
                    oCommand.ExecuteNonQuery();

                    vCheck = true;
                    CheckExecute(true);
                }
                catch (Exception exp)
                {
                    vCheck = false;
                    CheckExecute(false);
                    mLog.WriteErrLog("[ExecuteSQL]" + pStrSQL);
                    mLog.WriteErrLog("[ExecuteSQL]" + exp.Message);
                }
            }
            return vCheck;
        }

        public bool ExecuteSQL(string pStrSQL, cOracleParameter pParam)
        {
            OracleCommand oCommand;
            bool vCheck = false;
            int vParamNo;
            if (mConnect)
            {
                try
                {
                    oCommand = new OracleCommand();
                    if (pParam == null)
                        return vCheck;

                    foreach(OracleParameter p in pParam._OraParam)
                    {
                        oCommand.Parameters.Add(p);
                    }
                    oCommand.CommandText = pStrSQL;
                    oCommand.CommandType = CommandType.Text;
                    oCommand.Connection = mConnOracle;
                    oCommand.ExecuteNonQuery();

                    vCheck = true;
                    CheckExecute(true);
                }
                catch (Exception exp)
                {
                    vCheck = false;
                    CheckExecute(false);
                    mLog.WriteErrLog("[ExecuteSQL]" + pStrSQL);
                    mLog.WriteErrLog("[ExecuteSQL]" + exp.Message);
                }  
            }
            return vCheck;
        }

        void GetOracleDbType(cOracle._OracleDbType pOracleDbType,ref Oracle.DataAccess.Client.OracleDbType pDAOracleDbType)
        {
            if (pOracleDbType == _OracleDbType.OraByte) 
                pDAOracleDbType = OracleDbType.Byte;
            else if (pOracleDbType == _OracleDbType.OraBlob) 
                pDAOracleDbType = OracleDbType.Blob;
            else if (pOracleDbType == _OracleDbType.OraDate)
                pDAOracleDbType = OracleDbType.Date;
            else if (pOracleDbType == _OracleDbType.OraDecimal)
                pDAOracleDbType = OracleDbType.Decimal;
            else if (pOracleDbType == _OracleDbType.OraDouble)
                pDAOracleDbType = OracleDbType.Double;
            else if (pOracleDbType == _OracleDbType.OraInt16)
                pDAOracleDbType = OracleDbType.Int16;
            else if (pOracleDbType == _OracleDbType.OraInt32)
                pDAOracleDbType = OracleDbType.Int32;
            else if (pOracleDbType == _OracleDbType.OraInt64)
                pDAOracleDbType = OracleDbType.Int64;
            else if (pOracleDbType == _OracleDbType.OraLong)
                pDAOracleDbType = OracleDbType.Long;
            else if (pOracleDbType == _OracleDbType.OraSingle)
                pDAOracleDbType = OracleDbType.Single;
            else if (pOracleDbType == _OracleDbType.OraVarchar2)
                pDAOracleDbType = OracleDbType.Varchar2;
        }

        void GetOracleDbDirection(_OracleDbDirection pOracleDbDirection ,ref System.Data.ParameterDirection pDAOracleDbDirection)
        {
            if (pOracleDbDirection == _OracleDbDirection.OraInput)
                pDAOracleDbDirection = ParameterDirection.Input;
            else if (pOracleDbDirection == _OracleDbDirection.OraOutput)
                pDAOracleDbDirection = ParameterDirection.Output;
        }

        #region "ChangeDatabaseServer"
        public void ScanDatabase()
        {
            while (!mShutdown)
            {
                //if (mConnect || !mRunn)
                //{
                //    break;
                //}
                DB_TYPE NewDB = GetSelectServerDb();
                if (mCurrentDB != NewDB)
                {
                    mConnect = false;
                    mCurrentDB = NewDB;
                    if (mCurrentDB != DB_TYPE.DB_None)
                    {
                        Reconnect();
                    }
                }
                else
                {
                    if (mCurrentDB != DB_TYPE.DB_None)
                    {
                        mIsConnectedDB = GetConnectServer(mCurrentDB);
                        mIsMasterDB = GetIsMaster(mCurrentDB);
                        if (!mIsConnectedDB)
                        {
                            mCurrentDB = DB_TYPE.DB_None;
                            mConnect = false;
                            Reconnect();
                        }
                    }
                }
                Thread.Sleep(5000);
            }
        }
        public string GetConnectDBName()
        {
            string ret = "";
            switch (mCurrentDB)
            {
                case DB_TYPE.DB_None:
                    ret = "Connect None";
                    break;
                case DB_TYPE.DB_MASTER:
                    ret = "Connect MASTER";
                    break;
                case DB_TYPE.DB_SUBMASTER:
                    ret = "Connect SUBMASTER";
                    break;
            }
            return ret;
        }

        DB_TYPE GetSelectServerDb()
        {
            string ret = mIni.INIRead(mPathIni, "SELECT", "SERVER", "");

            switch (Convert.ToInt16(ret))
            {
                case 0:
                    return DB_TYPE.DB_MASTER;
                case 1:
                    return DB_TYPE.DB_SUBMASTER;
            }
            return DB_TYPE.DB_None;
        }

        bool GetConnectServer(DB_TYPE pServer)
        {
            string ret="0";
            switch (pServer)
            {
                case DB_TYPE.DB_None:
                    ret = "0";
                    break;
                case DB_TYPE.DB_MASTER:
                    ret = mIni.INIRead(mPathIni, "MASTER", "CONNECT", "");
                    break;
                case DB_TYPE.DB_SUBMASTER:
                    ret = mIni.INIRead(mPathIni, "SUBMASTER", "CONNECT", "");
                    break;
            }
            return Convert.ToBoolean(Convert.ToInt16(ret));
        }

        bool GetIsMaster(DB_TYPE pServer)
        {
            string ret="0";
            switch (pServer)
            {
                case DB_TYPE.DB_None:
                    ret = "0";
                    break;
                case DB_TYPE.DB_MASTER:
                    ret = mIni.INIRead(mPathIni, "MASTER", "ISMASTER", "");
                    break;
                case DB_TYPE.DB_SUBMASTER:
                    ret = mIni.INIRead(mPathIni, "SUBMASTER", "ISMASTER", "");
                    break;
            }
            return Convert.ToBoolean(Convert.ToInt16(ret));
        }
        #endregion
	}
    public class cOracleParameter
    {
        #region "Oracle Parameter"
        public  Oracle.DataAccess.Client.OracleParameter[] _OraParam;

        public OracleParameter mParm;

        public void CreateParameter(int pLength)
        {
            _OraParam = new OracleParameter[pLength];
            for (int i = 0; i < _OraParam.Length; i++)
            {
                _OraParam[i] = new OracleParameter();
            }
        }

        public ParameterDirection OraDirection;
        public OracleDbType OraDbType;

        public void AddParameter(int pIndex, string pName, OracleDbType pDbType, ParameterDirection pDbDirection,int pSize )
        {
            if (pIndex < _OraParam.Length )
            {
                _OraParam[pIndex].ParameterName = pName;
                _OraParam[pIndex].OracleDbType = pDbType;
                _OraParam[pIndex].Size = pSize;
                _OraParam[pIndex].Direction = pDbDirection;
            }
        }

        public void AddParameter(int pIndex, string pName, OracleDbType pDbType, ParameterDirection pDbDirection)
        {
            if (pIndex < _OraParam.Length )
            {
                _OraParam[pIndex].ParameterName = pName;
                _OraParam[pIndex].OracleDbType = pDbType;
                _OraParam[pIndex].Direction = pDbDirection;
                _OraParam[pIndex].Size = 512;
            }
        }

        public void AddParameter(int pIndex, string pName, OracleDbType pDbType,int pSize)
        {
            if (pIndex < _OraParam.Length)
            {
                _OraParam[pIndex].ParameterName = pName;
                _OraParam[pIndex].OracleDbType = pDbType;
                _OraParam[pIndex].Direction = ParameterDirection.Output;
                _OraParam[pIndex].Size = pSize;
            }
        }

        public void AddParameter(int pIndex, string pName,OracleDbType pDbType)
        {
            if (pIndex < _OraParam.Length)
            {
                _OraParam[pIndex].ParameterName = pName;
                _OraParam[pIndex].OracleDbType = pDbType;
                _OraParam[pIndex].Direction = ParameterDirection.Output;
                _OraParam[pIndex].Size = 512;
            }
        }

        public void RemoveParameter()
        {
            for (int i = 0; i < _OraParam.Length - 1; i++)
            {
                _OraParam[i].Dispose();
            }
            _OraParam = null;
        }

        public void SetParameterValue(int pIndex, object pValue)
        {
            _OraParam[pIndex].Value = pValue;
        }

        public void SetParameterValue(string pName, ref OracleParameter pValue)
        {
            for (int i = 0; i < _OraParam.Length - 1; i++)
            {
                if (_OraParam[i].ParameterName == pName)
                {
                    _OraParam[i].Value = pValue;
                    break;
                }
            }
        }

        public void GetParameterValue(int pIndex, ref OracleParameter pParam)
        {
            pParam= _OraParam[pIndex];
        }

        public void GetParameterValue(string pName,ref OracleParameter pParam)
        {
            foreach (OracleParameter p in _OraParam)
            {
                if (p.ParameterName == pName)
                {
                    pParam = p;
                    return;
                }
            }
        }

        public OracleParameter GetParameter(int pIndex)
        {
            return _OraParam[pIndex];
        }

        public OracleParameter GetParameter(string pName)
        {
            foreach (OracleParameter p in _OraParam)
            {
                if (p.ParameterName == pName)
                {
                    return p;
                }
            }
            return null;
        }

        #endregion
    }

}
