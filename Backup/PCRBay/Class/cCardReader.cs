using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oracle.DataAccess.Client;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PCRBay
{
    public class cCardReader : IDisposable
    {
        enum LoadingStep :int
        {
            SystemOffLine=99
            ,DisplayDateTime=97
            ,LoadingNone=0
            ,NoDataFound = -1
            ,EnterCard = 11
            ,LoadingCancel = 2
            ,LoadingCompartment=3
            ,DisplayCompartment = 30
            //,EnterCompartment = 31
            ,ShowPreset = 31
            ,LoadingStart=51
            ,LoadingLoad=53
            ,LoadingStop=54
            ,LoadingComplete=98         //n/a
            ,OperatorConfirm=55
            ,DriverConfirm=61
        }
        int mSTX_Position, mETX_Position;
        CMercuryLib.MercuryLib mMercuryLib;
        frmCRBay mFMercury;
        cPorts mPort;
        clogfile mLog;
        public struct _CardReader
        {
            public bool IsKeypad;
            public bool IsEnable;
            public bool IsProcess;
            public bool IsLock;
            public string DataSend;
            public string DataReceive;
            public string ID;
            public int Address;
            public string Name;
            public string BayNo;
            public string MeterName;
            public int CompartmentNo;
            public string CardCode;
            public bool Connect;
            public bool IsAlarm;
            //public string CompartmentList;
            public int ModeStatus;   // ตรวจสอบโหมด Load หรือ Load
            public int Tmode;

            public DateTime DateTimeStart;
            public DateTime TimeSend;
            public bool bTimeOut;

            public int RET_BATCH_STATUS;
            public string RET_LOADED_MASS;
            public string RET_FLOWRATE;
            public string RET_UNIT;
            public int RET_LOAD_STATUS;
            public int RET_STEP;
            public Double RET_LOAD_HEADER;
            public Double RET_LOAD_LINE;
            public int RET_CHECK;
            public string RET_MSG;
            public string RET_CR_MSG;
            public int RET_LOAD_TYPE;       //1=bay in, 2=cancel
            public int RET_LOAD_COUNT;
            public int RET_RECIPES_NO;
            public int RET_PRESET;
            public double RET_DENSITY30C;
            public double RET_VCF30;
            public string RET_MSG_BATCH1  ;
            public string RET_MSG_BATCH2  ;
            public string RET_COMPARTMENT_LIST;
            public int RET_TOT_COMPARTMENT;
            public string RET_METER_NO;
            public string RET_METER_NAME;
            public string RET_TU_ID;        //ทะเบียนรถ
            public string RET_SALE_PRODUCT_NAME;
            public string RET_SEAL_USE;
            public string RET_SEAL_NUMBER;
            public int RET_IS_BLENDING;
            public int RET_TOPUP_NO;   //หมายเลข Topup
            public int RET_BATCH_NO;
           // public string RET_DESITY30C;
            public int RET_VCF30C;
            

        }
        public _CardReader mCR_NewValue;
        _CardReader mCR_OldValue;
        DateTime mResponseTime;
        bool mResponse;
        cCRProcess mCRProcess;
        object mThreadLock = new object();
        int mCount;

        #region construct and deconstruct
        private bool IsDisposed = false;
        public void Dispose()
        {
            mRunn = false;
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected void Dispose(bool Diposing)
        {
            if (!IsDisposed)
            {
                if (Diposing)
                {
                    //Clean Up managed resources
                    mRunn = false;
                    DisplayToCardReader(99);
                    mThreadLock = null;
                    mShutdown = true;
                    mCR_NewValue.RET_STEP = 0;
                    //hread.Sleep(10);
                    if ((mThread != null) && (mThread.ThreadState == System.Threading.ThreadState.Running))
                        mThread.Abort();

                    //DisplayToCardReader(99);
                    //Thread.Sleep(10);
                    //PPort = null;
                    mLog = null;
                    mMercuryLib = null;

                }
                //Clean up unmanaged resources
                mRunn = false;
            }
            IsDisposed = true;
        }
        public cCardReader(frmCRBay f, cCRProcess pCRProcess)
        {
            mFMercury = f;
            mLog = new clogfile();
            mMercuryLib = new CMercuryLib.MercuryLib();
            mCRProcess = pCRProcess;
        }
        ~cCardReader()
        {
            mShutdown = true;
            mPort = null;
            mLog = null;
            mMercuryLib = null;
        }
        #endregion

        #region "Thread"
        bool mConnect;
        bool mShutdown;
        bool mRunning;
        bool mRunn;
        public Thread mThread;

        public void StartThread()
        {
            mRunn = true;
            mRunning = false;

            //if ((mThread != null) && (mThread.ThreadState != ThreadState.Aborted))
            //    mThread.Abort();

            mThread = null;
            Thread.Sleep(1000);
            try
            {
                mResponseTime = DateTime.Now;
                mCR_NewValue.TimeSend = DateTime.Now;
                mCR_OldValue.TimeSend = DateTime.Now;
                if (mRunning)
                {
                    return;
                }
                mThread = new Thread(this.RunProcess);
                mRunning = true;
                mThread.Name = mCR_NewValue.Name;
                mThread.Start();
            }
            catch (Exception exp)
            {
                mLog.WriteErrLog(exp.Message);
                mRunning = false;
            }

        }

        public void StartThread(ref cPorts p)
        {
            mRunn = true;
            mPort = p;
            Thread.Sleep(1000);
            try
            {
                mResponseTime = DateTime.Now;
                if (mRunning)
                {
                    return;
                }
                mThread = new Thread(this.RunProcess);
                mRunning = true;
                mThread.Name = mCR_NewValue.Name;
                mThread.Start();
            }
            catch (Exception exp)
            {
                mLog.WriteErrLog(exp.Message);
                mRunning = false;
            }

        }

        private void RunProcess()
        {
            try
            {
                lock (mThreadLock)
                {
                    //Thread.Sleep(1000);

                    //while (!mShutdown)
                    while (mRunn)
                    {
                        
                        if (mShutdown)
                            return;
                        if (mCR_NewValue.Connect)
                        {
                            SendToCardReader(mMercuryLib.MakeCursorInvisible());
                            SendToCardReader(mMercuryLib.SetKeyToNum());
                            SendToCardReader(mMercuryLib.DeleteAllStoreMessage());
                            SendToCardReader(mMercuryLib.SendNextQueueBlock(), false);
                            SendToCardReader(mMercuryLib.SendNextQueueBlock(), false);
                            SendToCardReader(mMercuryLib.SendNextQueueBlock(), false);
                            SendToCardReader(mMercuryLib.SendNextQueueBlock(), false);
                            SendToCardReader(mMercuryLib.ClearDisplay());
                            SendToCardReader(mMercuryLib.MakeCursorInvisible());
                            M_CRBAY_CHECK_TOPUP();
                            //MessageBox.Show(mCR_NewValue.ModeStatus.ToString());
                           // mCR_NewValue.Tmode = mCR_NewValue.ModeStatus;
                            if (mCR_NewValue.ModeStatus == 0)
                            {
                                CRBayLoading();
                            }
                            else
                            {
                                CRBayLoading_Topup();
                            }
                           
                            
                        }
                        else
                        {
                            try
                            {
                                if (mPort.IsOpen())
                                {
                                    string s = "";
                                    SendToCardReader(mMercuryLib.SendNextQueueBlock(),true);
                                    //Thread.Sleep(300);
                                    //ReadFromCardReader();
                                }
                            }
                            catch (Exception exp)
                            { }
                        }
                        Thread.Sleep(3000);
                    }
                    //ClosePort();

                    mRunning = false;
                    //break;
                }
            }
            finally
            { 
                //this.Dispose(); 
                mShutdown = true;
                mRunn = false;
            }

        }
        #endregion

        #region "Database"
        private void UPDATE_CARDREADER_CONNECT()
        {
            string strSQL = "begin tas.UPDATE_CARDREADER_CONNECT(" +
                            mCR_NewValue.ID + "," + Convert.ToInt16(mCR_NewValue.Connect) +
                            ");end;";
            mFMercury.mOraDb.ExecuteSQL(strSQL);

            Addlistbox("connect=" + mCR_NewValue.Connect.ToString());
        }

        private bool M_CRBAY_CHECK_STEP()
        {
            bool bCheck=false;
            string strSQL = "begin load.M_CRBAY_CHECK_STEP(" +
                            + Convert.ToUInt64(mCR_NewValue.CardCode) + "," + mCR_NewValue.BayNo + "," + mCR_NewValue.ID +
                            ",:RET_STEP,:RET_LOAD_HEADER_NO,:RET_LOAD_STATUS,:RET_COMPARTMENT_LIST,:RET_TOT_COMPARTMENT" +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG,:RET_TU_ID,:RET_METER_NO,:RET_SALE_PRODUCT_NAME" +
                            ",:RET_SEAL_USE,:RET_SEAL_NUMBER,:RET_IS_BLENDING" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(14);

            OraParam.AddParameter(0, "RET_STEP", Oracle.DataAccess.Client.OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_LOAD_HEADER_NO", Oracle.DataAccess.Client.OracleDbType.Int32);
            OraParam.AddParameter(2, "RET_LOAD_STATUS", Oracle.DataAccess.Client.OracleDbType.Int16);
            OraParam.AddParameter(3, "RET_COMPARTMENT_LIST", Oracle.DataAccess.Client.OracleDbType.Varchar2,512);
            OraParam.AddParameter(4, "RET_TOT_COMPARTMENT", Oracle.DataAccess.Client.OracleDbType.Int16);
            OraParam.AddParameter(5, "RET_CHECK", Oracle.DataAccess.Client.OracleDbType.Int16);
            OraParam.AddParameter(6, "RET_MSG", Oracle.DataAccess.Client.OracleDbType.Varchar2, 512);
            OraParam.AddParameter(7, "RET_CR_MSG", Oracle.DataAccess.Client.OracleDbType.Varchar2, 512);
            OraParam.AddParameter(8, "RET_TU_ID", Oracle.DataAccess.Client.OracleDbType.Varchar2, 512);
            OraParam.AddParameter(9, "RET_METER_NO", Oracle.DataAccess.Client.OracleDbType.Varchar2, 64);
            OraParam.AddParameter(10, "RET_SALE_PRODUCT_NAME", Oracle.DataAccess.Client.OracleDbType.Varchar2, 64);
            OraParam.AddParameter(11, "RET_SEAL_USE", Oracle.DataAccess.Client.OracleDbType.Varchar2, 64);
            OraParam.AddParameter(12, "RET_SEAL_NUMBER", Oracle.DataAccess.Client.OracleDbType.Varchar2, 128);
            OraParam.AddParameter(13, "RET_IS_BLENDING", Oracle.DataAccess.Client.OracleDbType.Int16);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_STEP", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_STEP = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_STEP = 0;

                OraParam.GetParameterValue("RET_LOAD_HEADER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_HEADER = Convert.ToInt32(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_HEADER = 0;

                OraParam.GetParameterValue("RET_LOAD_STATUS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_STATUS = Convert.ToInt32(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_HEADER = 0;

                OraParam.GetParameterValue("RET_COMPARTMENT_LIST", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_COMPARTMENT_LIST = p.Value.ToString();
                else
                    mCR_NewValue.RET_COMPARTMENT_LIST = "???";

                OraParam.GetParameterValue("RET_TOT_COMPARTMENT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_TOT_COMPARTMENT = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_TOT_COMPARTMENT = 0;

                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = -1;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "???";

                OraParam.GetParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CR_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_CR_MSG = "";

                OraParam.GetParameterValue("RET_TU_ID", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_TU_ID = p.Value.ToString();
                else
                    mCR_NewValue.RET_TU_ID = "";

                OraParam.GetParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_METER_NO = p.Value.ToString();
                else
                    mCR_NewValue.RET_METER_NO = "???";

                OraParam.GetParameterValue("RET_SALE_PRODUCT_NAME", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_SALE_PRODUCT_NAME = p.Value.ToString();
                else
                    mCR_NewValue.RET_SALE_PRODUCT_NAME = "???";

                OraParam.GetParameterValue("RET_SEAL_USE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_SEAL_USE = p.Value.ToString();
                else
                    mCR_NewValue.RET_SEAL_USE = "???";

                OraParam.GetParameterValue("RET_SEAL_NUMBER", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_SEAL_NUMBER = p.Value.ToString();
                else
                    mCR_NewValue.RET_SEAL_NUMBER = "???";

                OraParam.GetParameterValue("RET_IS_BLENDING", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_IS_BLENDING = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_IS_BLENDING = 0;
                
                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                Addlistbox(mCR_NewValue.RET_MSG);
                if(mCR_NewValue.RET_CR_MSG !="")
                    Addlistbox(mCR_NewValue.RET_CR_MSG);
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_CRBAY_TOPUP_CHECK_STEP()
        {
            bool bCheck = false;
            string strSQL = "begin load.M_CRBAY_TOPUP_CHECK_STEP(" +
                            +Convert.ToUInt64(mCR_NewValue.CardCode) + "," + mCR_NewValue.BayNo + ",'" + mCR_NewValue.RET_METER_NO+"',"+mCR_NewValue.RET_TOPUP_NO+
                            ",:RET_STEP,:RET_LOAD_HEADER_NO,:RET_COMPARTMENT_NO" +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(6);

            OraParam.AddParameter(0, "RET_STEP", Oracle.DataAccess.Client.OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_LOAD_HEADER_NO", Oracle.DataAccess.Client.OracleDbType.Int32);
            OraParam.AddParameter(2, "RET_COMPARTMENT_NO", Oracle.DataAccess.Client.OracleDbType.Int16);
            OraParam.AddParameter(3, "RET_CHECK", Oracle.DataAccess.Client.OracleDbType.Int16);
            OraParam.AddParameter(4, "RET_MSG", Oracle.DataAccess.Client.OracleDbType.Varchar2, 512);
            OraParam.AddParameter(5, "RET_CR_MSG", Oracle.DataAccess.Client.OracleDbType.Varchar2, 512);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_STEP", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_STEP = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_STEP = 0;

                OraParam.GetParameterValue("RET_LOAD_HEADER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_HEADER = Convert.ToInt32(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_HEADER = 0;

                OraParam.GetParameterValue("RET_COMPARTMENT_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.CompartmentNo = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.CompartmentNo = 0;


                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = -1;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                OraParam.GetParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CR_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_CR_MSG = "";

                Addlistbox(mCR_NewValue.RET_MSG);
                if (mCR_NewValue.RET_CR_MSG != "")
                    Addlistbox(mCR_NewValue.RET_CR_MSG);
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;

        }

        private bool M_CRBAY_LOAD_CHECK_TU()
        {
            
            bool bCheck = false;
            string strSQL = "begin load.M_CRBAY_LOAD_CHECK_TU(" +
                            Convert.ToInt64(mCR_NewValue.CardCode) + "," + mCR_NewValue.ID + ",0,0," + mCR_NewValue.BayNo + ",' ',' '" +
                            ",:RET_LOAD_TYPE,:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(4);

            OraParam.AddParameter(0, "RET_LOAD_TYPE", OracleDbType.Int32);
            OraParam.AddParameter(1, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddParameter(2,"RET_MSG",OracleDbType.Varchar2,512);
            OraParam.AddParameter(3, "RET_CR_MSG", OracleDbType.Varchar2, 512);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {

                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;
                //if (CR.RET_CHECK != -1)
                //{
                    OraParam.GetParameterValue("RET_LOAD_TYPE", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        mCR_NewValue.RET_LOAD_TYPE = Convert.ToInt16(p.Value.ToString());
                    else
                        mCR_NewValue.RET_LOAD_TYPE = 0;

                    OraParam.GetParameterValue("RET_MSG", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        mCR_NewValue.RET_MSG = p.Value.ToString();
                    else
                        mCR_NewValue.RET_MSG = "";

                    OraParam.GetParameterValue("RET_CR_MSG", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        mCR_NewValue.RET_CR_MSG = p.Value.ToString();
                    else
                        mCR_NewValue.RET_CR_MSG = "";

                    Addlistbox(mCR_NewValue.RET_MSG);
                    if(mCR_NewValue.RET_CR_MSG!="")
                        Addlistbox(mCR_NewValue.RET_CR_MSG);
               // }
                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }
        private bool M_CRBAY_LOAD_CHECK_TU_TOPUP()
        {

            bool bCheck = false;
            string strSQL = "begin load.M_CRBAY_LOAD_CHECK_TU_TOPUP(" +
                            Convert.ToInt64(mCR_NewValue.CardCode) + "," + mCR_NewValue.ID + ",0,0," + mCR_NewValue.BayNo + ",' ',' '" +
                            ",:RET_LOAD_TYPE,:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(4);

            OraParam.AddParameter(0, "RET_LOAD_TYPE", OracleDbType.Int32);
            OraParam.AddParameter(1, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddParameter(2, "RET_MSG", OracleDbType.Varchar2, 512);
            OraParam.AddParameter(3, "RET_CR_MSG", OracleDbType.Varchar2, 512);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {

                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;
                //if (CR.RET_CHECK != -1)
                //{
                OraParam.GetParameterValue("RET_LOAD_TYPE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_TYPE = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_TYPE = 0;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                OraParam.GetParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CR_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_CR_MSG = "";

                Addlistbox(mCR_NewValue.RET_MSG);
                if (mCR_NewValue.RET_CR_MSG != "")
                    Addlistbox(mCR_NewValue.RET_CR_MSG);
                // }
                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }
        private bool M_CRBAY_TOPUP_LOAD_CHECK_TU()
        {

            bool bCheck = false;
            string strSQL = "begin load.M_CRBAY_TOPUP_LOAD_CHECK_TU(" +
                            mCR_NewValue.BayNo + ",' ',' '," + mCR_NewValue.RET_TOPUP_NO +
                             ",:RET_CHECK" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(1);

            OraParam.AddParameter(0, "RET_CHECK", OracleDbType.Int32);
      
            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;

                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }

     
            return bCheck;
        }
        private bool M_CRBAY_CHECK_COMPARTMENT()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_CHECK_COMPARTMENT(" +
                            mCR_NewValue.BayNo + "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.CompartmentNo +
                            ",:RET_BATCH_NO,:RET_LOAD_COUNT,:RET_RECIPES_NO,:RET_PRESET" +
                            ",:RET_DESITY30C,:RET_VCF30" +
                            ",:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2,:RET_METER_NO,:RET_SALE_PRODUCT_NAME" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(12);

            OraParam.AddParameter(0, "RET_BATCH_NO", OracleDbType.Int32);
            OraParam.AddParameter(1, "RET_LOAD_COUNT", OracleDbType.Int16);
            OraParam.AddParameter(2, "RET_RECIPES_NO", OracleDbType.Int16);
            OraParam.AddParameter(3, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddParameter(4, "RET_DENSITY30C", OracleDbType.Varchar2,64);
            OraParam.AddParameter(5, "RET_VCF30", OracleDbType.Single);
            OraParam.AddParameter(6, "RET_CHECK", OracleDbType.Varchar2,64);
            OraParam.AddParameter(7, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(8, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(9, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(10, "RET_METER_NO", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(11, "RET_SALE_PRODUCT_NAME", OracleDbType.Varchar2, 128);


            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_BATCH_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_LINE = Convert.ToInt32(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_LINE = 0;

                OraParam.GetParameterValue("RET_LOAD_COUNT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_COUNT = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_COUNT = 0;

                OraParam.GetParameterValue("RET_RECIPES_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_RECIPES_NO = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_RECIPES_NO = 0;

                OraParam.GetParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_PRESET = 0;

                OraParam.GetParameterValue("RET_DENSITY30C", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_DENSITY30C = Convert.ToDouble(p.Value.ToString());
                else
                    mCR_NewValue.RET_DENSITY30C = 0;

                OraParam.GetParameterValue("RET_VCF30", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_VCF30 = Convert.ToDouble(p.Value.ToString());
                else
                    mCR_NewValue.RET_VCF30 = 0;

                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                OraParam.GetParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH1 = "";

                OraParam.GetParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH2 = "";

                OraParam.GetParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_METER_NO = p.Value.ToString();
                else
                    mCR_NewValue.RET_METER_NO = "";

                OraParam.GetParameterValue("RET_SALE_PRODUCT_NAME", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_SALE_PRODUCT_NAME = p.Value.ToString();
                else
                    mCR_NewValue.RET_SALE_PRODUCT_NAME = "";

                Addlistbox(mCR_NewValue.RET_MSG);
                if (mCR_NewValue.RET_CHECK == -1)
                {
                    Addlistbox(mCR_NewValue.RET_MSG_BATCH1 + " " + mCR_NewValue.RET_MSG_BATCH2);
                }
                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }
        private bool M_CRBAY_CHECK_COMP_TOPUP()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_CHECK_COMP_TOPUP(" +
                            mCR_NewValue.BayNo + "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.CompartmentNo +
                            ",:RET_BATCH_NO,:RET_LOAD_COUNT,:RET_RECIPES_NO,:RET_PRESET" +
                            ",:RET_DESITY30C,:RET_VCF30" +
                            ",:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2,:RET_METER_NO,:RET_SALE_PRODUCT_NAME" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(12);

            OraParam.AddParameter(0, "RET_BATCH_NO", OracleDbType.Int32);
            OraParam.AddParameter(1, "RET_LOAD_COUNT", OracleDbType.Int16);
            OraParam.AddParameter(2, "RET_RECIPES_NO", OracleDbType.Int16);
            OraParam.AddParameter(3, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddParameter(4, "RET_DENSITY30C", OracleDbType.Varchar2, 64);
            OraParam.AddParameter(5, "RET_VCF30", OracleDbType.Single);
            OraParam.AddParameter(6, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddParameter(7, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(8, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(9, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(10, "RET_METER_NO", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(11, "RET_SALE_PRODUCT_NAME", OracleDbType.Varchar2, 128);


            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_BATCH_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_LINE = Convert.ToInt32(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_LINE = 0;

                OraParam.GetParameterValue("RET_LOAD_COUNT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_COUNT = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_COUNT = 0;

                OraParam.GetParameterValue("RET_RECIPES_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_RECIPES_NO = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_RECIPES_NO = 0;

                OraParam.GetParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_PRESET = 0;

                OraParam.GetParameterValue("RET_DENSITY30C", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_DENSITY30C = Convert.ToDouble(p.Value.ToString());
                else
                    mCR_NewValue.RET_DENSITY30C = 0;

                OraParam.GetParameterValue("RET_VCF30", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_VCF30 = Convert.ToDouble(p.Value.ToString());
                else
                    mCR_NewValue.RET_VCF30 = 0;

                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                OraParam.GetParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH1 = "";

                OraParam.GetParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH2 = "";

                OraParam.GetParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_METER_NO = p.Value.ToString();
                else
                    mCR_NewValue.RET_METER_NO = "";

                OraParam.GetParameterValue("RET_SALE_PRODUCT_NAME", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_SALE_PRODUCT_NAME = p.Value.ToString();
                else
                    mCR_NewValue.RET_SALE_PRODUCT_NAME = "";

                Addlistbox(mCR_NewValue.RET_MSG);
                if (mCR_NewValue.RET_CHECK == -1)
                {
                    Addlistbox(mCR_NewValue.RET_MSG_BATCH1 + " " + mCR_NewValue.RET_MSG_BATCH2);
                }
                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_CRBAY_BATCH_START()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_BATCH_START(" +
                            mCR_NewValue.BayNo + ",'" + mCR_NewValue.RET_METER_NO + "'," + mCR_NewValue.CompartmentNo +
                            "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.RET_LOAD_LINE + "," + mCR_NewValue.RET_LOAD_COUNT +
                            ",:RET_CHECK,:RET_MSG" +
                            ");end;";
            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                Addlistbox(mCR_NewValue.RET_MSG);
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }
        private bool M_CRBAY_TOPUP_BATCH_START()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_TOPUP_BATCH_START(" +
                            mCR_NewValue.BayNo + ",'" + mCR_NewValue.RET_METER_NO + "'," + mCR_NewValue.CompartmentNo +
                            "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.RET_TOPUP_NO + "," + mCR_NewValue.RET_LOAD_COUNT +
                            ",:RET_CHECK,:RET_MSG" +
                            ");end;";
            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                Addlistbox(mCR_NewValue.RET_MSG);
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }
        private bool M_BATCH_START_LOADING()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_BATCH_START_LOADING(" +
                            mCR_NewValue.BayNo + ",'" + mCR_NewValue.RET_METER_NO + "'," + mCR_NewValue.CompartmentNo +
                            "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.RET_LOAD_LINE + "," + mCR_NewValue.RET_LOAD_COUNT +
                            ",:RET_CHECK,:RET_MSG" +
                            ");end;";
            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = -1;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                //Addlistbox(CR_NewValue.RET_MSG);
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_BATCH_CHECK_LINE_LOADING()
        {
            bool bCheck = false;
            string strSQL = "begin load.M_BATCH_CHECK_LINE_LOADING(" +
                            "'" + mCR_NewValue.RET_METER_NO + "'" +
                            ",:RET_COMP_NO,:RET_BAY_NO,:RET_LOAD_HEADER_NO,:RET_LOAD_LINE_NO,:RET_LOAD_COUNT,:RET_START_TOT" +
                            ");end;";
            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(6);

            OraParam.AddParameter(0, "RET_COMP_NO", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_BAY_NO", OracleDbType.Int16);
            OraParam.AddParameter(2, "RET_LOAD_HEADER_NO", OracleDbType.Double);
            OraParam.AddParameter(3, "RET_LOAD_LINE_NO", OracleDbType.Double);
            OraParam.AddParameter(4, "RET_LOAD_COUNT", OracleDbType.Int16);
            OraParam.AddParameter(5, "RET_START_TOT", OracleDbType.Varchar2,64);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_LOAD_LINE_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_LINE = Convert.ToDouble(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_LINE = 0;

                bCheck = true;
            }

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_CRBAY_BATCH_STOP()
        {
            bool bCheck = false;
            string strSQL = "begin load.M_CRBAY_BATCH_STOP(" +
                            mCR_NewValue.BayNo + ",'" + mCR_NewValue.RET_METER_NO + "'," + mCR_NewValue.CompartmentNo +
                            "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.RET_LOAD_LINE + "," + mCR_NewValue.RET_LOAD_COUNT +
                            ",:RET_CHeCK,:RET_MSG" +
                            ");end;";
            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = -1;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                Addlistbox(mCR_NewValue.RET_MSG);
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }
        private bool M_CRBAY_TOPUP_BATCH_STOP()
        {
            bool bCheck = false;
            string strSQL = "begin load.M_CRBAY_TOPUP_BATCH_STOP(" +
                            mCR_NewValue.BayNo + ",'" + mCR_NewValue.RET_METER_NO + "'," + mCR_NewValue.CompartmentNo +
                            "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.RET_TOPUP_NO + "," + mCR_NewValue.RET_LOAD_COUNT +
                            ",:RET_CHeCK,:RET_MSG" +
                            ");end;";
            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = -1;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                Addlistbox(mCR_NewValue.RET_MSG);
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_BATCH_STOP_LOADING()
        {
            bool bCheck = false;
            string strSQL = "begin load.M_BATCH_STOP_LOADING(" +
                            mCR_NewValue.BayNo + ",'" + mCR_NewValue.RET_METER_NO + "'," + mCR_NewValue.CompartmentNo +
                            "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.RET_LOAD_LINE + "," + mCR_NewValue.RET_LOAD_COUNT +
                            ",:RET_CHECK,:RET_MSG" +
                            ");end;";
            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = -1;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                //Addlistbox(CR_NewValue.RET_MSG);
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_CRBAY_LOADING()
        {
            //create new procedure(line_no,compartment_no,batch_status)
            // check batch_status -> =5,6 step=loading_none
            //                    -> =4 display batch value 
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_LOADING(" +
                            "'" + mCR_NewValue.RET_METER_NO + "'," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.RET_LOAD_LINE +
                            "," + mCR_NewValue.CompartmentNo +
                            ",:RET_BATCH_STATUS,:RET_LOADED_MASS,:RET_FLOWRATE" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);

            OraParam.AddParameter(0, "RET_BATCH_STATUS", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_LOADED_MASS", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(2, "RET_FLOWRATE", OracleDbType.Varchar2, 128);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_BATCH_STATUS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_BATCH_STATUS = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_BATCH_STATUS = 0;

                OraParam.GetParameterValue("RET_LOADED_MASS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOADED_MASS = p.Value.ToString();
                else
                    mCR_NewValue.RET_LOADED_MASS = "0";

                OraParam.GetParameterValue("RET_FLOWRATE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_FLOWRATE = p.Value.ToString();
                else
                    mCR_NewValue.RET_FLOWRATE = "0";

                bCheck = true;
            }

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_CRBAY_TOPUP_LOADING()
        {
            //create new procedure(line_no,compartment_no,batch_status)
            // check batch_status -> =5,6 step=loading_none
            //                    -> =4 display batch value 
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_TOPUP_LOADING(" +
                            "'" + mCR_NewValue.RET_METER_NO + "'," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.RET_TOPUP_NO +
                            "," + mCR_NewValue.CompartmentNo +
                            ",:RET_BATCH_STATUS,:RET_LOADED_MASS,:RET_FLOWRATE" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);

            OraParam.AddParameter(0, "RET_BATCH_STATUS", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_LOADED_MASS", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(2, "RET_FLOWRATE", OracleDbType.Varchar2, 128);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_BATCH_STATUS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_BATCH_STATUS = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_BATCH_STATUS = 0;
                OraParam.GetParameterValue("RET_LOADED_MASS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOADED_MASS = p.Value.ToString();
                else
                    mCR_NewValue.RET_LOADED_MASS = "0";

                OraParam.GetParameterValue("RET_FLOWRATE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_FLOWRATE = p.Value.ToString();
                else
                    mCR_NewValue.RET_FLOWRATE = "0";

                bCheck = true;
            }

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_CRBAY_CHECK_BAY()
        {
            bool bCheck = false;
            string strSQL = "begin load.M_CRBAY_CHECK_BAY(" +
                            mCR_NewValue.BayNo +
                            ",:RET_CARD_CODE,:RET_LOAD_HEADER,:RET_LOAD_LINE" +
                            ",:RET_COMPARTMENT_NO,:RET_COMPARTMENT_LIST,:RET_TOT_COMPARTMENT,:RET_TU_ID,:RET_METER_NO" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(8);

            OraParam.AddParameter(0, "RET_CARD_CODE", OracleDbType.Varchar2,128);
            OraParam.AddParameter(1, "RET_LOAD_HEADER", OracleDbType.Int32);
            OraParam.AddParameter(2, "RET_LOAD_LINE", OracleDbType.Int32);
            OraParam.AddParameter(3, "RET_COMPARTMENT_NO", OracleDbType.Int16);
            OraParam.AddParameter(4, "RET_COMPARTMENT_LIST", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(5, "RET_TOT_COMPARTMENT", OracleDbType.Int16);
            OraParam.AddParameter(6, "RET_TU_ID", OracleDbType.Varchar2, 64);
            OraParam.AddParameter(7, "RET_METER_NO", OracleDbType.Varchar2, 64);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CARD_CODE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.CardCode = p.Value.ToString();
                else
                    mCR_NewValue.CardCode = "0";

                OraParam.GetParameterValue("RET_LOAD_HEADER", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_HEADER =Convert.ToDouble(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_HEADER = 0;

                OraParam.GetParameterValue("RET_LOAD_LINE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_LINE = Convert.ToDouble(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_LINE = 0;

                OraParam.GetParameterValue("RET_COMPARTMENT_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.CompartmentNo = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.CompartmentNo = 0;


                OraParam.GetParameterValue("RET_COMPARTMENT_LIST", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_COMPARTMENT_LIST = p.Value.ToString();
                else
                    mCR_NewValue.RET_COMPARTMENT_LIST = "0";

                OraParam.GetParameterValue("RET_TOT_COMPARTMENT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_TOT_COMPARTMENT = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_TOT_COMPARTMENT = 0;

                OraParam.GetParameterValue("RET_TU_ID", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_TU_ID = p.Value.ToString();
                else
                    mCR_NewValue.RET_TU_ID = "";

                OraParam.GetParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_METER_NO = p.Value.ToString();
                else
                    mCR_NewValue.RET_METER_NO = "";

                bCheck = true;
            }

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }
        private bool M_CRBAY_TOPUP_CHECK_BAY()
        {
            bool bCheck = false;
            string strSQL = "begin load.M_CRBAY_TOPUP_CHECK_BAY(" +
                            mCR_NewValue.BayNo +
                            ",:RET_CARD_CODE,:RET_LOAD_HEADER,:RET_TOPUP_NO" +
                            ",:RET_COMPARTMENT_NO,:RET_COMPARTMENT_LIST,:RET_TOT_COMPARTMENT,:RET_TU_ID,:RET_METER_NO" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(8);

            OraParam.AddParameter(0, "RET_CARD_CODE", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(1, "RET_LOAD_HEADER", OracleDbType.Int32);
            OraParam.AddParameter(2, "RET_TOPUP_NO", OracleDbType.Int32);
            OraParam.AddParameter(3, "RET_COMPARTMENT_NO", OracleDbType.Int16);
            OraParam.AddParameter(4, "RET_COMPARTMENT_LIST", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(5, "RET_TOT_COMPARTMENT", OracleDbType.Int16);
            OraParam.AddParameter(6, "RET_TU_ID", OracleDbType.Varchar2, 64);
            OraParam.AddParameter(7, "RET_METER_NO", OracleDbType.Varchar2, 64);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CARD_CODE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.CardCode = p.Value.ToString();
                else
                    mCR_NewValue.CardCode = "0";

                OraParam.GetParameterValue("RET_LOAD_HEADER", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_HEADER = Convert.ToDouble(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_HEADER = 0;

                OraParam.GetParameterValue("RET_TOPUP_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_TOPUP_NO = Convert.ToInt32(p.Value.ToString());
                else
                    mCR_NewValue.RET_TOPUP_NO = 0;

                OraParam.GetParameterValue("RET_COMPARTMENT_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.CompartmentNo = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.CompartmentNo = 0;


                OraParam.GetParameterValue("RET_COMPARTMENT_LIST", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_COMPARTMENT_LIST = p.Value.ToString();
                else
                    mCR_NewValue.RET_COMPARTMENT_LIST = "0";

                OraParam.GetParameterValue("RET_TOT_COMPARTMENT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_TOT_COMPARTMENT = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_TOT_COMPARTMENT = 0;

                OraParam.GetParameterValue("RET_TU_ID", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_TU_ID = p.Value.ToString();
                else
                    mCR_NewValue.RET_TU_ID = "";

                OraParam.GetParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_METER_NO = p.Value.ToString();
                //else
                //    mCR_NewValue.RET_METER_NO = "";

                bCheck = true;
            }

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }
        private bool M_CRBAY_CHECK_TOPUP()
        {
            string is_topup;
            bool bCheck = false;
            string strSQL = "begin load.M_CRBAY_CHECK_TOPUP(" +
                            mCR_NewValue.BayNo +
                            ",:RET_METER_NO,:RET_METER_NAME,:RET_IS_TOPUP" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);

            OraParam.AddParameter(0, "RET_METER_NO", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(1, "RET_METER_NAME", OracleDbType.Varchar2,128);
            OraParam.AddParameter(2, "RET_IS_TOPUP", OracleDbType.Int32);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_IS_TOPUP", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                {
                    is_topup = p.Value.ToString();
                    mCR_NewValue.ModeStatus = Convert.ToInt32(is_topup);
                }
                OraParam.GetParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_METER_NO = p.Value.ToString();

                bCheck = true;
            }

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_CRBAY_TOPUP_CHECK_CARD()
        {
            bool bCheck = false;
            string strSQL = "begin load.M_CRBAY_TOPUP_CHECK_CARD(" +
                            mCR_OldValue.CardCode + "," + mCR_NewValue.BayNo + "," + "'" + mCR_NewValue.RET_METER_NO +"'"+","+ mCR_NewValue.ID +
                            ",:RET_BATCH_STATUS,:RET_TOPUP_NO,:RET_LOAD_HEADER_NO,:RET_COMPARTMENT_NO" +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(7);

            OraParam.AddParameter(0, "RET_BATCH_STATUS", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_TOPUP_NO", OracleDbType.Int32);
            OraParam.AddParameter(2, "RET_LOAD_HEADER_NO", OracleDbType.Int32);
            OraParam.AddParameter(3, "RET_COMPARTMENT_NO", OracleDbType.Int32);
            OraParam.AddParameter(4, "RET_CHECK", OracleDbType.Int32);
            OraParam.AddParameter(5, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(6, "RET_CR_MSG", OracleDbType.Varchar2, 128);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_BATCH_STATUS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_BATCH_STATUS = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_BATCH_STATUS = 0;
                OraParam.GetParameterValue("RET_TOPUP_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_TOPUP_NO = Convert.ToInt32(p.Value.ToString());
                else
                    mCR_NewValue.RET_TOPUP_NO = 0;
                OraParam.GetParameterValue("RET_LOAD_HEADER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_HEADER = Convert.ToInt32(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_HEADER = 0;
                OraParam.GetParameterValue("RET_COMPARTMENT_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.CompartmentNo = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.CompartmentNo = 0;
                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt32(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;
                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";
                OraParam.GetParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CR_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_CR_MSG = "";
                
                bCheck = true;
            }

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }
        private bool M_CRBAY_TOPUP_CHECK_COMP()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_TOPUP_CHECK_COMP(" +
                            mCR_NewValue.BayNo + "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.RET_TOPUP_NO + "," + mCR_NewValue.CompartmentNo + "," +"'"+ mCR_NewValue.RET_METER_NO+"'"+
                            ",:RET_BATCH_NO" +
                            ",:RET_LOAD_COUNT,:RET_RECIPES_NO,:RET_PRESET,:RET_DESITY30C" +
                            ",:RET_VCF30C,:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2,:RET_SALE_PRODUCT_NAME" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(11);

            OraParam.AddParameter(0, "RET_BATCH_NO", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_LOAD_COUNT", OracleDbType.Int16);
            OraParam.AddParameter(2, "RET_RECIPES_NO", OracleDbType.Int16);
            OraParam.AddParameter(3, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddParameter(4, "RET_DESITY30C", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(5, "RET_VCF30C", OracleDbType.Int16);
            OraParam.AddParameter(6, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddParameter(7, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(8, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(9, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(10, "RET_SALE_PRODUCT_NAME", OracleDbType.Varchar2, 128);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_BATCH_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_BATCH_NO = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_BATCH_NO = 0;

                OraParam.GetParameterValue("RET_LOAD_COUNT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_COUNT = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_COUNT = 0;

                OraParam.GetParameterValue("RET_RECIPES_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_RECIPES_NO = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_RECIPES_NO = 0;

                OraParam.GetParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_PRESET = 0;

                OraParam.GetParameterValue("RET_DESITY30C", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_DENSITY30C = Convert.ToDouble(p.Value.ToString());
                else
                    mCR_NewValue.RET_DENSITY30C = 0;
                OraParam.GetParameterValue("RET_VCF30C", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_VCF30C = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_VCF30C = 0;

                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                OraParam.GetParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH1 = "";

                OraParam.GetParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH2 = "";
                OraParam.GetParameterValue("RET_SALE_PRODUCT_NAME", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_SALE_PRODUCT_NAME = p.Value.ToString();
                else
                    mCR_NewValue.RET_SALE_PRODUCT_NAME = "";

                Addlistbox(mCR_NewValue.RET_MSG);
                if (mCR_NewValue.RET_CHECK == -1)
                {
                    Addlistbox(mCR_NewValue.RET_MSG_BATCH1 + " " + mCR_NewValue.RET_MSG_BATCH2);
                }
                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_BATCH_CHECK_ALARM()
        {
            bool bCheck = false;
            string strSQL = "begin load.M_BATCH_CHECK_ALARM(" +
                            "'" + mCR_NewValue.RET_METER_NO + "'" +
                            ",:RET_CHECK,:RET_MSG" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_MSG", OracleDbType.Varchar2, 128);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = -1;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }
            else
                mCR_NewValue.RET_CHECK = -1;

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;

            return bCheck;
        }

        private bool M_CRBAY_GET_PRESET()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_GET_PRESET(" +
                            mCR_NewValue.BayNo + "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.CompartmentNo +
                            ",:RET_PRESET" +
                            ",:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(5);

            OraParam.AddParameter(0, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddParameter(2, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(3, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(4, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_PRESET = 0;

                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                OraParam.GetParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH1 = "";

                OraParam.GetParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH2 = "";

                Addlistbox(mCR_NewValue.RET_MSG);
                if (mCR_NewValue.RET_CHECK == -1)
                {
                    Addlistbox(mCR_NewValue.RET_MSG_BATCH1 + " " + mCR_NewValue.RET_MSG_BATCH2);
                }
                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }
        private bool M_CRBAY_TOPUP_GET_PRESET()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_TOPUP_GET_PRESET(" +
                            mCR_NewValue.BayNo + "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.CompartmentNo +
                            ",:RET_PRESET" +
                            ",:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(5);

            OraParam.AddParameter(0, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddParameter(1, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddParameter(2, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(3, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(4, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_PRESET = 0;

                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                OraParam.GetParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH1 = "";

                OraParam.GetParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH2 = "";

                Addlistbox(mCR_NewValue.RET_MSG);
                if (mCR_NewValue.RET_CHECK == -1)
                {
                    Addlistbox(mCR_NewValue.RET_MSG_BATCH1 + " " + mCR_NewValue.RET_MSG_BATCH2);
                }
                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_BATCH_DETAIL_COMPARTMENT()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_BATCH_DETAIL_COMPARTMENT(" +
                            mCR_NewValue.BayNo + ",'" + mCR_NewValue.RET_METER_NO + "'," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.CompartmentNo +
                            ",:RET_BATCH_NO,:RET_LOAD_COUNT,:RET_RECIPES_NO,:RET_PRESET" +
                            ",:RET_WRITE_DESITY30C,:RET_DESITY30C,:RET_WRITE_VCF30,:RET_VCF30" +
                            ",:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2" +
                            ");end;";

            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(12);

            OraParam.AddParameter(0, "RET_BATCH_NO", OracleDbType.Int32);
            OraParam.AddParameter(1, "RET_LOAD_COUNT", OracleDbType.Int16);
            OraParam.AddParameter(2, "RET_RECIPES_NO", OracleDbType.Int16);
            OraParam.AddParameter(3, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddParameter(4, "RET_WRITE_DENSITY30C", OracleDbType.Varchar2, 64);
            OraParam.AddParameter(5, "RET_DENSITY30C", OracleDbType.Varchar2, 64);
            OraParam.AddParameter(6, "RET_WRITE_VCF30", OracleDbType.Single);
            OraParam.AddParameter(7, "RET_VCF30", OracleDbType.Single);
            OraParam.AddParameter(8, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddParameter(9, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(10, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(11, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_BATCH_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_LINE = Convert.ToInt32(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_LINE = 0;

                OraParam.GetParameterValue("RET_LOAD_COUNT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_LOAD_COUNT = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_LOAD_COUNT = 0;

                OraParam.GetParameterValue("RET_RECIPES_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_RECIPES_NO = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_RECIPES_NO = 0;

                OraParam.GetParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_PRESET = 0;

                OraParam.GetParameterValue("RET_DENSITY30C", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_DENSITY30C = Convert.ToDouble(p.Value.ToString());
                else
                    mCR_NewValue.RET_DENSITY30C = 0;

                OraParam.GetParameterValue("RET_VCF30", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_VCF30 = Convert.ToDouble(p.Value.ToString());
                else
                    mCR_NewValue.RET_VCF30 = 0;

                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                OraParam.GetParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH1 = "";

                OraParam.GetParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG_BATCH2 = "";


                Addlistbox(mCR_NewValue.RET_MSG);
                if (mCR_NewValue.RET_CHECK == -1)
                {
                    Addlistbox(mCR_NewValue.RET_MSG_BATCH1 + " " + mCR_NewValue.RET_MSG_BATCH2);
                }
                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_CRBAY_OPERATOR_CONFIRM()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_OPERATOR_CONFIRM(" +
                            Convert.ToInt64(mCR_NewValue.CardCode) + "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.ID +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";
            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);

            OraParam.AddParameter(0, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddParameter(1, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(2, "RET_CR_MSG", OracleDbType.Varchar2, 128);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                OraParam.GetParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CR_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_CR_MSG = "";


                Addlistbox(mCR_NewValue.RET_MSG);
                if (mCR_NewValue.RET_CHECK == -1)
                {
                    Addlistbox(mCR_NewValue.RET_CR_MSG);
                }
                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_CRBAY_DRIVER_CONFIRM()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_DRIVER_CONFIRM(" +
                            Convert.ToInt64(mCR_NewValue.CardCode) + "," + mCR_NewValue.RET_LOAD_HEADER + "," + mCR_NewValue.ID +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";
            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);

            OraParam.AddParameter(0, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddParameter(1, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddParameter(2, "RET_CR_MSG", OracleDbType.Varchar2, 128);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    mCR_NewValue.RET_CHECK = 0;

                OraParam.GetParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_MSG = "";

                OraParam.GetParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_CR_MSG = p.Value.ToString();
                else
                    mCR_NewValue.RET_CR_MSG = "";


                Addlistbox(mCR_NewValue.RET_MSG);
                if (mCR_NewValue.RET_CHECK == -1)
                {
                    Addlistbox(mCR_NewValue.RET_CR_MSG);
                }
                if (mCR_NewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool M_CRBAY_GET_METER_NAME()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_GET_METER_NAME(" +
                            mCR_NewValue.RET_LOAD_HEADER +","+mCR_NewValue.BayNo + "," + mCR_NewValue.CompartmentNo +
                            ",:RET_METER_NAME" +
                            ");end;";
            cOracleParameter OraParam = new cOracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(1);

            OraParam.AddParameter(0, "RET_METER_NAME", OracleDbType.Varchar2, 64);

            if (mFMercury.mOraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetParameterValue("RET_METER_NAME", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    mCR_NewValue.RET_METER_NAME = p.Value.ToString();
                    
                else
                    mCR_NewValue.RET_METER_NAME = "";
                bCheck = true; 
            }
            else
            {
                bCheck = false;
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        #endregion

        #region "Process Bay Loading"
        private void CheckResponse(bool pResponse)
        {
            if (pResponse)
            {
                mCount = 0;
                mResponseTime = DateTime.Now;
                if (mResponse != pResponse)
                {
                    mCR_NewValue.Connect = true;
                    UPDATE_CARDREADER_CONNECT();
                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                    //if(mThread.ThreadState != ThreadState.Aborted)
                    //    mThread.Abort();
                    //StartThread();
                    mResponse = pResponse;
                }
            }
            else
            {
                mCount += 1;
                if (mCount <= 5)
                    return;

                DateTime vDateTime = DateTime.Now;
                
                var vDiff = (vDateTime - mResponseTime).TotalSeconds;

                if ((vDiff > 30) && (mResponse = true))
                {
                    mResponseTime = DateTime.Now;
                    mCR_NewValue.Connect = false;
                    mResponse = false;
                    UPDATE_CARDREADER_CONNECT();
                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                    //mThread.Abort();
                    //Thread.Sleep(1000);
                    //StartThread();
                    //if (!PPort.IsOpen())
                    //    PPort.StartThread();
                }
                //else
                //    vResponseTime = DateTime.Now;
            }
        }

        private byte[] BuildMsg(byte pCR_address, string pMsg)
        {
            byte vSTX = 2;
            byte R = 82;
            byte vETX = 3;

            byte[] b = Encoding.ASCII.GetBytes(pMsg);
            byte[] vMsg = new byte[1 + 2 + 1 + b.Length + 1 + 1 + 1];
            byte[] a;
            for (int i = 0; i < vMsg.Length; i++)
            {
                switch (i)
                {   //string.Format("{0:X}",(int)(msg[i-1]));
                    case 0:
                        vMsg[i] = vSTX;
                        break;
                    case 1:
                        a = Encoding.ASCII.GetBytes(pCR_address.ToString());
                        vMsg[i + 1] = a[0];
                        a = Encoding.ASCII.GetBytes("0");
                        vMsg[i] = a[0];
                        break;
                    case 2:
                        break;
                    case 3:
                        vMsg[i] = R;
                        break;
                    default:
                        if (i == vMsg.Length - 3)   //DMY=00
                        {
                            vMsg[i] = (byte)(32);
                        }
                        else if (i == vMsg.Length - 2)
                            break;
                        else if (i == vMsg.Length - 1)
                        {
                            vMsg[i] = vETX;
                        }
                        //else if (i == 4)    
                        //{

                        //    bMsg[i] = ESC;
                        //}
                        else
                            vMsg[i] = b[i - 4];
                        break;
                }
            }
            //cr_msg = bMsg;
            CalCSUM(vMsg, ref vMsg[vMsg.Length - 2]);
            return vMsg;
        }

        private void CalCSUM(byte[] pMsg, ref byte pCSUM)
        {
            int vCSUM;
            vCSUM = 0;
            for (int i = 0; i < pMsg.Length - 1; i++)
            {
                vCSUM += pMsg[i];
            }
            vCSUM &= 127;    //bit AND 0x7F
            vCSUM = ~vCSUM;

            pCSUM = (byte)(vCSUM & 127);
            pCSUM += 1;
        }

        private bool CheckBlockRecv(string pRecv)
        {
            bool vCheck = false;
            int vCheckPos;
            pRecv.Trim();

            if (pRecv.Length >= 5)
            {
                mSTX_Position = pRecv.IndexOf(char.ConvertFromUtf32(2));
                mETX_Position = pRecv.IndexOf(char.ConvertFromUtf32(3));
                vCheckPos = pRecv.IndexOf(char.ConvertFromUtf32(2) + mCR_NewValue.Address.ToString("00D"));
                if ((vCheckPos >= 0) && (mETX_Position > mSTX_Position))
                {
                    //DATA_Position = CheckPos;
                    mCR_NewValue.DataReceive = pRecv.Substring(mSTX_Position,mETX_Position+1);
                    vCheck = true;
                }
            }
            return vCheck;
        }
        private bool CheckBlockRecv(ref string pRecv)
        {
            bool vCheck = false;
            int vCheckPos;
            pRecv.Trim();

            if (pRecv.Length >= 5)
            {
                mSTX_Position = pRecv.IndexOf(char.ConvertFromUtf32(2));
                mETX_Position = pRecv.IndexOf(char.ConvertFromUtf32(3));
                vCheckPos = pRecv.IndexOf(char.ConvertFromUtf32(2) + mCR_NewValue.Address.ToString("00D"));
                if ((vCheckPos >= 0) && (mETX_Position > mSTX_Position))
                {
                    //DATA_Position = CheckPos;
                    mCR_NewValue.DataReceive = pRecv.Substring(mSTX_Position, mETX_Position + 1);
                    vCheck = true;
                }
            }
            return vCheck;
        }
        private void CRBayLoading()
        {

            //MessageBox.Show("ท่านอยู่ในโหมด Load");
                //CRBayLoading_CheckBay();
                //while (!mShutdown)
            //lock (mThreadLock)
            //{
            //Thread.Sleep(1000);
                DisplayToCardReader(mCR_NewValue.RET_STEP);
                while (mRunn)
                {
                    M_CRBAY_CHECK_TOPUP();//ตรววสอบโหมด
                    if (mCR_NewValue.ModeStatus == 0)
                    {
                        Thread.Sleep(300);
                        switch (mCR_NewValue.RET_STEP)
                        {
                            case 0:
                                CRBayLoading_None();
                                //DisplayToCardReader(mCR_NewValue.RET_STEP);
                                break;
                            //case 11:
                            //    DisplayToCardReader(CR_NewValue.RET_STEP);
                            //    break;
                            case 2:
                                CRBayLoading_Cancel();
                                mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                                break;
                            case 3:
                                CRBayLoading_Compartment();
                                break;
                            case (int)LoadingStep.LoadingStart:
                                CRBayLoading_Start();
                                break;
                            case (int)LoadingStep.LoadingLoad:
                                CRBayLoading_Load();
                                break;
                            case (int)LoadingStep.LoadingStop:
                                CRBayLoading_Stop();
                                break;
                            case (int)LoadingStep.OperatorConfirm:             // operator confirm
                                CRBayOperatorConfirm();
                                break;
                            case (int)LoadingStep.DriverConfirm:             //driver confirm
                                CRBayDriverConfirm();
                                break;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        CRBayLoading_Topup();
                    }
                   

                    //CR_NewValue.DataReceive = CR_NewValue.DataReceive;
                }
            //}
        }
        private void CRBayLoading_Topup()
        {
             // MessageBox.Show("ท่านอยู่ในโหมด Topup");
            //CRBayLoading_CheckBay();
            //while (!mShutdown)
            //lock (mThreadLock)
            //{
            //Thread.Sleep(1000);
            DisplayToCardReader(mCR_NewValue.RET_STEP);
            while (mRunn)
            {
                M_CRBAY_CHECK_TOPUP();//ตรววสอบโหมด
                if (mCR_NewValue.ModeStatus == 1)
                {
                    Thread.Sleep(600);
                    switch (mCR_NewValue.RET_STEP)
                    {
                        case 0:
                            CRBayLoading_None_Topup();
                            //DisplayToCardReader(mCR_NewValue.RET_STEP);
                            break;
                        //case 11:
                        //    DisplayToCardReader(CR_NewValue.RET_STEP);
                        //    break;
                        case 2:
                            CRBayLoading_Cancel();
                            mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                            break;
                        case 3:
                            CRBayLoadingTopup_Compartment();
                            break;
                        case (int)LoadingStep.LoadingStart:
                            CRBayLoadingTopup_Start();            //start
                            break;
                        case (int)LoadingStep.LoadingLoad:
                            CRBayLoadingTopup_Load();            //topup load
                            break;
                        case (int)LoadingStep.LoadingStop:
                            CRBayLoadingTopup_Stop();            //topup load
                            break;

                    }
                }
                else
                {
                    CRBayLoading();
                }
                

                //CR_NewValue.DataReceive = CR_NewValue.DataReceive;
            }
            //}
        }
        private void CRBayLoading_CheckBay()
        {
            if (M_CRBAY_CHECK_BAY())
            {
                if (Convert.ToDouble(mCR_NewValue.RET_LOAD_HEADER) > 0)
                {
                    mCR_NewValue.CompartmentNo = 1;
                    M_CRBAY_CHECK_STEP();
                }
            }
        }

        private void CRBayLoading_None()
        {
            try
            {
                //DisplayToCardReader(mCR_NewValue.RET_STEP);
                //SendToCardReader(DisplayDateTime());
                DisplayToCardReader((int)LoadingStep.DisplayDateTime);
                string vRecv = "";
                SendToCardReader(mMercuryLib.SendNextQueueBlock(),true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                //vRecv = CR_NewValue.DataReceive;
                mCR_NewValue.CardCode = CheckDataCard(ref mCR_NewValue.DataReceive);
                
                if (Convert.ToInt64(mCR_NewValue.CardCode) > 0)
                {
                    mCR_OldValue.CardCode = mCR_NewValue.CardCode;
                   
                     M_CRBAY_CHECK_BAY();
                    if (mCR_NewValue.RET_LOAD_HEADER == 0)
                    {
                        mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                        M_CRBAY_CHECK_STEP();
                        if (mCR_NewValue.RET_STEP == 3)
                        {
                            if (M_CRBAY_LOAD_CHECK_TU())
                            {
                                //M_CRBAY_LOAD_CHECK_TU();
                                if (mCR_NewValue.RET_LOAD_TYPE == 1)
                                {
                                    mCR_OldValue = mCR_NewValue;
                                    //M_CRBAY_CHECK_STEP();
                                }
                                else
                                {
                                    DisplayToCardReader(mCR_NewValue.RET_LOAD_TYPE);
                                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                                    SendToCardReader(mMercuryLib.ClearDisplay());
                                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                                }
                            }
                            else
                            {
                                DisplayToCardReader(-1);
                                //Thread.Sleep(5000);
                                ClearDataCard();
                                mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                                SendToCardReader(mMercuryLib.ClearDisplay());
                                DisplayToCardReader(mCR_NewValue.RET_STEP);
                            }
                        }
                        else
                        {
                            if (mCR_NewValue.RET_STEP == -1)
                            {
                                DisplayToCardReader(-1);
                                ClearDataCard();
                                mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                                SendToCardReader(mMercuryLib.ClearDisplay());
                                DisplayToCardReader(mCR_NewValue.RET_STEP);
                            }
                        }
                    }
                    else
                    {
                        mCR_NewValue.CardCode = mCR_OldValue.CardCode;//อ่านการ์ดมาใหม่
                        M_CRBAY_CHECK_STEP();
                        if (mCR_NewValue.RET_STEP == -1)
                        {
                            DisplayToCardReader(-1);
                            ClearDataCard();
                            mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                            SendToCardReader(mMercuryLib.ClearDisplay());
                            DisplayToCardReader(mCR_NewValue.RET_STEP);
                        }
                    }
                }
                if ((mCR_NewValue.RET_TOT_COMPARTMENT > 0) && (mCR_NewValue.RET_STEP != (int)LoadingStep.LoadingNone))
                {
                    if (mCR_NewValue.RET_IS_BLENDING == 0)
                    {
                        mCR_OldValue = mCR_NewValue;
                        mCR_NewValue.CompartmentNo = 1;

                        //M_CRBAY_CHECK_COMPARTMENT();
                        if (M_BATCH_DETAIL_COMPARTMENT())
                        {
                            mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStart;
                            //DisplayToCardReader(mCR_NewValue.RET_STEP);
                            mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                        }
                        else
                        {
                            DisplayToCardReader(-1);
                            //Thread.Sleep(5000);
                            ClearDataCard();
                            M_CRBAY_LOAD_CHECK_TU();
                            mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                            SendToCardReader(mMercuryLib.ClearDisplay());
                            DisplayToCardReader(mCR_NewValue.RET_STEP);
                        }
                    }
                    else
                    {
                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                        DisplayToCardReader(mCR_NewValue.RET_STEP);
                    }
                    //if (M_CRBAY_CHECK_COMPARTMENT())
                    //{
                    //    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStart;
                    //    DisplayToCardReader(mCR_NewValue.RET_STEP);
                    //}
                    //else
                    //{
                    //    DisplayToCardReader(mCR_NewValue.RET_CHECK);
                    //    DisplayToCardReader((int)LoadingStep.LoadingStart);
                    //    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStart;
                    //}
                }
                else if (mCR_NewValue.RET_STEP == (int)LoadingStep.OperatorConfirm)
                {
                    mCR_NewValue.RET_STEP = (int)LoadingStep.OperatorConfirm;
                }
                else if (mCR_NewValue.RET_STEP == (int)LoadingStep.DriverConfirm)
                {
                    mCR_NewValue.RET_STEP = (int)LoadingStep.DriverConfirm;
                }
                else
                {
                    //DisplayToCardReader(mCR_NewValue.RET_STEP);
                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                }
            }
            catch (Exception exp)
            { }
        }
        private void CRBayLoading_None_Topup()
        {
            try
            {
                //DisplayToCardReader(mCR_NewValue.RET_STEP);
                //SendToCardReader(DisplayDateTime());
                DisplayToCardReader((int)LoadingStep.DisplayDateTime);
                string vRecv = "";
                SendToCardReader(mMercuryLib.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                //vRecv = CR_NewValue.DataReceive;
                mCR_NewValue.CardCode = CheckDataCard(ref mCR_NewValue.DataReceive);

                if (Convert.ToInt64(mCR_NewValue.CardCode) > 0)
                {
                    mCR_OldValue.CardCode = mCR_NewValue.CardCode;
                    //MessageBox.Show(mCR_OldValue.CardCode);
                    //if (mCR_NewValue.Tmode == 1) //ถ้าเท่ากับโหมด Topup
                    //{
                        M_CRBAY_TOPUP_CHECK_CARD();
                        if (mCR_NewValue.RET_CHECK == 0) //ตรวจสอบบัตรพนักงาน
                        {
                            if (mCR_NewValue.RET_BATCH_STATUS < 6)
                            {
                         
                                if (M_CRBAY_LOAD_CHECK_TU_TOPUP())
                                {
                                    //M_CRBAY_LOAD_CHECK_TU();
                                    //CRBayLoading_Topup();
                                    if (mCR_NewValue.RET_LOAD_TYPE == 1)
                                    {
                                        M_CRBAY_TOPUP_CHECK_COMP();
                                        Addlistbox(mCR_NewValue.RET_MSG);
                                        //M_CRBAY_CHECK_COMP_TOPUP();
                                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStart;
                                        mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                                        // CRBayLoading_Topup();
                                    }
                                    else
                                    {
                                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                                        // M_CRBAY_LOAD_CHECK_TU();
                                        ClearDataCard();
                                        SendToCardReader(mMercuryLib.ClearDisplay());
                                        DisplayToCardReader(mCR_NewValue.RET_STEP);
                                        //CRBayLoading_Topup();
                                    }
                                }
                                else
                                {
                                    DisplayToCardReader(-1);
                                    //Thread.Sleep(5000);
                                    ClearDataCard();
                                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                                    SendToCardReader(mMercuryLib.ClearDisplay());
                                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                                    //CRBayLoading_Topup();
                                }
                                
                            }

                        }
                        else
                        {
                            Addlistbox(mCR_NewValue.RET_MSG);
                            mCR_NewValue.RET_STEP = (int)LoadingStep.NoDataFound;
                            //MessageBox.Show(mCR_NewValue.RET_MSG);
                            ////CRBayLoading_Topup();
                            ClearDataCard();
                            SendToCardReader(mMercuryLib.ClearDisplay());
                            DisplayToCardReader(mCR_NewValue.RET_STEP);
                            
                            //CRBayLoading_Topup();

                            mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                           // M_CRBAY_LOAD_CHECK_TU();
                            ClearDataCard();
                            SendToCardReader(mMercuryLib.ClearDisplay());
                            DisplayToCardReader(mCR_NewValue.RET_STEP);
                            //CRBayLoading_Topup();
                        }
                }
                else
                {
                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                }
            }
            catch (Exception exp)
            { }
        }
        private void CRBayLoading_Cancel()
        {
            DisplayToCardReader((int)LoadingStep.LoadingCancel);
            Thread.Sleep(5000);
            mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
            SendToCardReader(mMercuryLib.ClearDisplay());
            DisplayToCardReader(mCR_NewValue.RET_STEP);
        }

        private void CRBayLoading_Compartment()
        {
            DisplayToCardReader((int)LoadingStep.LoadingCompartment);
            //Addlistbox("["+ mCR_NewValue.RET_TU_ID + "]" +"Enter Compartment" + "[Load No=" + mCR_NewValue.RET_LOAD_HEADER + "]");
            Addlistbox("" + mCR_NewValue.RET_TU_ID  + "Select Product " + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + ".");
            string vRecv = "",k="";
            mCR_OldValue = mCR_NewValue;
            while (mCR_NewValue.RET_STEP == (int)LoadingStep.LoadingCompartment)
            {
                if (!mRunn)
                    break;

                SendToCardReader(mMercuryLib.SendNextQueueBlock(),true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                mCR_NewValue.CardCode = CheckDataCard(ref mCR_NewValue.DataReceive);

                if (Convert.ToInt64(mCR_NewValue.CardCode) > 0)      //cancel load
                {
                    //M_CRBAY_LOAD_CHECK_TU();
                    if (M_CRBAY_LOAD_CHECK_TU())
                    {
                        //DisplayToCardReader((int)LoadingStep.LoadingCancel);
                        //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        mCR_OldValue = mCR_NewValue;
                        CRBayLoading_Cancel();
                    }
                    else
                        if (mCR_NewValue.RET_CHECK == 0)
                        {
                            mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                        }
                        else
                        {
                            DisplayToCardReader(-1);
                            mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                        }
                }
                else
                {
                    mCR_NewValue.CardCode=mCR_OldValue.CardCode;
                    if (mCR_NewValue.DataReceive != "")
                    {
                        k = CheckKeyPress(ref mCR_NewValue.DataReceive);
                    }
                }
                if (k != "") //check enter compartment
                {
                    int val;
                    if(int.TryParse(k,out val))
                        mCR_NewValue.CompartmentNo = Convert.ToInt16(k);
                    else
                        mCR_NewValue.CompartmentNo =0;
                    //M_CRBAY_CHECK_COMPARTMENT();
                    if ((mCR_NewValue.CompartmentNo > 0) && M_CRBAY_CHECK_COMPARTMENT())
                    {
                        DisplayToCardReader((int)LoadingStep.LoadingStart);
                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStart;
                        //SendToCardReader(PMercuryFnc.ClearDisplay());
                    }
                
                    else
                    {
                        string s="";
                        DisplayToCardReader(-1);
                        //Thread.Sleep(5000);
                        SendToCardReader(mMercuryLib.MoveCursor(8,1) + s.PadRight(40,' '));
                        DisplayToCardReader((int)LoadingStep.LoadingCompartment);
                    }
                }
                else//display compartment
                {
                    SendToCardReader(mMercuryLib.MoveCursor(1, 1) +  mMercuryLib.MoveCursor(1, 20) + DateTime.Now);
                    //M_CRBAY_CHECK_STEP();
                    //if (M_CRBAY_CHECK_STEP())
                    //{
                    //    DisplayToCardReader(CR_NewValue.RET_STEP);
                    //}
                    //else
                    //    DisplayToCardReader(-1);
                }
                if (!mRunn)
                    break;
            }
        }
        private void CRBayLoadingTopup_Compartment()
        {
            DisplayToCardReader((int)LoadingStep.LoadingCompartment);
            //Addlistbox("["+ mCR_NewValue.RET_TU_ID + "]" +"Enter Compartment" + "[Load No=" + mCR_NewValue.RET_LOAD_HEADER + "]");
            Addlistbox("[Top up]" + mCR_NewValue.RET_TU_ID  + " Select Product" + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + ".");
            string vRecv = "", k = "";
            mCR_OldValue = mCR_NewValue;
            while (mCR_NewValue.RET_STEP == (int)LoadingStep.LoadingCompartment)
            {
                if (!mRunn)
                    break;

                SendToCardReader(mMercuryLib.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                mCR_NewValue.CardCode = CheckDataCard(ref mCR_NewValue.DataReceive);

                if (Convert.ToInt64(mCR_NewValue.CardCode) > 0)      //cancel load
                {
                    //M_CRBAY_LOAD_CHECK_TU();
                    if (M_CRBAY_LOAD_CHECK_TU())
                    {
                        //DisplayToCardReader((int)LoadingStep.LoadingCancel);
                        //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        mCR_OldValue = mCR_NewValue;
                        CRBayLoading_Cancel();
                    }
                    else
                        if (mCR_NewValue.RET_CHECK == 0)
                        {
                            mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                        }
                        else
                        {
                            DisplayToCardReader(-1);
                            mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                        }
                }
                else
                {
                    mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                    if (mCR_NewValue.DataReceive != "")
                    {
                        k = CheckKeyPress(ref mCR_NewValue.DataReceive);
                    }
                }
                if (k != "") //check enter compartment
                {
                    int val;
                    if (int.TryParse(k, out val))
                        mCR_NewValue.CompartmentNo = Convert.ToInt16(k);
                    else
                        mCR_NewValue.CompartmentNo = 0;
                    //M_CRBAY_CHECK_COMPARTMENT();
                    if ((mCR_NewValue.CompartmentNo > 0) && M_CRBAY_CHECK_COMPARTMENT())
                    {
                        DisplayToCardReader((int)LoadingStep.LoadingStart);
                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStart;
                        //SendToCardReader(PMercuryFnc.ClearDisplay());
                    }

                    else
                    {
                        string s = "";
                        DisplayToCardReader(-1);
                        //Thread.Sleep(5000);
                        SendToCardReader(mMercuryLib.MoveCursor(8, 1) + s.PadRight(40, ' '));
                        DisplayToCardReader((int)LoadingStep.LoadingCompartment);
                    }
                }
                else//display compartment
                {
                    SendToCardReader(mMercuryLib.MoveCursor(1, 1) + mMercuryLib.MoveCursor(1, 20) + DateTime.Now);
                    //M_CRBAY_CHECK_STEP();
                    //if (M_CRBAY_CHECK_STEP())
                    //{
                    //    DisplayToCardReader(CR_NewValue.RET_STEP);
                    //}
                    //else
                    //    DisplayToCardReader(-1);
                }
                if (!mRunn)
                    break;
            }
        }
        private void CRBayLoading_Start() //check key F1 for start ,F3 for cancel
        {
            string vRecv = "", k = "";
            mCR_NewValue.IsAlarm = false;
            mCR_OldValue = mCR_NewValue;
            SendToCardReader(mMercuryLib.ClearDisplay());
            DisplayToCardReader(mCR_NewValue.RET_STEP);
            SendToCardReader(mMercuryLib.SendNextQueueBlock(), true);
            while (mCR_NewValue.RET_STEP == (int)LoadingStep.LoadingStart)
            {
               // SendToCardReader(mMercuryLib.MoveCursor(1, 20) + DateTime.Now);
                //SendToCardReader(DisplayDateTime());
                //DisplayToCardReader((int)LoadingStep.DisplayDateTime);
                DisplayToCardReader((int)LoadingStep.DisplayDateTime);
                SendToCardReader(mMercuryLib.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                mCR_NewValue.CardCode = CheckDataCard(ref mCR_NewValue.DataReceive);
                if (Convert.ToInt64(mCR_NewValue.CardCode) == 0)
                    k = CheckKeyPress(ref mCR_NewValue.DataReceive);
                //if(k!="")
                //    Addlistbox("Enter key=" + k);
                if (!M_BATCH_CHECK_ALARM())
                {
                    if (!mCR_NewValue.IsAlarm)
                    {
                        DisplayMeterAlarm();
                        Thread.Sleep(500);
                        mCR_NewValue.IsAlarm = true;
                        //SendToCardReader(ClearLastLine());
                    }
                    //DisplayToCardReader(mCR_NewValue.RET_STEP);
                    //goto Next;
                }
                else
                {
                    if (mCR_NewValue.IsAlarm)
                    {
                        mCR_NewValue.IsAlarm = false;
                        SendToCardReader(ClearLastLine());
                    }
                }

                if(k=="F1")
                {
                    mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                    if (!M_BATCH_CHECK_ALARM())
                    {
                        mCR_NewValue.IsAlarm = true;
                        DisplayMeterAlarm();
                        Thread.Sleep(500);
                        //DisplayToCardReader(mCR_NewValue.RET_STEP);
                        goto Next;
                    }
                    if ( M_CRBAY_CHECK_COMPARTMENT())
                    {
                        if (M_CRBAY_BATCH_START()) //M_CRBAY_BATCH_START()
                        {
                            SendToCardReader(mMercuryLib.MoveCursor(8, 1) + "Start Load");
                            Thread.Sleep(3000);
                            //M_BATCH_START_LOADING();
                            mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingLoad;
                            Addlistbox("[Start Load]" + mCR_NewValue.RET_TU_ID + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + " Comp.No=" + mCR_NewValue.CompartmentNo + ".");
                            SendToCardReader(ClearLastLine());
                            //DisplayToCardReader((int)LoadingStep.LoadingLoad);
                            //Thread.Sleep(5000);
                        }
                        else
                        {
                            mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                            DisplayToCardReader(-1);
                            DisplayToCardReader((int)LoadingStep.LoadingStart);
                        }
                    }
                    else
                    {
                        DisplayToCardReader(-1);
                        DisplayToCardReader((int)LoadingStep.LoadingStart);
                    }
                }
                else if (k == "F3")
                {
                    mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                    mCR_NewValue.RET_LOAD_HEADER = mCR_OldValue.RET_LOAD_HEADER;
                    mCR_NewValue.RET_CR_MSG = "Cancel Load";
                    
                    Addlistbox("[Cancel Load]" + mCR_NewValue.RET_TU_ID + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + " Comp.No=" + mCR_NewValue.CompartmentNo + ".");
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    DisplayToCardReader((int)LoadingStep.LoadingCancel);
                    mCR_NewValue.RET_CR_MSG = "";
                    SendToCardReader(ClearLastLine());
                    Thread.Sleep(3000);
                    //CRBayLoading_Stop();
                    if (mCR_NewValue.RET_IS_BLENDING == 0)
                    {
                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        M_CRBAY_LOAD_CHECK_TU();
                        ClearDataCard();
                        SendToCardReader(mMercuryLib.ClearDisplay());
                        DisplayToCardReader(mCR_NewValue.RET_STEP);
                    }
                    else
                    {
                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                    }
                }
                else if (Convert.ToInt64(mCR_NewValue.CardCode) > 0)
                {
                    mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                    mCR_NewValue.RET_LOAD_HEADER = mCR_OldValue.RET_LOAD_HEADER;
                    mCR_NewValue.RET_CR_MSG = "Cancel Load";
                    //mCR_NewValue.CompartmentNo = 1;
                    Addlistbox("[Cancel Load]" + mCR_NewValue.RET_TU_ID + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + " Comp.No=" + mCR_NewValue.CompartmentNo + ".");
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    DisplayToCardReader((int)LoadingStep.LoadingCancel);
                    mCR_NewValue.RET_CR_MSG = "";
                    SendToCardReader(ClearLastLine());
                    Thread.Sleep(3000);
                    //CRBayLoading_Stop();
                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                    M_CRBAY_LOAD_CHECK_TU();
                    ClearDataCard();
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                    //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStop;
                }
            Next:
                if (!mRunn)
                    break;
                //SendToCardReader(DisplayDateTime());
                Thread.Sleep(600);
            }
        }

        private void CRBayLoading_Load() //check key F1 for start ,F3 for stop
        {
            string vRecv = "", k = "";
            DisplayToCardReader((int)LoadingStep.LoadingLoad);
            Addlistbox("[Loading]" + mCR_NewValue.RET_TU_ID + " Loading in progress" + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + " Comp.No=" + mCR_NewValue.CompartmentNo + ".");
            mCR_NewValue.IsAlarm = false;

            while (mCR_NewValue.RET_STEP == (int)LoadingStep.LoadingLoad)
            {
                if (!mRunn)
                    break;
                //SendToCardReader(PMercuryFnc.MoveCursor(1, 1) + PMercuryFnc.MoveCursor(1, 20) + DateTime.Now);
                SendToCardReader(DisplayDateTime());
                SendToCardReader(mMercuryLib.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                k = CheckKeyPress(ref mCR_NewValue.DataReceive);
                if (k != "")
                    Addlistbox("Enter key=" + k);
                if (k == "F3")
                {
                    mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                    Addlistbox("[Stop Load]" + mCR_NewValue.RET_TU_ID + "");
                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStop;
                    SendToCardReader(mMercuryLib.MoveCursor(8, 1) + "Stop Load");
                     Thread.Sleep(1000);
                     SendToCardReader(ClearLastLine());
                    //if (M_BATCH_STOP())
                    //{
                    //    M_BATCH_STOP_LOADING();
                    //    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                    //    DisplayToCardReader((int)LoadingStep.LoadingStop);
                    //    Addlistbox("Stop Load[" + mCR_NewValue.RET_TU_ID + "]"+ "[Load No=" + mCR_NewValue.RET_LOAD_HEADER + "][Comp.No=" + mCR_NewValue.CompartmentNo + "]"); 
                    //    Thread.Sleep(5000);
                    //}
                }
                else
                {
                    if (!M_BATCH_CHECK_ALARM())
                    {
                        mCR_NewValue.IsAlarm = true;
                        DisplayMeterAlarm();
                        M_CRBAY_BATCH_STOP();
                        Thread.Sleep(3000);
                        //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStart;
                        //goto Next;
                    }
                    //else
                    //    ClearLastLine();

                    if (M_CRBAY_LOADING())
                    {

                        switch (mCR_NewValue.RET_BATCH_STATUS)
                        {
                            case 3:
                                DisplayToCardReader((int)LoadingStep.LoadingLoad);
                                break;
                            case 4:
                                DisplayToCardReader((int)LoadingStep.LoadingLoad);
                                break;
                            case 5:
                                mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                                mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStop;
                                Thread.Sleep(1000);
                                break;
                            case 6:
                                DisplayToCardReader((int)LoadingStep.LoadingComplete);
                                Addlistbox("[Complete Load]" + mCR_NewValue.RET_TU_ID  + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + " Comp.No=" + mCR_NewValue.CompartmentNo + ".");
                                Thread.Sleep(1000);
                                SendToCardReader(mMercuryLib.ClearDisplay());
                                M_CRBAY_CHECK_STEP();
                                if (mCR_NewValue.RET_STEP == (int)LoadingStep.LoadingNone)
                                {
                                    ClearDataCard();
                                }
                                SendToCardReader(mMercuryLib.ClearDisplay());
                                //DisplayToCardReader(mCR_NewValue.RET_STEP);
                                break;
                            default:
                                break;
                        }
                    }
                }
            //if (!mRunn)
            //    break;
            Next:
                //SendToCardReader(DisplayDateTime());
                Thread.Sleep(600);
            }
        }

        private void CRBayLoading_Stop()
        {
            //M_BATCH_CHECK_LINE_LOADING();
            if (M_CRBAY_BATCH_STOP())
            {
                //Thread.Sleep(2000);
                //M_BATCH_STOP();
                DisplayToCardReader((int)LoadingStep.LoadingStop);
                Thread.Sleep(3000);
                //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                //DisplayToCardReader((int)LoadingStep.LoadingStop);
                
                Addlistbox("[Stop Load]" + mCR_NewValue.RET_TU_ID  + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + " Comp.No=" + mCR_NewValue.CompartmentNo + ".");
                Thread.Sleep(2000);
                if (M_CRBAY_CHECK_STEP())
                {
                    if (mCR_NewValue.RET_STEP == 3)
                    {
                        M_CRBAY_GET_PRESET();
                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStart;
                    }
                    else
                    {
                        DisplayToCardReader(mCR_NewValue.RET_STEP);
                    }
                }
                else
                {
                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                }
                SendToCardReader(mMercuryLib.ClearDisplay());
                SendToCardReader(mMercuryLib.SendNextQueueBlock(),true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
            }
        }

        private void CRBayOperatorConfirm()
        {
            string vRecv = "", k = "",vMsg="";
            SendToCardReader(mMercuryLib.ClearDisplay());
            //Addlistbox("Operator confirm");
            DisplayToCardReader((int)LoadingStep.OperatorConfirm);
            mCR_OldValue = mCR_NewValue;
            while (mCR_NewValue.RET_STEP == (int)LoadingStep.OperatorConfirm)
            {
                DisplayToCardReader((int)LoadingStep.DisplayDateTime);
                SendToCardReader(mMercuryLib.SendNextQueueBlock(),true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                //vRecv = CR_NewValue.DataReceive;
                mCR_NewValue.CardCode = CheckDataCard(ref mCR_NewValue.DataReceive);

                if (Convert.ToInt64(mCR_NewValue.CardCode) > 0)
                {
                   //call procedure 
                    if (M_CRBAY_OPERATOR_CONFIRM())
                    {
                        SendToCardReader(mMercuryLib.MoveCursor(7, 1) + mCR_NewValue.RET_CR_MSG);
                        Thread.Sleep(3000);
                        //SendToCardReader(mMercuryLib.ClearDisplay());
                        mCR_NewValue.RET_STEP = (int)LoadingStep.DriverConfirm;
                    }
                    else
                    {
                        DisplayToCardReader((int)LoadingStep.NoDataFound);
                        DisplayToCardReader((int)LoadingStep.OperatorConfirm);
                    }
                }
                else
                {
                    k = CheckKeyPress(ref mCR_NewValue.DataReceive);
                    if (k == "F3")
                    {
                        Addlistbox("Cancel operator confrim");
                        SendToCardReader(mMercuryLib.ClearDisplay());
                        //vMsg += mMercuryLib.MoveCursor(1, 1) + "55 Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                        vMsg += mMercuryLib.MoveCursor(5, 1) + "         Cancel operator confrim                  ";
                        SendToCardReader(vMsg.ToUpper());
                        Thread.Sleep(5000);
                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        SendToCardReader(mMercuryLib.ClearDisplay());
                        DisplayToCardReader(mCR_NewValue.RET_STEP);
                    }
                }
                //SendToCardReader(DisplayDateTime());
                if (!mRunn)
                    break;
                Thread.Sleep(100);
            }
        }
        private void CRBayOperatorConfirm_Topup()
        {
            string vRecv = "", k = "", vMsg = "";
            SendToCardReader(mMercuryLib.ClearDisplay());
            //Addlistbox("Operator confirm");
            DisplayToCardReader((int)LoadingStep.OperatorConfirm);
            mCR_OldValue = mCR_NewValue;
            while (mCR_NewValue.RET_STEP == (int)LoadingStep.OperatorConfirm)
            {
                SendToCardReader(mMercuryLib.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                //vRecv = CR_NewValue.DataReceive;
                mCR_NewValue.CardCode = CheckDataCard(ref mCR_NewValue.DataReceive);

                if (Convert.ToInt64(mCR_NewValue.CardCode) > 0)
                {
                    //call procedure 
                    if (M_CRBAY_OPERATOR_CONFIRM())
                    {
                        SendToCardReader(mMercuryLib.MoveCursor(7, 1) + mCR_NewValue.RET_CR_MSG);
                        Thread.Sleep(3000);
                        //SendToCardReader(mMercuryLib.ClearDisplay());
                        mCR_NewValue.RET_STEP = (int)LoadingStep.DriverConfirm;
                    }
                }
                else
                {
                    k = CheckKeyPress(ref mCR_NewValue.DataReceive);
                    if (k == "F3")
                    {
                        Addlistbox("Cancel operator confrim");
                        SendToCardReader(mMercuryLib.ClearDisplay());
                        //vMsg += mMercuryLib.MoveCursor(1, 1) + "55 Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                        vMsg += mMercuryLib.MoveCursor(5, 1) + "         Cancel operator confrim                  ";
                        SendToCardReader(vMsg.ToUpper());
                        Thread.Sleep(5000);
                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        SendToCardReader(mMercuryLib.ClearDisplay());
                        DisplayToCardReader(mCR_NewValue.RET_STEP);
                    }
                }
                SendToCardReader(DisplayDateTime());
                if (!mRunn)
                    break;
                Thread.Sleep(600);
            }
        }
        private void CRBayDriverConfirm()
        {
            string vRecv = "", k = "", vMsg = "";
            SendToCardReader(mMercuryLib.ClearDisplay());
            //Addlistbox("Driver confirm");
            DisplayToCardReader((int)LoadingStep.DriverConfirm);
            mCR_OldValue = mCR_NewValue;
            while (mCR_NewValue.RET_STEP == (int)LoadingStep.DriverConfirm)
            {
                //SendToCardReader(DisplayDateTime());
                DisplayToCardReader((int)LoadingStep.DisplayDateTime);
                SendToCardReader(mMercuryLib.SendNextQueueBlock(),true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                //vRecv = CR_NewValue.DataReceive;
                mCR_NewValue.CardCode = CheckDataCard(ref mCR_NewValue.DataReceive);

                if (Convert.ToInt64(mCR_NewValue.CardCode) > 0)
                {
                    //call procedure 
                    if (M_CRBAY_DRIVER_CONFIRM())
                    {
                        SendToCardReader(mMercuryLib.MoveCursor(7, 1) + mCR_NewValue.RET_CR_MSG);
                        Thread.Sleep(3000);
                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        SendToCardReader(mMercuryLib.ClearDisplay());
                        DisplayToCardReader(mCR_NewValue.RET_STEP);
                    }
                    else
                    {
                        DisplayToCardReader((int)LoadingStep.NoDataFound);
                        //Thread.Sleep(3000);
                        DisplayToCardReader((int)LoadingStep.DriverConfirm);
                    }

                }
                else
                {
                    k = CheckKeyPress(ref mCR_NewValue.DataReceive);
                    if (k == "F3")
                    {
                        Addlistbox("Cancel Driver confrim");
                        SendToCardReader(mMercuryLib.ClearDisplay());
                        //vMsg += mMercuryLib.MoveCursor(1, 1) + "55 Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                        vMsg += mMercuryLib.MoveCursor(5, 1) + "         Cancel driver confrim                  ";
                        SendToCardReader(vMsg.ToUpper());
                        Thread.Sleep(5000);
                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        SendToCardReader(mMercuryLib.ClearDisplay());
                    }
                }
                //SendToCardReader(DisplayDateTime());
                if (!mRunn)
                    break;
                Thread.Sleep(100);
            }
        }


        private void CRBayLoadingTopup_Start() //check key F1 for start ,F3 for cancel
        {
            string vRecv = "", k = "";
            mCR_NewValue.IsAlarm = false;
            mCR_OldValue = mCR_NewValue;
            SendToCardReader(mMercuryLib.ClearDisplay());
            DisplayToCardReader(mCR_NewValue.RET_STEP);
            SendToCardReader(mMercuryLib.SendNextQueueBlock(), true);
            while (mCR_NewValue.RET_STEP == (int)LoadingStep.LoadingStart)
            {
                // SendToCardReader(mMercuryLib.MoveCursor(1, 20) + DateTime.Now);
                //SendToCardReader(DisplayDateTime());
                DisplayToCardReader((int)LoadingStep.DisplayDateTime);
                SendToCardReader(mMercuryLib.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                mCR_NewValue.CardCode = CheckDataCard(ref mCR_NewValue.DataReceive);
                if (Convert.ToInt64(mCR_NewValue.CardCode) == 0)
                    k = CheckKeyPress(ref mCR_NewValue.DataReceive);
                //if(k!="")
                //    Addlistbox("Enter key=" + k);
                if (!M_BATCH_CHECK_ALARM())
                {
                    if (!mCR_NewValue.IsAlarm)
                    {
                        DisplayMeterAlarm();
                        Thread.Sleep(500);
                        mCR_NewValue.IsAlarm = true;
                        DisplayToCardReader(mCR_NewValue.RET_STEP);
                    }
                    //DisplayToCardReader(mCR_NewValue.RET_STEP);
                    //goto Next;
                }
                else
                {
                    if (mCR_NewValue.IsAlarm)
                    {
                        mCR_NewValue.IsAlarm = false;
                       SendToCardReader(ClearLastLine());
                    }
                }

                if (k == "F1")
                {
                    mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                    if (!M_BATCH_CHECK_ALARM())
                    {
                        mCR_NewValue.IsAlarm = true;
                        DisplayMeterAlarm();
                        Thread.Sleep(500);
                        //DisplayToCardReader(mCR_NewValue.RET_STEP);
                        goto Next;
                    }
                    // M_CRBAY_CHECK_COMPARTMENT
                    if (M_CRBAY_TOPUP_CHECK_COMP())
                    {
                        mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                        //M_CRBAY_TOPUP_CHECK_COMP();
                        if (M_CRBAY_TOPUP_BATCH_START())//M_CRBAY_BATCH_START
                        {

                            SendToCardReader(mMercuryLib.MoveCursor(8, 1) + "Start Load  ");
                            Thread.Sleep(3000);
                            //M_BATCH_START_LOADING();
                            mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingLoad;
                            Addlistbox("[Top up][Start Load]" + mCR_NewValue.RET_TU_ID + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + " Comp.No=" + mCR_NewValue.CompartmentNo + ".");
                            SendToCardReader(ClearLastLine());
                            //DisplayToCardReader((int)LoadingStep.LoadingLoad);
                            //Thread.Sleep(5000);
                        }
                        else
                        {
                            mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                            DisplayToCardReader(-1);
                            DisplayToCardReader((int)LoadingStep.LoadingStart);
                            DisplayToCardReader(mCR_NewValue.RET_STEP);
                        }
                    }
                    else
                    {
                        //DisplayToCardReader(-1);
                        //DisplayToCardReader((int)LoadingStep.LoadingStart);
                        Addlistbox(mCR_NewValue.RET_MSG);
                        mCR_NewValue.RET_STEP = (int)LoadingStep.NoDataFound;
                        //MessageBox.Show(mCR_NewValue.RET_MSG);
                        ////CRBayLoading_Topup();
                        ClearDataCard();
                        SendToCardReader(mMercuryLib.ClearDisplay());
                        DisplayToCardReader(mCR_NewValue.RET_STEP);

                        //CRBayLoading_Topup();

                        mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        // M_CRBAY_LOAD_CHECK_TU();
                        ClearDataCard();
                        SendToCardReader(mMercuryLib.ClearDisplay());
                        DisplayToCardReader(mCR_NewValue.RET_STEP);
                    }
                }
                else if (k == "F3")
                {
                    mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                    mCR_NewValue.RET_LOAD_HEADER = mCR_OldValue.RET_LOAD_HEADER;
                    mCR_NewValue.RET_CR_MSG = "Cancle Load";

                    Addlistbox("[Top up][Canel Load]" + mCR_NewValue.RET_TU_ID + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + " Comp.No=" + mCR_NewValue.CompartmentNo + ".");
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    DisplayToCardReader((int)LoadingStep.LoadingCancel);
                    mCR_NewValue.RET_CR_MSG = "";
                    SendToCardReader(ClearLastLine());
                    Thread.Sleep(3000);

                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                    M_CRBAY_LOAD_CHECK_TU_TOPUP();
                    ClearDataCard();
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                    //CRBayLoading_Stop();
                    //if (mCR_NewValue.RET_IS_BLENDING == 0)
                    //{
                    //    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                    //    M_CRBAY_TOPUP_LOAD_CHECK_TU();
                    //    ClearDataCard();
                    //    SendToCardReader(mMercuryLib.ClearDisplay());
                    //    DisplayToCardReader(mCR_NewValue.RET_STEP);
                    //}
                    //else
                    //{
                    //    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                    //}
                }
                else if (Convert.ToInt64(mCR_NewValue.CardCode) > 0)
                {
                    mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                    mCR_NewValue.RET_LOAD_HEADER = mCR_OldValue.RET_LOAD_HEADER;
                    mCR_NewValue.RET_CR_MSG = "Cancle Load";
                    //mCR_NewValue.CompartmentNo = 1;
                    Addlistbox("[Top up][Canel Load]" + mCR_NewValue.RET_TU_ID + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + " Comp.No=" + mCR_NewValue.CompartmentNo + ".");
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    DisplayToCardReader((int)LoadingStep.LoadingCancel);
                    mCR_NewValue.RET_CR_MSG = "";
                    SendToCardReader(ClearLastLine());
                    Thread.Sleep(3000);
                    //CRBayLoading_Stop();
                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                    M_CRBAY_LOAD_CHECK_TU_TOPUP();
                    ClearDataCard();
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                   
                    //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStop;
                }
            Next:
                if (!mRunn)
                    break;
                //SendToCardReader(DisplayDateTime());
                Thread.Sleep(600);
            }
        }

        private void CRBayLoadingTopup_Load() //check key F1 for start ,F3 for stop
        {
            string vRecv = "", k = "";
            DisplayToCardReader((int)LoadingStep.LoadingLoad);
            Addlistbox("[Top up][Loading]" + mCR_NewValue.RET_TU_ID + " Loading in progress" + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + " Comp.No=" + mCR_NewValue.CompartmentNo + ".");

            mCR_NewValue.IsAlarm = false;

            while (mCR_NewValue.RET_STEP == (int)LoadingStep.LoadingLoad)
            {
                if (!mRunn)
                    break;
                //SendToCardReader(PMercuryFnc.MoveCursor(1, 1) + PMercuryFnc.MoveCursor(1, 20) + DateTime.Now);
                //SendToCardReader(DisplayDateTime());
                DisplayToCardReader((int)LoadingStep.DisplayDateTime);
                SendToCardReader(mMercuryLib.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                k = CheckKeyPress(ref mCR_NewValue.DataReceive);
                if (k != "")
                    Addlistbox("Enter key=" + k);
                if (k == "F3")
                {
                    mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                    Addlistbox("[Top up][Stop Load]" + mCR_NewValue.RET_TU_ID);
                    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStop;
                    SendToCardReader(mMercuryLib.MoveCursor(8, 1) + "Stop Load");
                    SendToCardReader(ClearLastLine());
                    Thread.Sleep(1000);
                    //if (M_BATCH_STOP())
                    //{
                    //    M_BATCH_STOP_LOADING();
                    //    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                    //    DisplayToCardReader((int)LoadingStep.LoadingStop);
                    //    Addlistbox("Stop Load[" + mCR_NewValue.RET_TU_ID + "]"+ "[Load No=" + mCR_NewValue.RET_LOAD_HEADER + "][Comp.No=" + mCR_NewValue.CompartmentNo + "]"); 
                    //    Thread.Sleep(5000);
                    //}
                }
                else
                {
                    if (!M_BATCH_CHECK_ALARM())
                    {
                        mCR_NewValue.IsAlarm = true;
                        DisplayMeterAlarm();
                        M_CRBAY_TOPUP_BATCH_STOP();
                        SendToCardReader(ClearLastLine());
                        Thread.Sleep(3000);
                        //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStart;
                        //goto Next;
                    }
                    //else
                    //    ClearLastLine();

                    if (M_CRBAY_TOPUP_LOADING())
                    {

                        switch (mCR_NewValue.RET_BATCH_STATUS)
                        {
                            case 3:
                                DisplayToCardReader((int)LoadingStep.LoadingLoad);
                                break;
                            case 4:
                                DisplayToCardReader((int)LoadingStep.LoadingLoad);
                                break;
                            case 5:
                                mCR_NewValue.CardCode = mCR_OldValue.CardCode;
                                mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStop;
                                Thread.Sleep(1000);
                                break;
                            case 6://เมื่อเติม TOPUP เสร็จ
                                DisplayToCardReader((int)LoadingStep.LoadingComplete);
                                Addlistbox("[Top up][Complete Load]" + mCR_NewValue.RET_TU_ID + " Load No=" + mCR_NewValue.RET_LOAD_HEADER + " Comp.No=" + mCR_NewValue.CompartmentNo + ".");
                                Thread.Sleep(1000);
                                SendToCardReader(mMercuryLib.ClearDisplay());
                                //M_CRBAY_TOPUP_CHECK_STEP();
                                //if (mCR_NewValue.RET_STEP == (int)LoadingStep.LoadingNone)
                                //{
                                //    ClearDataCard();
                                //}
                                //SendToCardReader(mMercuryLib.ClearDisplay());
                                ////DisplayToCardReader(mCR_NewValue.RET_STEP);
                                mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                                ClearDataCard();
                                SendToCardReader(mMercuryLib.ClearDisplay());
                                DisplayToCardReader(mCR_NewValue.RET_STEP);
                                break;
                            default:
                                break;
                        }
                    }
                }
            //if (!mRunn)
            //    break;
            Next:
                //SendToCardReader(DisplayDateTime());
                Thread.Sleep(600);
            }
        }

        private void CRBayLoadingTopup_Stop()
        {
            //M_BATCH_CHECK_LINE_LOADING();
            if (M_CRBAY_TOPUP_BATCH_STOP())
            {
                //Thread.Sleep(2000);
                //M_BATCH_STOP();
                DisplayToCardReader((int)LoadingStep.LoadingStop);
                Thread.Sleep(3000);
                //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                //DisplayToCardReader((int)LoadingStep.LoadingStop);

                Addlistbox("Stop Load[" + mCR_NewValue.RET_TU_ID + "]" + "[Load No=" + mCR_NewValue.RET_LOAD_HEADER + "][Comp.No=" + mCR_NewValue.CompartmentNo + "]");
                Thread.Sleep(2000);

                // M_CRBAY_TOPUP_CHECK_STEP();
                //if (M_CRBAY_TOPUP_CHECK_STEP())
                // {
                //     //if (mCR_NewValue.RET_STEP == 3)
                //     if (mCR_NewValue.RET_STEP == 5)
                //     {
                //         M_CRBAY_GET_TOPUP_PRESET();
                //         mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStart;
                //     }
                //     else
                //     {
                //         DisplayToCardReader(mCR_NewValue.RET_STEP);
                //     }
                // }
                // else
                // {
                //     mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                //     SendToCardReader(mMercuryLib.ClearDisplay());
                //     DisplayToCardReader(mCR_NewValue.RET_STEP);
                // }
                M_CRBAY_TOPUP_GET_PRESET();
                mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStart;
                SendToCardReader(mMercuryLib.ClearDisplay());
                DisplayToCardReader(mCR_NewValue.RET_STEP);

                SendToCardReader(mMercuryLib.ClearDisplay());
                SendToCardReader(mMercuryLib.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
            }
        }

        private void CheckIsNumeric()
        {
            
        }

        private string CheckDataCard(ref string pRecv)
        {
            string CardCode = "0";
            string temp = "";
            //Thread.Sleep(300);
            //SendToCardReader(PMercuryFnc.SendNextQueueBlock());
            //Thread.Sleep(500);
            if (pRecv != "")
            {
                string addr = mCR_NewValue.Address.ToString("00D");
                int vIndex = pRecv.IndexOf(char.ConvertFromUtf32(2) + addr);
                if ((vIndex > -1) && pRecv.Length > 5)
                {
                    //01D\0Y , 01DC005748EB\0W -> touch card
                    //01DAR\0F -> press R
                    //01DAA\0W -> press A
                    //01DDOP\0[ -> press F1
                    if ((mSTX_Position < 0) || (mETX_Position < mSTX_Position))
                    {
                        mSTX_Position = pRecv.IndexOf(char.ConvertFromUtf32(2));
                        mETX_Position = pRecv.IndexOf(char.ConvertFromUtf32(3));
                        if (mETX_Position < mSTX_Position)
                        {
                            mETX_Position = pRecv.IndexOf(char.ConvertFromUtf32(3),mSTX_Position);
                        }
                        if ((mSTX_Position == -1) || (mETX_Position == -1))
                        {
                            return "0";
                        }
                    }
                    temp = pRecv.Substring(mSTX_Position, mETX_Position - mSTX_Position + 1);
                    vIndex = pRecv.IndexOf("DC");

                    if ((vIndex == 3) || (vIndex-mSTX_Position==3))
                    {
                        temp = pRecv.Substring(vIndex + 2, pRecv.Length - 2 - vIndex).Trim();
                        if ((temp.IndexOf("\0") > -1) && pRecv.Length > 10)
                        {
                            temp = temp.Substring(0, temp.IndexOf("\0"));
                            CardCode = int.Parse(temp, System.Globalization.NumberStyles.HexNumber).ToString();
                            Addlistbox("Card Code = " + CardCode);
                        }
                        else
                        {
                            CardCode = "0";
                        }

                    }
                    else
                    {
                        CardCode = CheckEnterCardCode(ref pRecv);
                    }
                }
            }
            else
                mPort.ReceiveData();

            return CardCode;
        }

        private string CheckEnterCardCode(ref string pData)
        {
            mCR_NewValue.bTimeOut=false;
            string k="",vRecv="";
            k = CheckKeyPress(ref pData);
            if (k == "F6")
            {
                CheckIsKeypad = true;
                SendToCardReader(mMercuryLib.DeleteAllStoreMessage());
                Thread.Sleep(300);
                SendToCardReader(mMercuryLib.ClearDisplay());
                mCR_OldValue.RET_STEP = mCR_NewValue.RET_STEP;
                mCR_NewValue.RET_STEP = (int)LoadingStep.EnterCard;
                DisplayToCardReader(mCR_NewValue.RET_STEP);
                Addlistbox("Enter key=" + k);
                Addlistbox("Enter Card Code");
                DisplayToCardReader(mCR_NewValue.RET_STEP);
                //Thread.Sleep(500);
                mCR_NewValue.DateTimeStart = DateTime.Now;
               
                //while ((DateTime.Now - vTimeActive).TotalSeconds < 30)
                //mCR_OldValue.RET_STEP = mCR_NewValue.RET_STEP;
                while(!mCR_NewValue.bTimeOut)
                {
                    //SendToCardReader(mMercuryLib.MoveCursor(1, 20) + DateTime.Now);
                    //Thread.Sleep(500);
                    SendToCardReader(DisplayDateTime());
                    SendToCardReader(mMercuryLib.SendNextQueueBlock(),true);
                    //Thread.Sleep(300);
                    //ReadFromCardReader();
                    k = CheckKeyPress(ref mCR_NewValue.DataReceive);
                    if (k == "F7")
                    {
                        CheckIsKeypad = false;
                        Addlistbox("Enter key=" + k);
                        Addlistbox("Cancel Enter Card Code");
                        //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        mCR_NewValue.RET_STEP = mCR_OldValue.RET_STEP;
                        SendToCardReader(mMercuryLib.ClearDisplay());
                        SendToCardReader(ClearLastLine());
                        //DisplayToCardReader(mCR_NewValue.RET_STEP);
                        break;
                    }
                    else
                    {
                        if (k != "")
                        {
                            Addlistbox("Enter key=" + k);
                            mCR_NewValue.RET_STEP = mCR_OldValue.RET_STEP;
                            SendToCardReader(mMercuryLib.ClearDisplay());
                            SendToCardReader(ClearLastLine());
                            if(mCR_NewValue.RET_STEP != (int)LoadingStep.LoadingNone)
                                DisplayToCardReader(mCR_NewValue.RET_STEP);
                            return k;
                        }
                    }

                    if (mCR_NewValue.bTimeOut = (DateTime.Now - mCR_NewValue.DateTimeStart).TotalSeconds < 30)
                        mCR_NewValue.bTimeOut = false;
                    else
                        mCR_NewValue.bTimeOut = true;

                    //SendToCardReader(DisplayDateTime());
                    if (!mRunn)
                        break;
                    Thread.Sleep(600);
                }
                if (mCR_NewValue.bTimeOut)
                {
                    Addlistbox("Cancel Enter Card Code[Time out]");
                    mCR_NewValue.bTimeOut = false;
                    mCR_NewValue.RET_STEP = mCR_OldValue.RET_STEP;
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    SendToCardReader(ClearLastLine());
                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                    SendToCardReader(ClearLastLine());
                    //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                }
            }
            else
            {
                if (k != "")
                {
                    Addlistbox("Enter key=" + k);
                    mCR_NewValue.RET_STEP = mCR_OldValue.RET_STEP;
                    SendToCardReader(ClearLastLine());
                    DisplayToCardReader(mCR_NewValue.RET_STEP);
                }
            }
            return "0";
        }

        private string CheckKeyPress(ref string pData)
        {
            string vKeyPress = "";
            try
            {                
                if (pData != null)
                {
                    string addr = mCR_NewValue.Address.ToString("00D");
                    int vIndex = pData.IndexOf(char.ConvertFromUtf32(2) + addr);
                    if ((vIndex > -1) && pData.Length > 5)
                    {
                        //01D\0Y -> touch card
                        //01DAR\0F -> press R
                        //01DAA\0W -> press A
                        //01DDOP\0[ -> press F1
                        vKeyPress = mMercuryLib.CheckKeyPress(mCR_NewValue.Address, pData);
                        if (vKeyPress == "")
                        {
                            string temp = "";
                            if ((mSTX_Position < 0) || (mETX_Position < mSTX_Position))
                            {
                                mSTX_Position = pData.IndexOf(char.ConvertFromUtf32(2));
                                mETX_Position = pData.IndexOf(char.ConvertFromUtf32(3));
                                if ((mSTX_Position == -1) || (mETX_Position == -1))
                                {
                                    return "";
                                }
                            }

                            vIndex = pData.IndexOf("DA");

                            if ((vIndex == 3) || (vIndex - mSTX_Position == 3))
                            {
                                temp = pData.Substring(vIndex + 2, pData.Length - 2 - vIndex).Trim();
                                if (temp.IndexOf("\0") > -1)
                                {
                                    vKeyPress = temp.Substring(0, temp.IndexOf("\0"));
                                    //Addlistbox("Enter key=" + KeyPress);
                                }
                                else
                                {
                                    vKeyPress = temp.Substring(0, temp.Length - 2);
                                }
                            }
                        }
                        //else
                        //{
                        //    Addlistbox("Enter key=" + KeyPress);
                        //}
                    }
                }
            }
            catch (Exception exp)
            {
            }

            return vKeyPress;
        }

        private bool SendToCardReader(string pMsg,bool pRead)
        {
            lock (mThreadLock)
            {
                try
                {
                    if (!mPort.IsOpen())
                        return false;
                    byte[] b = BuildMsg((byte)mCR_NewValue.Address, pMsg);

                    mPort.SendData(b);
                    mCR_NewValue.DataSend = Encoding.ASCII.GetString(b);

                    Thread.Sleep(200);
                    if (pRead)
                    {
                        ReadFromCardReader();
                    }
                    return true;
                }
                catch (Exception exp)
                { return false; }
            }
        }

        private bool SendToCardReader(string pMsg)
        {
            lock (mThreadLock)
            {
                try
                {
                    if (!mPort.IsOpen())
                        return false;
                    byte[] b = BuildMsg((byte)mCR_NewValue.Address, pMsg);

                    mPort.SendData(b);
                    mCR_NewValue.DataSend = Encoding.ASCII.GetString(b);

                    return true;
                }
                catch (Exception exp)
                { return false; }
            }
        }

        private bool ReadFromCardReader()
        {
            lock (mThreadLock)
            {
                bool vCheck = false;
                try
                {
                    if (!mPort.IsOpen())
                    {
                        CheckResponse(false);
                        //return false;
                    }
                    //string s = PPort.ReceiveData();
                    string s = mPort.ReceiveData();
                    //mCR_NewValue.DataReceive = mPort.ReceiveData();
                    //string s = mCR_NewValue.DataReceive;
                    if ((s != "") && (s != null))
                    {
                        if (CheckBlockRecv(s))
                        {
                            //mCR_NewValue.DataReceive = s;
                            mCR_NewValue.Connect = true;
                            CheckResponse(true);
                            //pRecv = s;
                            vCheck = true;
                        }
                        //return true;
                    }
                    else
                    {
                        mCR_NewValue.DataReceive = "";
                        CheckResponse(false);
                        //pRecv = "";

                        //return false;
                    }
                }
                catch (Exception exp)
                {
                    //return false;
                }
                return vCheck;
            }
        }

        private void DisplayToCardReader(int pDisplay)
        {
            string vMsg = "";
            string s = "";
            //SendToCardReader(mMercuryLib.MoveCursor(8, 1) + s.PadRight(40, ' '));  //clear last line
            M_CRBAY_GET_METER_NAME();
           // M_CRBAY_CHECK_TOPUP();
            //MessageBox.Show(mCR_NewValue.ModeStatus.ToString()) ;
            
            switch (pDisplay)
            {
                case (int)LoadingStep.NoDataFound:
                    vMsg += mMercuryLib.ClearDisplay();
                    vMsg += mMercuryLib.MoveCursor(1, 1) + "99\n         No data found          ";
                    vMsg += mMercuryLib.MoveCursor(4, 1) + " Card: " + mCR_NewValue.CardCode;
                    //vMsg += PMercuryFnc.MoveCursor(5, 1) + " Compartment: " + mCR_NewValue.CompartmentNo;
                    vMsg += mMercuryLib.MoveCursor(5, 1) + " " + mCR_NewValue.RET_CR_MSG;
                    vMsg += mMercuryLib.MoveCursor(6, 1) + " " + mCR_NewValue.RET_MSG_BATCH1 + " " + mCR_NewValue.RET_MSG_BATCH2;
                    //SendToCardReader(PMercuryFnc.MoveCursor(8, 1) + "                              ");  //clear last line
                    SendToCardReader(vMsg.ToUpper());
                    Thread.Sleep(5000);
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    Thread.Sleep(500);
                    //SendToCardReader(mMercuryLib.SendNextQueueBlock(),true);
                    vMsg = "";
                    SendToCardReader(mMercuryLib.DeleteAllStoreMessage());
                    SendToCardReader(mMercuryLib.MoveCursor(8, 1) + s.PadRight(40, ' '));  //clear last line
                    break;
                case (int)LoadingStep.SystemOffLine:
                    vMsg += mMercuryLib.ClearDisplay();
                    vMsg += mMercuryLib.MoveCursor(1, 1) + "99\n\n        System Offline     ";
                    SendToCardReader(mMercuryLib.SendNextQueueBlock(),false);
                    SendToCardReader(ClearLastLine());
                    break;
                case (int)LoadingStep.LoadingNone:
                    //SendToCardReader(mMercuryLib.DeleteAllStoreMessage());
                    vMsg += mMercuryLib.MoveCursor(1, 1) + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                    if (mCR_NewValue.ModeStatus == 1)//ตรวสอบสถานะ โหมด Topup 
                    {
                        vMsg += mMercuryLib.MoveCursor(3, 1) + "             TOPUP MODE            ";
                        vMsg += mMercuryLib.MoveCursor(5, 1) + "                        ";
                        vMsg += mMercuryLib.MoveCursor(7, 1) + "         Please Touch Card                ";
                    }
                    else
                    {
                        vMsg += mMercuryLib.MoveCursor(3, 1) + "                                   ";
                        //vMsg += mMercuryLib.MoveCursor(7, 1) + "         Please Touch Card                  ";
                        vMsg += mMercuryLib.MoveCursor(7, 1) + "         Please Touch Card                ";
                        vMsg += mMercuryLib.MoveCursor(5, 1) + "                        ";
                    }
                    //vMsg += mMercuryLib.MoveCursor(7, 1) + "         Please Touch Card (tetee)                 ";
                    //Msg += PMercuryFnc.MoveCursor(7, 1) + "" + DateTime.Now;
                    break;
                case (int)LoadingStep.EnterCard:
                    vMsg += mMercuryLib.MoveCursor(1, 1) + pDisplay.ToString() + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                    //vMsg += PMercuryFnc.MoveCursor(3, 1) + "      Welcome to Sakchaisit System";
                    vMsg += mMercuryLib.MoveCursor(7, 1) + "         Please Enter Card Code             ";
                    //Msg += PMercuryFnc.MoveCursor(7, 1) + "" + DateTime.Now;
                    break;
                case (int)LoadingStep.LoadingCancel:
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    if (mCR_NewValue.ModeStatus == 1)//ตรวสอบสถานะ โหมด Topup 
                    {
                        vMsg += mMercuryLib.MoveCursor(1, 1) + pDisplay.ToString() + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                        vMsg += mMercuryLib.MoveCursor(3, 1) + " Card: " + mCR_NewValue.CardCode + mMercuryLib.MoveCursor(4, 1) + " Load No: " + mCR_NewValue.RET_LOAD_HEADER;
                        vMsg += mMercuryLib.MoveCursor(4, 20) + "   Topup No:" + mCR_NewValue.RET_TOPUP_NO.ToString();
                        vMsg += mMercuryLib.MoveCursor(7, 1) + "Cancle topup";
                    }
                    else
                    {
                        vMsg += mMercuryLib.MoveCursor(1, 1) + pDisplay.ToString() + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                        vMsg += mMercuryLib.MoveCursor(3, 1) + " Card: " + mCR_NewValue.CardCode + mMercuryLib.MoveCursor(3, 20) + "Load No: " + mCR_NewValue.RET_LOAD_HEADER;
                        vMsg += mMercuryLib.MoveCursor(5, 1) + "          " + mCR_NewValue.RET_CR_MSG;
                    }
                    
                    break;
                case (int)LoadingStep.LoadingCompartment:
                    vMsg += mMercuryLib.ClearDisplay();
                    vMsg += mMercuryLib.MoveCursor(1, 1) + pDisplay.ToString() + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                    vMsg += mMercuryLib.MoveCursor(3, 1) + " Card: " + mCR_NewValue.CardCode + mMercuryLib.MoveCursor(3, 20) + "Load No: " + mCR_NewValue.RET_LOAD_HEADER;
                    //vMsg += mMercuryLib.MoveCursor(5, 1) + " Please Enter Compartment: " + mCR_NewValue.RET_COMPARTMENT_LIST;
                    vMsg += mMercuryLib.MoveCursor(5, 1) + " Please Select Product: " + mCR_NewValue.RET_COMPARTMENT_LIST;
                    break;
                case (int)LoadingStep.LoadingStart:
                    vMsg = "";
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    vMsg += mMercuryLib.MoveCursor(1, 1) + pDisplay.ToString() + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                    vMsg += mMercuryLib.MoveCursor(2, 1) + " Meter  : " + mCR_NewValue.RET_METER_NAME + "  Card: " + mCR_NewValue.CardCode ;
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = "";
                    Thread.Sleep(100);
                    if (mCR_NewValue.ModeStatus == 0) //tetee
                        vMsg += mMercuryLib.MoveCursor(3, 1) + " Load No: " + mCR_NewValue.RET_LOAD_HEADER;
                    else
                    {
                        vMsg += mMercuryLib.MoveCursor(3, 1) + " Load No: " + mCR_NewValue.RET_LOAD_HEADER + "   TOPUP NO:" + mCR_NewValue.RET_TOPUP_NO.ToString();
                        SendToCardReader(vMsg.ToUpper());
                    }
                    vMsg = "";
                    vMsg += mMercuryLib.MoveCursor(4, 1) + " Product: " + mCR_NewValue.RET_SALE_PRODUCT_NAME;
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = "";
                    Thread.Sleep(100);
                    //Msg += PMercuryFnc.MoveCursor(4, 1) + " Line No: " + CR_NewValue.RET_LOAD_LINE;
                    if (mCR_NewValue.ModeStatus == 0)
                        vMsg += mMercuryLib.MoveCursor(5, 1) + " Product No: " + mCR_NewValue.CompartmentNo + "  Preset: " + mCR_NewValue.RET_PRESET;
                    else
                        vMsg += mMercuryLib.MoveCursor(5, 1) + " Preset : " + mCR_NewValue.RET_PRESET;
                    vMsg += mMercuryLib.MoveCursor(7, 1) + " Press F1 to START or F3 to Cancel          ";
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = "";
                    Thread.Sleep(100);
                    break;
                case (int)LoadingStep.LoadingLoad:
                    //Msg += PMercuryFnc.ClearDisplay();
                    vMsg += mMercuryLib.MoveCursor(1, 1) + pDisplay.ToString() + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                    vMsg += mMercuryLib.MoveCursor(2, 1) + " Meter  : " + mCR_NewValue.RET_METER_NAME + "  Card: " + mCR_NewValue.CardCode;
                    vMsg += mMercuryLib.MoveCursor(6, 1) + " Loaded: " + mCR_NewValue.RET_LOADED_MASS  + " F/R: " + mCR_NewValue.RET_FLOWRATE;
                    vMsg += mMercuryLib.MoveCursor(7, 1) + " Press F3 to STOP                           ";
                    break;
                case (int)LoadingStep.LoadingStop:
                    SendToCardReader(mMercuryLib.ClearDisplay());
                    if (mCR_NewValue.ModeStatus == 0)
                    {
                        vMsg += mMercuryLib.MoveCursor(1, 1) + pDisplay.ToString() + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                        vMsg += mMercuryLib.MoveCursor(3, 1) + " Card: " + mCR_NewValue.CardCode + mMercuryLib.MoveCursor(3, 20) + "Load No: " + mCR_NewValue.RET_LOAD_HEADER;
                        vMsg += mMercuryLib.MoveCursor(5, 1) + "       Stop Load                            ";
                        SendToCardReader(vMsg.ToUpper());
                        vMsg = "";
                    }
                    else
                    {
                        vMsg += mMercuryLib.MoveCursor(1, 1) + pDisplay.ToString() + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                        vMsg += mMercuryLib.MoveCursor(3, 1) + " Card: " + mCR_NewValue.CardCode + mMercuryLib.MoveCursor(4, 1) + " Load No: " + mCR_NewValue.RET_LOAD_HEADER;
                        vMsg += mMercuryLib.MoveCursor(4, 20) + "   Topup No:" + mCR_NewValue.RET_TOPUP_NO.ToString();
                        vMsg += mMercuryLib.MoveCursor(7, 1) + "Stop Load                            ";
                        SendToCardReader(vMsg.ToUpper());
                        vMsg = "";
                    }
                    Thread.Sleep(5000);
                    break;
                case (int)LoadingStep.LoadingComplete:
                    vMsg += mMercuryLib.MoveCursor(1, 1) + pDisplay.ToString() + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                   // vMsg += mMercuryLib.MoveCursor(5, 1) + " Product: " + mCR_NewValue.RET_SALE_PRODUCT_NAME + " Product No: " + mCR_NewValue.CompartmentNo;
                    vMsg += mMercuryLib.MoveCursor(6, 1) + " Loaded: " + mCR_NewValue.RET_LOADED_MASS ;
                    vMsg += mMercuryLib.MoveCursor(7, 1) + " Complete Load                              ";
                    if (mCR_NewValue.ModeStatus == 0)//ตรวจสอบโหมด Load
                        vMsg += mMercuryLib.MoveCursor(5, 1) + " Product: " + mCR_NewValue.RET_SALE_PRODUCT_NAME + " Product No: " + mCR_NewValue.CompartmentNo;
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = "";
                    Thread.Sleep(7000);
                    break;
                case (int)LoadingStep.OperatorConfirm:
                    vMsg += mMercuryLib.MoveCursor(1, 1) + pDisplay.ToString() + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                    vMsg += mMercuryLib.MoveCursor(2, 1) + "         Operator confirm                   ";
                    vMsg += mMercuryLib.MoveCursor(4, 1) + "seal use    : " + mCR_NewValue.RET_SEAL_USE;
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = "";
                    vMsg += mMercuryLib.MoveCursor(5, 1) + "seal number : " + mCR_NewValue.RET_SEAL_NUMBER;
                    vMsg += mMercuryLib.MoveCursor(6, 1) + "         Please Touch Card                  ";
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = "";
                    break;
                case (int)LoadingStep.DriverConfirm:
                    vMsg += mMercuryLib.MoveCursor(1, 1) + pDisplay.ToString() + " Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                    vMsg += mMercuryLib.MoveCursor(2, 1) + "         driver confirm                     ";
                    vMsg += mMercuryLib.MoveCursor(4, 1) + "seal use    : " + mCR_NewValue.RET_SEAL_USE;
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = "";
                    vMsg += mMercuryLib.MoveCursor(5, 1) + "seal number : " + mCR_NewValue.RET_SEAL_NUMBER;
                    vMsg += mMercuryLib.MoveCursor(6, 1) + "         Please Touch Card                  ";
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = "";
                    break;
                case (int)LoadingStep.DisplayDateTime:
                    mCR_NewValue.TimeSend = DateTime.Now;
                    if ((mCR_NewValue.TimeSend - mCR_OldValue.TimeSend).TotalSeconds > 1)
                    {
                        SendToCardReader(DisplayDateTime());
                        mCR_OldValue.TimeSend = mCR_NewValue.TimeSend;
                    }
                    break;
                default:
                    break;
            }
            //SendToCardReader(PMercuryFnc.ClearDisplay());
            if (vMsg.Length > 0)
            {
                //SendToCardReader(PMercuryFnc.MoveCursor(1, 1) + Msg);
                SendToCardReader(vMsg.ToUpper());
            }
            //if (mCR_NewValue.Tmode != mCR_NewValue.ModeStatus)//ตรวจสอบโหมด Topup 
            //{
            //    RunProcess();
            //}

               //SendToCardReader(PMercuryFnc.MoveCursor(8, 1) + "                              ");  //clear last line
        }

        private void ClearDataCard()
        {
            mCR_NewValue.CompartmentNo = 0;
            mCR_NewValue.RET_BATCH_STATUS = 0;
            mCR_NewValue.RET_CHECK = -1;
            mCR_NewValue.RET_COMPARTMENT_LIST = "";
            mCR_NewValue.RET_CR_MSG = "";
            mCR_NewValue.RET_DENSITY30C = 0;
            mCR_NewValue.RET_FLOWRATE = "0";
            mCR_NewValue.RET_LOAD_COUNT = 0;
            mCR_NewValue.RET_LOAD_HEADER = 0;
            mCR_NewValue.RET_LOAD_LINE = 0;
            mCR_NewValue.RET_LOAD_STATUS = 0;
            mCR_NewValue.RET_LOADED_MASS = "0";
            mCR_NewValue.RET_METER_NO = "";
            mCR_NewValue.RET_MSG = "";
            mCR_NewValue.RET_MSG_BATCH1 = "";
            mCR_NewValue.RET_MSG_BATCH2 = "";
            mCR_NewValue.RET_PRESET = 0;
            mCR_NewValue.RET_RECIPES_NO = 0;
            mCR_NewValue.RET_TOT_COMPARTMENT = 0;
            mCR_NewValue.RET_VCF30 = 0;
            mCR_NewValue.RET_TU_ID = "";
            mCR_NewValue.RET_SEAL_USE = "";
            mCR_NewValue.RET_SEAL_NUMBER = "";
            mCR_OldValue = mCR_NewValue;
        }

        private string DisplayDateTime()
        {
            //Thread.Sleep(50);
            return mMercuryLib.MoveCursor(1, 20) + DateTime.Now;
        }

        private void DisplayMeterAlarm()
        {
            string[] s; 
            string vMsg = "";
            M_CRBAY_GET_METER_NAME();
            Addlistbox("[" + mCR_NewValue.RET_METER_NAME + "] " + mCR_NewValue.RET_MSG);
            s=mCR_NewValue.RET_MSG.Split('-');
            //vMsg += mMercuryLib.ClearDisplay();
            //vMsg += mMercuryLib.MoveCursor(1, 1) + "99                                       ";
            //vMsg += mMercuryLib.MoveCursor(3, 1) + "            [" + mCR_NewValue.RET_METER_NO + "]Meter Alarm" ;
            //vMsg += mMercuryLib.MoveCursor(4, 1) + " " + s[1];
            SendToCardReader(mMercuryLib.MoveCursor(8, 1) + mCR_NewValue.RET_MSG);  
            SendToCardReader(vMsg.ToUpper());
            //Thread.Sleep(5000);
            //SendToCardReader(mMercuryLib.ClearDisplay());
            //Thread.Sleep(500);
            //SendToCardReader(mMercuryLib.SendNextQueueBlock());
            //vMsg = "";
            Thread.Sleep(1000);
        }

        private string ClearLastLine()
        {
            string s = " ";
            s += s.PadRight(38, ' ');
            return mMercuryLib.MoveCursor(8, 1) + s.PadRight(38,' ') + mMercuryLib.MoveCursor(8,1);
        }

        #endregion

        private bool CheckIsKeypad
        {
            get { return mCR_NewValue.IsKeypad; }
            set { mCR_NewValue.IsKeypad = value; }
        }

        private void Addlistbox(string iMsg)
        {
            try
            {
                mCRProcess.WriteLogCardReader(">[Bay=" + mCR_NewValue.BayNo + "]" + mCR_NewValue.Name  +"->" + iMsg);
                //mFMercury.AddListBox = (object)DateTime.Now + ">[Bay=" + mCR_NewValue.BayNo + "]" + mCR_NewValue.Name  +"->" + iMsg;
            }
            catch (Exception exp)
            { }
        }

        public void InitialCardReader(string pCardReaderID)
        {
            try
            {
                string strSQL = "select t.card_reader_name,t.card_reader_address,t.bay_no " +
                                " from tas.VIEW_CR_BAY t" +
                                " where t.card_reader_id=" + pCardReaderID;

                DataSet ds = new DataSet();
                DataTable dt;
                string vMsg = "";

                if (mFMercury.mOraDb.OpenDyns(strSQL, "TableName", ref ds))
                {
                    dt = ds.Tables["TableName"];
                    if (dt.Rows.Count > 0)
                    {
                        mCR_NewValue.Address = Convert.ToInt32(dt.Rows[0]["card_reader_address"]);
                        mCR_NewValue.ID = pCardReaderID;
                        mCR_NewValue.Name = dt.Rows[0]["card_reader_name"].ToString();
                        mCR_NewValue.BayNo = dt.Rows[0]["bay_no"].ToString();

                        vMsg = "Create " + mCR_NewValue.Name + " successful.[Adddress=" + mCR_NewValue.Address + "]";
                    }
                    else
                    {
                        vMsg = "Cannot find Card Reader id[" + pCardReaderID + "]";
                    }
                    Addlistbox(vMsg);
                }
            }
            catch (Exception exp)
            {
                mLog.WriteErrLog(exp.Message);
            }

        }


    }
}
