using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO.Ports;

namespace PCRBAY
{
    public class CardReaderProcess : IDisposable
    {
        #region Constant, Struct and Enum
        public enum _LoadingMode : int
        {
            TouchCard = 0,
            Loading=1,
            Topup=2,
            CancelLoad=3
        }
        public struct _LoadingDetail
        {
            public bool Enable;
            public bool SelectCompNo;
            public string ArmNo;
            public string MeterNo;
            public string Product;
            public string Advice;
            public string Loaded;
            public string Unit;
            public string Additive;
            public string Message;
        }
        public struct _LoadingInfo
        {
            public bool IsLoading;
            public bool OldLoading;
            public string LoadHeaderNo;
            public string ShipmentNo;
            public string TUCardNo;
            public string TUCardNo1;
            public string CardNo;
            public string DriverName;
            public string msgLOAD;
            public string sysdate;
        }
        public enum _BayLocation : int
        {
            BayA=0,
            BayB
        }
        public enum _CRStepProcess :int
        {
            LoadingNone= 0
                ,
            NoDataFound = -1
                ,
            LoadingCancel = -2
                ,
            EnterCard = 11
                ,
            LoadingCompartment = 30
                ,
            DisplayCompartment = 31
            //    ,
            //EnterCompartment = 31
                ,
            ShowPreset = 32
                ,
            LoadingStart = 51
                ,
            LoadingLoad = 53
                ,
            LoadingStop=54
                ,
            OperatorConfirm = 55
                ,
            DriverConfirm = 61
                ,
            WipeCard = 91
                ,
            DisplayNoLoading = 92
                , 
            LoadingInvalid = 93
                ,
            DisplayDateTime = 97
                ,
            LoadingComplete = 98
                ,
            SystemOffLine = 99
            ,
            Permissive =77
        }
        public struct _CardReaderBuffer
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
            public string BayName;
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
            public bool TimeOut;

            public int RET_BATCH_STATUS;
            public string RET_LOADED_MASS;
            public string RET_FLOWRATE;
            public string RET_UNIT;
            public int RET_LOAD_STATUS;
            public int RET_STEP;
            public double RET_LOAD_HEADER;
            public double RET_LOAD_LINE;
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
            public int RET_ACTIVE;

            //---for LLTLB
            public string ComportNo;
            public string ComportNo1;

            public int ComportID;
            public int ComportID1;

            public int ControlComport;
            public int ControlComport1;
            
            public _BayLocation BayLocation;
            public _LoadingInfo LoadingInfo_A;     //LEFT
            public _LoadingInfo LoadingInfo_B;    //RIGHT
            public _LoadingMode LoadingMode;
            public int MaxLoad;
            public int PageNum;
            public _LoadingDetail[] LoadingDetail;
            public string PermissiveMsg;
            public bool Permissive;
            public bool PermissiveOld;
            public _CRStepProcess CRStepProcess;
            public bool OldPermissive;
            public bool NewCancelLoad;
            public bool OldCancelLoad;
            public bool MeterAlarm;
            public string AlarmMsg;
            public string KeyPress;
            public string BlockFormat;
            //----------
        }
        #endregion

        public Comport[] CRComport;
        SerialPort[] crSerialPort;

        public _CardReaderBuffer CRNewValue;
        _CardReaderBuffer CROldValue;
        DateTime datetimeResponse;
        bool chkResponse;
        int countResponse;
        int processId = 0;

        int PositionSTX, PositionETX;
        MercuryCardReader.MercuryProtocol mercuryProtocol;
        frmMain fMain;
        Comport crPort;
        //cPorts mPort;

        #region construct and deconstruct
        private bool IsDisposed = false;
        public void Dispose()
        {
            thrRun = false;
            thrShutdown = true;
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
                    thrRun = false;
                    DisplayToCardReader(99);
                    thrLock = null;
                    thrShutdown = true;
                    CRNewValue.RET_STEP = 0;

                    if ((thrMain != null) && (thrMain.ThreadState == System.Threading.ThreadState.Running))
                        thrMain.Abort();

                    mercuryProtocol = null;
                    crPort.CloseComPort();
                    crPort.Dispose();
                }
                //Clean up unmanaged resources
                thrRun = false;
            }
            IsDisposed = true;
        }

        public CardReaderProcess(frmMain f)
        {
            fMain = f;
            mercuryProtocol = new MercuryCardReader.MercuryProtocol();
            CRNewValue.LoadingDetail = new _LoadingDetail[20];
            CRNewValue.MaxLoad = 15;
        }
        
        public CardReaderProcess(frmMain f,int pCRAddress, string pCRId,string pCRName)
        {
            fMain = f;
            CRNewValue.Name = pCRName;
            CRNewValue.ID = pCRId;
            CRNewValue.Address = pCRAddress;
            mercuryProtocol = new  MercuryCardReader.MercuryProtocol();
            CRNewValue.LoadingDetail = new _LoadingDetail[20];
            CRNewValue.MaxLoad = 15;
        }
        ~CardReaderProcess()
        {
            thrShutdown = true;
            //mPort = null;
            mercuryProtocol = null;
        }
        #endregion

        #region Class Events
        //public delegate void ATGEventsHaneler(object pSender, string pEventMsg);
        //public event ATGEventsHaneler OnATGEvents;
        string logFileName;

        void RaiseEvents(string pSender, string pMsg)
        {
            string vMsg = DateTime.Now + ">[" + pSender + "]" + pMsg;
            logFileName = CRNewValue.Name;
            try
            {
                //fMain.AddListBox = "" + logFileName + ">" + vMsg;
                //fMain.LogFile.WriteLog(logFileName, vMsg);
                fMain.DisplayMessage(logFileName, vMsg);
            }
            catch (Exception exp)
            { }
        }

        void RaiseEvents(string pMsg)
        {
            logFileName = CRNewValue.Name;
            try
            {
                string vMsg = CRNewValue.Name + ">" + DateTime.Now + "> " + pMsg;
                //fMain.AddListBox = vMsg;
                //fMain.LogFile.WriteLog(logFileName, vMsg);
                fMain.AddListBox = "" + logFileName + ">" + vMsg;
                fMain.LogFile.WriteLog(logFileName, vMsg);
            }
            catch (Exception exp)
            { }
        }
        #endregion

        #region Thread
        bool thrConnect;
        bool thrShutdown;
        bool thrRunning;
        bool thrRun;
        private object thrLock = new object();
        public Thread thrMain;

        public void StartThread()
        {
            thrRun = true;
            thrRunning = false;

            //if ((mThread != null) && (mThread.ThreadState != ThreadState.Aborted))
            //    mThread.Abort();

            thrMain = null;
            Thread.Sleep(1000);
            try
            {
                datetimeResponse = DateTime.Now;
                CRNewValue.TimeSend = DateTime.Now;
                CROldValue.TimeSend = DateTime.Now;
                if (thrRunning)
                {
                    return;
                }
                thrMain = new Thread(this.RunProcess);
                thrRunning = true;
                thrMain.Name = CRNewValue.Name;
                thrMain.Start();
            }
            catch (Exception exp)
            {
                fMain.LogFile.WriteErrLog(exp.Message);
                thrRunning = false;
            }

        }

        public void StartThread(ref Comport p)
        {
            thrRun = true;

            //crPort = p;
            Thread.Sleep(1000);
            try
            {
                datetimeResponse = DateTime.Now;
                if (thrRunning)
                {
                    return;
                }
                thrMain = new Thread(this.RunProcess);
                thrRunning = true;
                thrMain.Name = CRNewValue.Name;
                thrMain.Start();
            }
            catch (Exception exp)
            {
                fMain.LogFile.WriteErrLog(exp.Message);
                thrRunning = false;
            }

        }

        private void RunProcess()
        {
            try
            {
                while (thrRun)
                {
                    if (thrShutdown)
                        return;
                    if (CRNewValue.Connect)
                    {
                        try
                        {
                            SendToCardReader(mercuryProtocol.MakeCursorInvisible());
                            SendToCardReader(mercuryProtocol.SetKeyToNum());
                            SendToCardReader(mercuryProtocol.DeleteAllStoreMessage());
                            SendToCardReader(mercuryProtocol.SendNextQueueBlock(), false);
                            SendToCardReader(mercuryProtocol.SendNextQueueBlock(), false);
                            SendToCardReader(mercuryProtocol.SendNextQueueBlock(), false);
                            SendToCardReader(mercuryProtocol.SendNextQueueBlock(), false);
                            SendToCardReader(mercuryProtocol.ClearDisplay());
                            SendToCardReader(mercuryProtocol.MakeCursorInvisible());
                            SendToCardReader(mercuryProtocol.SetStandardCharacterSize());
                            SendToCardReader(mercuryProtocol.SetFontEnglish());
                            CRBayLoading_LLTLB();
                        }
                        catch (Exception exp)
                        { }
                        //M_CRBAY_CHECK_TOPUP();
                            
                        //if (mCR_NewValue.ModeStatus == 0)
                        //{
                        //    CRBayLoading();
                        //}
                        //else
                        //{
                        //    CRBayLoading_Topup();
                        //}
                    }
                    else
                    {
                        try
                        {
                            if (crPort.IsOpen())
                            //if(true)
                            {
                                string s = "";
                                SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
                                //Thread.Sleep(300);
                                //ReadFromCardReader();
                                ChangeProcess();
                            }
                            else
                            {
                                ChangeProcess();
                            }
                        }
                        catch (Exception exp)
                        { }
                    }
                    Thread.Sleep(3000);
                }
                //ClosePort();
                thrRunning = false;
                //break;
            }
            finally
            { 
                //this.Dispose(); 
                thrShutdown = true;
                thrRun = false;
            }
        }
        #endregion

        #region Database
        
        private void P_UPDATE_CARDREADER_CONNECT()
        {
            string strSQL = "begin tas.P_UPDATE_CARDREADER_CONNECT(" +
                            CRNewValue.ID + "," + Convert.ToInt16(CRNewValue.Connect) + ",'" + Environment.MachineName + "'" +
                            ");end;";
            fMain.OraDb.ExecuteSQL(strSQL);

            RaiseEvents("Card reader connect=" + CRNewValue.Connect.ToString());
        }
        
        #region "SAKCTAS"
        private bool M_CRBAY_CHECK_STEP()
        {
            bool bCheck=false;
            string strSQL = "begin load.M_CRBAY_CHECK_STEP(" +
                            + Convert.ToUInt64(CRNewValue.CardCode) + "," + CRNewValue.BayNo + "," + CRNewValue.ID +
                            ",:RET_STEP,:RET_LOAD_HEADER_NO,:RET_LOAD_STATUS,:RET_COMPARTMENT_LIST,:RET_TOT_COMPARTMENT" +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG,:RET_TU_ID,:RET_METER_NO,:RET_SALE_PRODUCT_NAME" +
                            ",:RET_SEAL_USE,:RET_SEAL_NUMBER,:RET_IS_BLENDING" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(14);

            OraParam.AddOracleParameter(0, "RET_STEP", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_LOAD_HEADER_NO", Oracle.ManagedDataAccess.Client.OracleDbType.Int32);
            OraParam.AddOracleParameter(2, "RET_LOAD_STATUS", Oracle.ManagedDataAccess.Client.OracleDbType.Int16);
            OraParam.AddOracleParameter(3, "RET_COMPARTMENT_LIST", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2,512);
            OraParam.AddOracleParameter(4, "RET_TOT_COMPARTMENT", Oracle.ManagedDataAccess.Client.OracleDbType.Int16);
            OraParam.AddOracleParameter(5, "RET_CHECK", Oracle.ManagedDataAccess.Client.OracleDbType.Int16);
            OraParam.AddOracleParameter(6, "RET_MSG", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2, 512);
            OraParam.AddOracleParameter(7, "RET_CR_MSG", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2, 512);
            OraParam.AddOracleParameter(8, "RET_TU_ID", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2, 512);
            OraParam.AddOracleParameter(9, "RET_METER_NO", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(10, "RET_SALE_PRODUCT_NAME", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(11, "RET_SEAL_USE", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(12, "RET_SEAL_NUMBER", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(13, "RET_IS_BLENDING", Oracle.ManagedDataAccess.Client.OracleDbType.Int16);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_STEP", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_STEP = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_STEP = 0;

                OraParam.GetOracleParameterValue("RET_LOAD_HEADER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_HEADER = Convert.ToInt32(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_HEADER = 0;

                OraParam.GetOracleParameterValue("RET_LOAD_STATUS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_STATUS = Convert.ToInt32(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_HEADER = 0;

                OraParam.GetOracleParameterValue("RET_COMPARTMENT_LIST", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_COMPARTMENT_LIST = p.Value.ToString();
                else
                    CRNewValue.RET_COMPARTMENT_LIST = "???";

                OraParam.GetOracleParameterValue("RET_TOT_COMPARTMENT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_TOT_COMPARTMENT = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_TOT_COMPARTMENT = 0;

                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = -1;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "???";

                OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CR_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_CR_MSG = "";

                OraParam.GetOracleParameterValue("RET_TU_ID", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_TU_ID = p.Value.ToString();
                else
                    CRNewValue.RET_TU_ID = "";

                OraParam.GetOracleParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_METER_NO = p.Value.ToString();
                else
                    CRNewValue.RET_METER_NO = "???";

                OraParam.GetOracleParameterValue("RET_SALE_PRODUCT_NAME", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_SALE_PRODUCT_NAME = p.Value.ToString();
                else
                    CRNewValue.RET_SALE_PRODUCT_NAME = "???";

                OraParam.GetOracleParameterValue("RET_SEAL_USE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_SEAL_USE = p.Value.ToString();
                else
                    CRNewValue.RET_SEAL_USE = "???";

                OraParam.GetOracleParameterValue("RET_SEAL_NUMBER", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_SEAL_NUMBER = p.Value.ToString();
                else
                    CRNewValue.RET_SEAL_NUMBER = "???";

                OraParam.GetOracleParameterValue("RET_IS_BLENDING", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_IS_BLENDING = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_IS_BLENDING = 0;
                
                if (CRNewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                RaiseEvents(CRNewValue.RET_MSG);
                if(CRNewValue.RET_CR_MSG !="")
                    RaiseEvents(CRNewValue.RET_CR_MSG);
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
                            +Convert.ToUInt64(CRNewValue.CardCode) + "," + CRNewValue.BayNo + ",'" + CRNewValue.RET_METER_NO+"',"+CRNewValue.RET_TOPUP_NO+
                            ",:RET_STEP,:RET_LOAD_HEADER_NO,:RET_COMPARTMENT_NO" +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(6);

            OraParam.AddOracleParameter(0, "RET_STEP", Oracle.ManagedDataAccess.Client.OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_LOAD_HEADER_NO", Oracle.ManagedDataAccess.Client.OracleDbType.Int32);
            OraParam.AddOracleParameter(2, "RET_COMPARTMENT_NO", Oracle.ManagedDataAccess.Client.OracleDbType.Int16);
            OraParam.AddOracleParameter(3, "RET_CHECK", Oracle.ManagedDataAccess.Client.OracleDbType.Int16);
            OraParam.AddOracleParameter(4, "RET_MSG", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2, 512);
            OraParam.AddOracleParameter(5, "RET_CR_MSG", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_STEP", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_STEP = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_STEP = 0;

                OraParam.GetOracleParameterValue("RET_LOAD_HEADER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_HEADER = Convert.ToInt32(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_HEADER = 0;

                OraParam.GetOracleParameterValue("RET_COMPARTMENT_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.CompartmentNo = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.CompartmentNo = 0;


                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = -1;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CR_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_CR_MSG = "";

                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CR_MSG != "")
                    RaiseEvents(CRNewValue.RET_CR_MSG);
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
                            Convert.ToInt64(CRNewValue.CardCode) + "," + CRNewValue.ID + ",0,0," + CRNewValue.BayNo + ",' ',' '" +
                            ",:RET_LOAD_TYPE,:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(4);

            OraParam.AddOracleParameter(0, "RET_LOAD_TYPE", OracleDbType.Int32);
            OraParam.AddOracleParameter(1, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(2,"RET_MSG",OracleDbType.Varchar2,512);
            OraParam.AddOracleParameter(3, "RET_CR_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {

                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;
                //if (CR.RET_CHECK != -1)
                //{
                    OraParam.GetOracleParameterValue("RET_LOAD_TYPE", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        CRNewValue.RET_LOAD_TYPE = Convert.ToInt16(p.Value.ToString());
                    else
                        CRNewValue.RET_LOAD_TYPE = 0;

                    OraParam.GetOracleParameterValue("RET_MSG", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        CRNewValue.RET_MSG = p.Value.ToString();
                    else
                        CRNewValue.RET_MSG = "";

                    OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        CRNewValue.RET_CR_MSG = p.Value.ToString();
                    else
                        CRNewValue.RET_CR_MSG = "";

                    RaiseEvents(CRNewValue.RET_MSG);
                    if(CRNewValue.RET_CR_MSG!="")
                        RaiseEvents(CRNewValue.RET_CR_MSG);
               // }
                if (CRNewValue.RET_CHECK == 0)
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
                            Convert.ToInt64(CRNewValue.CardCode) + "," + CRNewValue.ID + ",0,0," + CRNewValue.BayNo + ",' ',' '" +
                            ",:RET_LOAD_TYPE,:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(4);

            OraParam.AddOracleParameter(0, "RET_LOAD_TYPE", OracleDbType.Int32);
            OraParam.AddOracleParameter(1, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(2, "RET_MSG", OracleDbType.Varchar2, 512);
            OraParam.AddOracleParameter(3, "RET_CR_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {

                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;
                //if (CR.RET_CHECK != -1)
                //{
                OraParam.GetOracleParameterValue("RET_LOAD_TYPE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_TYPE = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_TYPE = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CR_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_CR_MSG = "";

                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CR_MSG != "")
                    RaiseEvents(CRNewValue.RET_CR_MSG);
                // }
                if (CRNewValue.RET_CHECK == 0)
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
                            CRNewValue.BayNo + ",' ',' '," + CRNewValue.RET_TOPUP_NO +
                             ",:RET_CHECK" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(1);

            OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int32);
      
            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                if (CRNewValue.RET_CHECK == 0)
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
                            CRNewValue.BayNo + "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.CompartmentNo +
                            ",:RET_BATCH_NO,:RET_LOAD_COUNT,:RET_RECIPES_NO,:RET_PRESET" +
                            ",:RET_DESITY30C,:RET_VCF30" +
                            ",:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2,:RET_METER_NO,:RET_SALE_PRODUCT_NAME" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(12);

            OraParam.AddOracleParameter(0, "RET_BATCH_NO", OracleDbType.Int32);
            OraParam.AddOracleParameter(1, "RET_LOAD_COUNT", OracleDbType.Int16);
            OraParam.AddOracleParameter(2, "RET_RECIPES_NO", OracleDbType.Int16);
            OraParam.AddOracleParameter(3, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddOracleParameter(4, "RET_DENSITY30C", OracleDbType.Varchar2,64);
            OraParam.AddOracleParameter(5, "RET_VCF30", OracleDbType.Single);
            OraParam.AddOracleParameter(6, "RET_CHECK", OracleDbType.Varchar2,64);
            OraParam.AddOracleParameter(7, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(8, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(9, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(10, "RET_METER_NO", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(11, "RET_SALE_PRODUCT_NAME", OracleDbType.Varchar2, 128);


            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_BATCH_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_LINE = Convert.ToInt32(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_LINE = 0;

                OraParam.GetOracleParameterValue("RET_LOAD_COUNT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_COUNT = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_COUNT = 0;

                OraParam.GetOracleParameterValue("RET_RECIPES_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_RECIPES_NO = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_RECIPES_NO = 0;

                OraParam.GetOracleParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_PRESET = 0;

                OraParam.GetOracleParameterValue("RET_DENSITY30C", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_DENSITY30C = Convert.ToDouble(p.Value.ToString());
                else
                    CRNewValue.RET_DENSITY30C = 0;

                OraParam.GetOracleParameterValue("RET_VCF30", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_VCF30 = Convert.ToDouble(p.Value.ToString());
                else
                    CRNewValue.RET_VCF30 = 0;

                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH1 = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH2 = "";

                OraParam.GetOracleParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_METER_NO = p.Value.ToString();
                else
                    CRNewValue.RET_METER_NO = "";

                OraParam.GetOracleParameterValue("RET_SALE_PRODUCT_NAME", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_SALE_PRODUCT_NAME = p.Value.ToString();
                else
                    CRNewValue.RET_SALE_PRODUCT_NAME = "";

                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CHECK == -1)
                {
                    RaiseEvents(CRNewValue.RET_MSG_BATCH1 + " " + CRNewValue.RET_MSG_BATCH2);
                }
                if (CRNewValue.RET_CHECK == 0)
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
                            CRNewValue.BayNo + "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.CompartmentNo +
                            ",:RET_BATCH_NO,:RET_LOAD_COUNT,:RET_RECIPES_NO,:RET_PRESET" +
                            ",:RET_DESITY30C,:RET_VCF30" +
                            ",:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2,:RET_METER_NO,:RET_SALE_PRODUCT_NAME" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(12);

            OraParam.AddOracleParameter(0, "RET_BATCH_NO", OracleDbType.Int32);
            OraParam.AddOracleParameter(1, "RET_LOAD_COUNT", OracleDbType.Int16);
            OraParam.AddOracleParameter(2, "RET_RECIPES_NO", OracleDbType.Int16);
            OraParam.AddOracleParameter(3, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddOracleParameter(4, "RET_DENSITY30C", OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(5, "RET_VCF30", OracleDbType.Single);
            OraParam.AddOracleParameter(6, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(7, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(8, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(9, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(10, "RET_METER_NO", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(11, "RET_SALE_PRODUCT_NAME", OracleDbType.Varchar2, 128);


            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_BATCH_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_LINE = Convert.ToInt32(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_LINE = 0;

                OraParam.GetOracleParameterValue("RET_LOAD_COUNT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_COUNT = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_COUNT = 0;

                OraParam.GetOracleParameterValue("RET_RECIPES_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_RECIPES_NO = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_RECIPES_NO = 0;

                OraParam.GetOracleParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_PRESET = 0;

                OraParam.GetOracleParameterValue("RET_DENSITY30C", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_DENSITY30C = Convert.ToDouble(p.Value.ToString());
                else
                    CRNewValue.RET_DENSITY30C = 0;

                OraParam.GetOracleParameterValue("RET_VCF30", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_VCF30 = Convert.ToDouble(p.Value.ToString());
                else
                    CRNewValue.RET_VCF30 = 0;

                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH1 = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH2 = "";

                OraParam.GetOracleParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_METER_NO = p.Value.ToString();
                else
                    CRNewValue.RET_METER_NO = "";

                OraParam.GetOracleParameterValue("RET_SALE_PRODUCT_NAME", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_SALE_PRODUCT_NAME = p.Value.ToString();
                else
                    CRNewValue.RET_SALE_PRODUCT_NAME = "";

                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CHECK == -1)
                {
                    RaiseEvents(CRNewValue.RET_MSG_BATCH1 + " " + CRNewValue.RET_MSG_BATCH2);
                }
                if (CRNewValue.RET_CHECK == 0)
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
                            CRNewValue.BayNo + ",'" + CRNewValue.RET_METER_NO + "'," + CRNewValue.CompartmentNo +
                            "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.RET_LOAD_LINE + "," + CRNewValue.RET_LOAD_COUNT +
                            ",:RET_CHECK,:RET_MSG" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                if (CRNewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                RaiseEvents(CRNewValue.RET_MSG);
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
                            CRNewValue.BayNo + ",'" + CRNewValue.RET_METER_NO + "'," + CRNewValue.CompartmentNo +
                            "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.RET_TOPUP_NO + "," + CRNewValue.RET_LOAD_COUNT +
                            ",:RET_CHECK,:RET_MSG" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                if (CRNewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                RaiseEvents(CRNewValue.RET_MSG);
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
                            CRNewValue.BayNo + ",'" + CRNewValue.RET_METER_NO + "'," + CRNewValue.CompartmentNo +
                            "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.RET_LOAD_LINE + "," + CRNewValue.RET_LOAD_COUNT +
                            ",:RET_CHECK,:RET_MSG" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = -1;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                if (CRNewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                //RaiseEvents(CR_NewValue.RET_MSG);
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
                            "'" + CRNewValue.RET_METER_NO + "'" +
                            ",:RET_COMP_NO,:RET_BAY_NO,:RET_LOAD_HEADER_NO,:RET_LOAD_LINE_NO,:RET_LOAD_COUNT,:RET_START_TOT" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(6);

            OraParam.AddOracleParameter(0, "RET_COMP_NO", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_BAY_NO", OracleDbType.Int16);
            OraParam.AddOracleParameter(2, "RET_LOAD_HEADER_NO", OracleDbType.Double);
            OraParam.AddOracleParameter(3, "RET_LOAD_LINE_NO", OracleDbType.Double);
            OraParam.AddOracleParameter(4, "RET_LOAD_COUNT", OracleDbType.Int16);
            OraParam.AddOracleParameter(5, "RET_START_TOT", OracleDbType.Varchar2,64);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_LOAD_LINE_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_LINE = Convert.ToDouble(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_LINE = 0;

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
                            CRNewValue.BayNo + ",'" + CRNewValue.RET_METER_NO + "'," + CRNewValue.CompartmentNo +
                            "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.RET_LOAD_LINE + "," + CRNewValue.RET_LOAD_COUNT +
                            ",:RET_CHeCK,:RET_MSG" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = -1;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                if (CRNewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                RaiseEvents(CRNewValue.RET_MSG);
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
                            CRNewValue.BayNo + ",'" + CRNewValue.RET_METER_NO + "'," + CRNewValue.CompartmentNo +
                            "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.RET_TOPUP_NO + "," + CRNewValue.RET_LOAD_COUNT +
                            ",:RET_CHeCK,:RET_MSG" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = -1;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                if (CRNewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                RaiseEvents(CRNewValue.RET_MSG);
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
                            CRNewValue.BayNo + ",'" + CRNewValue.RET_METER_NO + "'," + CRNewValue.CompartmentNo +
                            "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.RET_LOAD_LINE + "," + CRNewValue.RET_LOAD_COUNT +
                            ",:RET_CHECK,:RET_MSG" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = -1;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                if (CRNewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;

                //RaiseEvents(CR_NewValue.RET_MSG);
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
                            "'" + CRNewValue.RET_METER_NO + "'," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.RET_LOAD_LINE +
                            "," + CRNewValue.CompartmentNo +
                            ",:RET_BATCH_STATUS,:RET_LOADED_MASS,:RET_FLOWRATE" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);

            OraParam.AddOracleParameter(0, "RET_BATCH_STATUS", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_LOADED_MASS", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(2, "RET_FLOWRATE", OracleDbType.Varchar2, 128);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_BATCH_STATUS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_BATCH_STATUS = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_BATCH_STATUS = 0;

                OraParam.GetOracleParameterValue("RET_LOADED_MASS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOADED_MASS = p.Value.ToString();
                else
                    CRNewValue.RET_LOADED_MASS = "0";

                OraParam.GetOracleParameterValue("RET_FLOWRATE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_FLOWRATE = p.Value.ToString();
                else
                    CRNewValue.RET_FLOWRATE = "0";

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
                            "'" + CRNewValue.RET_METER_NO + "'," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.RET_TOPUP_NO +
                            "," + CRNewValue.CompartmentNo +
                            ",:RET_BATCH_STATUS,:RET_LOADED_MASS,:RET_FLOWRATE" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);

            OraParam.AddOracleParameter(0, "RET_BATCH_STATUS", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_LOADED_MASS", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(2, "RET_FLOWRATE", OracleDbType.Varchar2, 128);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_BATCH_STATUS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_BATCH_STATUS = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_BATCH_STATUS = 0;
                OraParam.GetOracleParameterValue("RET_LOADED_MASS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOADED_MASS = p.Value.ToString();
                else
                    CRNewValue.RET_LOADED_MASS = "0";

                OraParam.GetOracleParameterValue("RET_FLOWRATE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_FLOWRATE = p.Value.ToString();
                else
                    CRNewValue.RET_FLOWRATE = "0";

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
                            CRNewValue.BayNo +
                            ",:RET_CARD_CODE,:RET_LOAD_HEADER,:RET_LOAD_LINE" +
                            ",:RET_COMPARTMENT_NO,:RET_COMPARTMENT_LIST,:RET_TOT_COMPARTMENT,:RET_TU_ID,:RET_METER_NO" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(8);

            OraParam.AddOracleParameter(0, "RET_CARD_CODE", OracleDbType.Varchar2,128);
            OraParam.AddOracleParameter(1, "RET_LOAD_HEADER", OracleDbType.Int32);
            OraParam.AddOracleParameter(2, "RET_LOAD_LINE", OracleDbType.Int32);
            OraParam.AddOracleParameter(3, "RET_COMPARTMENT_NO", OracleDbType.Int16);
            OraParam.AddOracleParameter(4, "RET_COMPARTMENT_LIST", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(5, "RET_TOT_COMPARTMENT", OracleDbType.Int16);
            OraParam.AddOracleParameter(6, "RET_TU_ID", OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(7, "RET_METER_NO", OracleDbType.Varchar2, 64);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CARD_CODE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.CardCode = p.Value.ToString();
                else
                    CRNewValue.CardCode = "0";

                OraParam.GetOracleParameterValue("RET_LOAD_HEADER", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_HEADER =Convert.ToDouble(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_HEADER = 0;

                OraParam.GetOracleParameterValue("RET_LOAD_LINE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_LINE = Convert.ToDouble(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_LINE = 0;

                OraParam.GetOracleParameterValue("RET_COMPARTMENT_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.CompartmentNo = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.CompartmentNo = 0;


                OraParam.GetOracleParameterValue("RET_COMPARTMENT_LIST", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_COMPARTMENT_LIST = p.Value.ToString();
                else
                    CRNewValue.RET_COMPARTMENT_LIST = "0";

                OraParam.GetOracleParameterValue("RET_TOT_COMPARTMENT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_TOT_COMPARTMENT = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_TOT_COMPARTMENT = 0;

                OraParam.GetOracleParameterValue("RET_TU_ID", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_TU_ID = p.Value.ToString();
                else
                    CRNewValue.RET_TU_ID = "";

                OraParam.GetOracleParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_METER_NO = p.Value.ToString();
                else
                    CRNewValue.RET_METER_NO = "";

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
                            CRNewValue.BayNo +
                            ",:RET_CARD_CODE,:RET_LOAD_HEADER,:RET_TOPUP_NO" +
                            ",:RET_COMPARTMENT_NO,:RET_COMPARTMENT_LIST,:RET_TOT_COMPARTMENT,:RET_TU_ID,:RET_METER_NO" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(8);

            OraParam.AddOracleParameter(0, "RET_CARD_CODE", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(1, "RET_LOAD_HEADER", OracleDbType.Int32);
            OraParam.AddOracleParameter(2, "RET_TOPUP_NO", OracleDbType.Int32);
            OraParam.AddOracleParameter(3, "RET_COMPARTMENT_NO", OracleDbType.Int16);
            OraParam.AddOracleParameter(4, "RET_COMPARTMENT_LIST", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(5, "RET_TOT_COMPARTMENT", OracleDbType.Int16);
            OraParam.AddOracleParameter(6, "RET_TU_ID", OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(7, "RET_METER_NO", OracleDbType.Varchar2, 64);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CARD_CODE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.CardCode = p.Value.ToString();
                else
                    CRNewValue.CardCode = "0";

                OraParam.GetOracleParameterValue("RET_LOAD_HEADER", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_HEADER = Convert.ToDouble(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_HEADER = 0;

                OraParam.GetOracleParameterValue("RET_TOPUP_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_TOPUP_NO = Convert.ToInt32(p.Value.ToString());
                else
                    CRNewValue.RET_TOPUP_NO = 0;

                OraParam.GetOracleParameterValue("RET_COMPARTMENT_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.CompartmentNo = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.CompartmentNo = 0;


                OraParam.GetOracleParameterValue("RET_COMPARTMENT_LIST", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_COMPARTMENT_LIST = p.Value.ToString();
                else
                    CRNewValue.RET_COMPARTMENT_LIST = "0";

                OraParam.GetOracleParameterValue("RET_TOT_COMPARTMENT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_TOT_COMPARTMENT = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_TOT_COMPARTMENT = 0;

                OraParam.GetOracleParameterValue("RET_TU_ID", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_TU_ID = p.Value.ToString();
                else
                    CRNewValue.RET_TU_ID = "";

                OraParam.GetOracleParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_METER_NO = p.Value.ToString();
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
                            CRNewValue.BayNo +
                            ",:RET_METER_NO,:RET_METER_NAME,:RET_IS_TOPUP" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);

            OraParam.AddOracleParameter(0, "RET_METER_NO", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(1, "RET_METER_NAME", OracleDbType.Varchar2,128);
            OraParam.AddOracleParameter(2, "RET_IS_TOPUP", OracleDbType.Int32);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_IS_TOPUP", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                {
                    is_topup = p.Value.ToString();
                    CRNewValue.ModeStatus = Convert.ToInt32(is_topup);
                }
                OraParam.GetOracleParameterValue("RET_METER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_METER_NO = p.Value.ToString();

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
                            CROldValue.CardCode + "," + CRNewValue.BayNo + "," + "'" + CRNewValue.RET_METER_NO +"'"+","+ CRNewValue.ID +
                            ",:RET_BATCH_STATUS,:RET_TOPUP_NO,:RET_LOAD_HEADER_NO,:RET_COMPARTMENT_NO" +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(7);

            OraParam.AddOracleParameter(0, "RET_BATCH_STATUS", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_TOPUP_NO", OracleDbType.Int32);
            OraParam.AddOracleParameter(2, "RET_LOAD_HEADER_NO", OracleDbType.Int32);
            OraParam.AddOracleParameter(3, "RET_COMPARTMENT_NO", OracleDbType.Int32);
            OraParam.AddOracleParameter(4, "RET_CHECK", OracleDbType.Int32);
            OraParam.AddOracleParameter(5, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(6, "RET_CR_MSG", OracleDbType.Varchar2, 128);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_BATCH_STATUS", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_BATCH_STATUS = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_BATCH_STATUS = 0;
                OraParam.GetOracleParameterValue("RET_TOPUP_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_TOPUP_NO = Convert.ToInt32(p.Value.ToString());
                else
                    CRNewValue.RET_TOPUP_NO = 0;
                OraParam.GetOracleParameterValue("RET_LOAD_HEADER_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_HEADER = Convert.ToInt32(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_HEADER = 0;
                OraParam.GetOracleParameterValue("RET_COMPARTMENT_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.CompartmentNo = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.CompartmentNo = 0;
                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt32(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;
                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";
                OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CR_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_CR_MSG = "";
                
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
                            CRNewValue.BayNo + "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.RET_TOPUP_NO + "," + CRNewValue.CompartmentNo + "," +"'"+ CRNewValue.RET_METER_NO+"'"+
                            ",:RET_BATCH_NO" +
                            ",:RET_LOAD_COUNT,:RET_RECIPES_NO,:RET_PRESET,:RET_DESITY30C" +
                            ",:RET_VCF30C,:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2,:RET_SALE_PRODUCT_NAME" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(11);

            OraParam.AddOracleParameter(0, "RET_BATCH_NO", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_LOAD_COUNT", OracleDbType.Int16);
            OraParam.AddOracleParameter(2, "RET_RECIPES_NO", OracleDbType.Int16);
            OraParam.AddOracleParameter(3, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddOracleParameter(4, "RET_DESITY30C", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(5, "RET_VCF30C", OracleDbType.Int16);
            OraParam.AddOracleParameter(6, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(7, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(8, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(9, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(10, "RET_SALE_PRODUCT_NAME", OracleDbType.Varchar2, 128);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_BATCH_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_BATCH_NO = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_BATCH_NO = 0;

                OraParam.GetOracleParameterValue("RET_LOAD_COUNT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_COUNT = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_COUNT = 0;

                OraParam.GetOracleParameterValue("RET_RECIPES_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_RECIPES_NO = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_RECIPES_NO = 0;

                OraParam.GetOracleParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_PRESET = 0;

                OraParam.GetOracleParameterValue("RET_DESITY30C", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_DENSITY30C = Convert.ToDouble(p.Value.ToString());
                else
                    CRNewValue.RET_DENSITY30C = 0;
                OraParam.GetOracleParameterValue("RET_VCF30C", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_VCF30C = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_VCF30C = 0;

                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH1 = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH2 = "";
                OraParam.GetOracleParameterValue("RET_SALE_PRODUCT_NAME", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_SALE_PRODUCT_NAME = p.Value.ToString();
                else
                    CRNewValue.RET_SALE_PRODUCT_NAME = "";

                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CHECK == -1)
                {
                    RaiseEvents(CRNewValue.RET_MSG_BATCH1 + " " + CRNewValue.RET_MSG_BATCH2);
                }
                if (CRNewValue.RET_CHECK == 0)
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
                            "'" + CRNewValue.RET_METER_NO + "'" +
                            ",:RET_CHECK,:RET_MSG" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(2);

            OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 128);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = -1;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                if (CRNewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }
            else
                CRNewValue.RET_CHECK = -1;

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;

            return bCheck;
        }

        private bool M_CRBAY_GET_PRESET()
        {
            bool bCheck = false;

            string strSQL = "begin load.M_CRBAY_GET_PRESET(" +
                            CRNewValue.BayNo + "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.CompartmentNo +
                            ",:RET_PRESET" +
                            ",:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(5);

            OraParam.AddOracleParameter(0, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(2, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(3, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(4, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_PRESET = 0;

                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH1 = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH2 = "";

                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CHECK == -1)
                {
                    RaiseEvents(CRNewValue.RET_MSG_BATCH1 + " " + CRNewValue.RET_MSG_BATCH2);
                }
                if (CRNewValue.RET_CHECK == 0)
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
                            CRNewValue.BayNo + "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.CompartmentNo +
                            ",:RET_PRESET" +
                            ",:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(5);

            OraParam.AddOracleParameter(0, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddOracleParameter(1, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(2, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(3, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(4, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_PRESET = 0;

                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH1 = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH2 = "";

                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CHECK == -1)
                {
                    RaiseEvents(CRNewValue.RET_MSG_BATCH1 + " " + CRNewValue.RET_MSG_BATCH2);
                }
                if (CRNewValue.RET_CHECK == 0)
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
                            CRNewValue.BayNo + ",'" + CRNewValue.RET_METER_NO + "'," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.CompartmentNo +
                            ",:RET_BATCH_NO,:RET_LOAD_COUNT,:RET_RECIPES_NO,:RET_PRESET" +
                            ",:RET_WRITE_DESITY30C,:RET_DESITY30C,:RET_WRITE_VCF30,:RET_VCF30" +
                            ",:RET_CHECK,:RET_MSG,:RET_MSG_BATCH1,:RET_MSG_BATCH2" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(12);

            OraParam.AddOracleParameter(0, "RET_BATCH_NO", OracleDbType.Int32);
            OraParam.AddOracleParameter(1, "RET_LOAD_COUNT", OracleDbType.Int16);
            OraParam.AddOracleParameter(2, "RET_RECIPES_NO", OracleDbType.Int16);
            OraParam.AddOracleParameter(3, "RET_PRESET", OracleDbType.Int16);
            OraParam.AddOracleParameter(4, "RET_WRITE_DENSITY30C", OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(5, "RET_DENSITY30C", OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(6, "RET_WRITE_VCF30", OracleDbType.Single);
            OraParam.AddOracleParameter(7, "RET_VCF30", OracleDbType.Single);
            OraParam.AddOracleParameter(8, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(9, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(10, "RET_MSG_BATCH1", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(11, "RET_MSG_BATCH2", OracleDbType.Varchar2, 128);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_BATCH_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_LINE = Convert.ToInt32(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_LINE = 0;

                OraParam.GetOracleParameterValue("RET_LOAD_COUNT", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_COUNT = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_COUNT = 0;

                OraParam.GetOracleParameterValue("RET_RECIPES_NO", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_RECIPES_NO = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_RECIPES_NO = 0;

                OraParam.GetOracleParameterValue("RET_PRESET", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_PRESET = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_PRESET = 0;

                OraParam.GetOracleParameterValue("RET_DENSITY30C", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_DENSITY30C = Convert.ToDouble(p.Value.ToString());
                else
                    CRNewValue.RET_DENSITY30C = 0;

                OraParam.GetOracleParameterValue("RET_VCF30", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_VCF30 = Convert.ToDouble(p.Value.ToString());
                else
                    CRNewValue.RET_VCF30 = 0;

                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH1", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH1 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH1 = "";

                OraParam.GetOracleParameterValue("RET_MSG_BATCH2", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG_BATCH2 = p.Value.ToString();
                else
                    CRNewValue.RET_MSG_BATCH2 = "";


                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CHECK == -1)
                {
                    RaiseEvents(CRNewValue.RET_MSG_BATCH1 + " " + CRNewValue.RET_MSG_BATCH2);
                }
                if (CRNewValue.RET_CHECK == 0)
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
                            Convert.ToInt64(CRNewValue.CardCode) + "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.ID +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);

            OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(2, "RET_CR_MSG", OracleDbType.Varchar2, 128);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CR_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_CR_MSG = "";


                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CHECK == -1)
                {
                    RaiseEvents(CRNewValue.RET_CR_MSG);
                }
                if (CRNewValue.RET_CHECK == 0)
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
                            Convert.ToInt64(CRNewValue.CardCode) + "," + CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.ID +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);

            OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Varchar2, 64);
            OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 128);
            OraParam.AddOracleParameter(2, "RET_CR_MSG", OracleDbType.Varchar2, 128);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CR_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_CR_MSG = "";


                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CHECK == -1)
                {
                    RaiseEvents(CRNewValue.RET_CR_MSG);
                }
                if (CRNewValue.RET_CHECK == 0)
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
                            CRNewValue.RET_LOAD_HEADER +","+CRNewValue.BayNo + "," + CRNewValue.CompartmentNo +
                            ",:RET_METER_NAME" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(1);

            OraParam.AddOracleParameter(0, "RET_METER_NAME", OracleDbType.Varchar2, 64);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {
                OraParam.GetOracleParameterValue("RET_METER_NAME", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_METER_NAME = p.Value.ToString();
                    
                else
                    CRNewValue.RET_METER_NAME = "";
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

        #region "LLTLB"
        private bool P_CRBAY_CHECK_ACTIVE()
        {
            bool bCheck = false;

            string strSQL = "begin tas.P_CRBAY_CHECK_ACTIVE(" +
                            CRNewValue.ID +
                            ",:RET_ACTIVE,:RET_LOAD_NO" +
                            ");end;";
            COracleParameter p = new COracleParameter();
            //OracleParameter p = null;
            p.CreateParameter(2);

            p.AddOracleParameter(0, "RET_ACTIVE", OracleDbType.Int16);
            p.AddOracleParameter(1, "RET_LOAD_NO", OracleDbType.Double);
            //p.AddOracleParameter(2, "RET_CONFIRM_START", OracleDbType.Int16);

            if (fMain.OraDb.ExecuteSQL(strSQL, p))
            {
                
                //p.GetOracleParameterValue("RET_ACTIVE", ref p);
                if (p.OraParam[0].Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_ACTIVE = Convert.ToInt16(p.OraParam[0].Value.ToString());
                else
                    CRNewValue.RET_ACTIVE = 0;

                //p.GetOracleParameterValue("RET_LOAD_NO", ref p);
                if (p.OraParam[1].Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_HEADER = Convert.ToDouble(p.OraParam[1].Value.ToString());
                else
                    CRNewValue.RET_LOAD_HEADER = 0;
                bCheck = true;
            }
            else
            {
                bCheck = false;
            }
            p.RemoveParameter();
            p = null;
            p = null;
            return bCheck;
        }

        private bool P_CRBAY_CHECK_PERMISSIVE()
        {
            bool bCheck = false;

            string strSQL = "begin tas.P_CRBAY_CHECK_PERMISSIVE(" +
                            CRNewValue.BayNo + "," + (int)CRNewValue.LoadingMode +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";
            COracleParameter p = new COracleParameter();
            p.CreateParameter(3);
            p.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
            p.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2,128);
            p.AddOracleParameter(2, "RET_CR_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, p))
            {
                if (p.OraParam[0].Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.OraParam[0].Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                if (p.OraParam[1].Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.OraParam[1].Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                if (p.OraParam[2].Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CR_MSG = p.OraParam[2].Value.ToString();
                else
                    CRNewValue.RET_CR_MSG = "";
                
                if (CRNewValue.RET_CHECK == -1)
                {
                    bCheck = true;
                    CRNewValue.Permissive = true;
                    CRNewValue.PermissiveMsg = CRNewValue.RET_MSG;
                }
                else
                {
                    bCheck = false;
                    CRNewValue.Permissive = false;
                    CRNewValue.PermissiveMsg = CRNewValue.RET_MSG;
                    CRNewValue.RET_CR_MSG = CRNewValue.PermissiveMsg;
                }
            }
            else
            {
                bCheck = false;
            }
            p.RemoveParameter();
            p = null;
            p = null;
            return bCheck;
        }

        private bool P_CRBAY_CHECK_PERMISSIVE_CANCEL(int pMode)
        {
            bool bCheck = false;

            string strSQL = "begin tas.P_CRBAY_CHECK_PERMISSIVE(" +
                            CRNewValue.BayNo + "," + pMode +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";
            COracleParameter p = new COracleParameter();
            p.CreateParameter(3);
            p.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
            p.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 128);
            p.AddOracleParameter(2, "RET_CR_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, p))
            {
                if (p.OraParam[0].Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.OraParam[0].Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                if (p.OraParam[1].Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.OraParam[1].Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                if (p.OraParam[1].Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CR_MSG = p.OraParam[2].Value.ToString();
                else
                    CRNewValue.RET_CR_MSG = "";

                if (CRNewValue.RET_CHECK == -1)
                {
                    bCheck = true;
                    CRNewValue.NewCancelLoad = true;
                }
                else
                {
                    CRNewValue.NewCancelLoad = false;
                }
            }
            else
            {
                bCheck = false;
            }
            p.RemoveParameter();
            p = null;
            p = null;
            return bCheck;
        }

        private bool P_CRBAY_CHECK_TU()
        {

            bool bCheck = false;
            string strSQL = "begin tas.P_CRBAY_CHECK_TU(" +
                            "tas.get_card_no('" + Convert.ToInt64(CRNewValue.CardCode) + "')," + CRNewValue.ID + ",0,0,sysdate," + CRNewValue.BayNo + ",tas.system_user,tas.server_name" +
                            ",:RET_LOAD_TYPE,:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(4);

            OraParam.AddOracleParameter(0, "RET_LOAD_TYPE", OracleDbType.Int32);
            OraParam.AddOracleParameter(1, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(2, "RET_MSG", OracleDbType.Varchar2, 512);
            OraParam.AddOracleParameter(3, "RET_CR_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {

                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_LOAD_TYPE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_TYPE = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_TYPE = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CR_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_CR_MSG = "";

                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CR_MSG != "")
                    RaiseEvents(CRNewValue.RET_CR_MSG);
                // }
                if (CRNewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }

            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool P_CRBAY_CANCEL_TU()
        {
            bool bCheck = false;
            string strSQL = "begin tas.P_CRBAY_CANCEL_TU(" +
                            CRNewValue.ID + ",sysdate," + CRNewValue.BayNo + 
                            ",:RET_LOAD_TYPE,:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";

            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(4);

            OraParam.AddOracleParameter(0, "RET_LOAD_TYPE", OracleDbType.Int32);
            OraParam.AddOracleParameter(1, "RET_CHECK", OracleDbType.Int16);
            OraParam.AddOracleParameter(2, "RET_MSG", OracleDbType.Varchar2, 512);
            OraParam.AddOracleParameter(3, "RET_CR_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
            {

                OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                OraParam.GetOracleParameterValue("RET_LOAD_TYPE", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_LOAD_TYPE = Convert.ToInt16(p.Value.ToString());
                else
                    CRNewValue.RET_LOAD_TYPE = 0;

                OraParam.GetOracleParameterValue("RET_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                if (p.Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CR_MSG = p.Value.ToString();
                else
                    CRNewValue.RET_CR_MSG = "";

                RaiseEvents(CRNewValue.RET_MSG);
                if (CRNewValue.RET_CR_MSG != "")
                    RaiseEvents(CRNewValue.RET_CR_MSG);
                // }
                if (CRNewValue.RET_CHECK == 0)
                    bCheck = true;
                else
                    bCheck = false;
            }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool P_CRBAY_CHECK_ALARM()
        {
            bool bCheck = false;

            string strSQL = "begin tas.P_CRBAY_CHECK_ALARM(" +
                            CRNewValue.RET_LOAD_HEADER + "," + CRNewValue.BayNo +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";
            COracleParameter p = new COracleParameter();
            p.CreateParameter(3);
            p.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
            p.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);
            p.AddOracleParameter(2, "RET_CR_MSG", OracleDbType.Varchar2, 512);

            if (fMain.OraDb.ExecuteSQL(strSQL, p))
            {
                if (p.OraParam[0].Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CHECK = Convert.ToInt16(p.OraParam[0].Value.ToString());
                else
                    CRNewValue.RET_CHECK = 0;

                if (p.OraParam[1].Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_MSG = p.OraParam[1].Value.ToString();
                else
                    CRNewValue.RET_MSG = "";

                if (p.OraParam[2].Status != OracleParameterStatus.NullFetched)
                    CRNewValue.RET_CR_MSG = p.OraParam[2].Value.ToString();
                else
                    CRNewValue.RET_CR_MSG = "";
                
                if (CRNewValue.RET_CHECK == 0)
                {
                    CRNewValue.MeterAlarm = false;
                    CRNewValue.AlarmMsg = "";
                }
                else
                {
                    CRNewValue.MeterAlarm = true;
                    CRNewValue.AlarmMsg = CRNewValue.RET_CR_MSG;
                    RaiseEvents(CRNewValue.AlarmMsg);
                }

                bCheck = true;
            }
            else
            {
                bCheck = false;
            }
            p.RemoveParameter();
            p = null;
            p = null;
            return bCheck;
        }

        private bool P_CRBAY_COMMAND_START(int pCompartmentNo)
        {
            bool bCheck = false;

            //if (!CRNewValue.Permissive || CRNewValue.MeterAlarm)
            //{
            //    CRNewValue.RET_CHECK = -1;
            //    CRNewValue.RET_MSG = "Error start batch.";
            //    CRNewValue.RET_CR_MSG = CRNewValue.RET_MSG;
            //    if (!CRNewValue.Permissive)
            //        CRNewValue.RET_MSG += " Permissive=false";
            //    if (CRNewValue.MeterAlarm)
            //        CRNewValue.RET_MSG += " Meter Alarm";
            //}
            //else
            //{
                string strSQL = "begin tas.P_CRBAY_COMMAND_START(" +
                                CRNewValue.LoadingInfo_A.LoadHeaderNo + "," + pCompartmentNo + "," + CRNewValue.BayNo +
                                ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                                ");end;";
                COracleParameter OraParam = new COracleParameter();
                OracleParameter p = null;
                OraParam.CreateParameter(3);
                try
                {
                    OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
                    OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);
                    OraParam.AddOracleParameter(2, "RET_CR_MSG", OracleDbType.Varchar2, 512);

                    if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
                    {

                        OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                        if (p.Status != OracleParameterStatus.NullFetched)
                            CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                        else
                            CRNewValue.RET_CHECK = 0;

                        OraParam.GetOracleParameterValue("RET_MSG", ref p);
                        if (p.Status != OracleParameterStatus.NullFetched)
                            CRNewValue.RET_MSG = p.Value.ToString();
                        else
                            CRNewValue.RET_MSG = "";

                        OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                        if (p.Status != OracleParameterStatus.NullFetched)
                            CRNewValue.RET_CR_MSG = p.Value.ToString();
                        else
                            CRNewValue.RET_CR_MSG = "";


                        if (CRNewValue.RET_CHECK == 0)
                            bCheck = true;
                        else
                            bCheck = false;
                    }
                }
                catch (Exception exp)
                { }

                OraParam.RemoveParameter();
                OraParam = null;
                p = null;
            //}
            return bCheck;
        }

        private bool P_CRBAY_COMMAND_STOP()
        {
            bool bCheck = false;

            string strSQL = "begin tas.P_CRBAY_COMMAND_STOP(" +
                            CRNewValue.LoadingInfo_A.LoadHeaderNo + "," +  CRNewValue.BayNo +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);
            try
            {
                OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
                OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);
                OraParam.AddOracleParameter(2, "RET_CR_MSG", OracleDbType.Varchar2, 512);

                if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
                {

                    OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                    else
                        CRNewValue.RET_CHECK = 0;

                    OraParam.GetOracleParameterValue("RET_MSG", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        CRNewValue.RET_MSG = p.Value.ToString();
                    else
                        CRNewValue.RET_MSG = "";

                    OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        CRNewValue.RET_CR_MSG = p.Value.ToString();
                    else
                        CRNewValue.RET_CR_MSG = "";


                    if (CRNewValue.RET_CHECK == 0)
                        bCheck = true;
                    else
                        bCheck = false;
                }
            }
            catch (Exception exp)
            { }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private bool P_CRBAY_COMMAND_END()
        {
            bool bCheck = false;

            string strSQL = "begin tas.P_CRBAY_COMMAND_END(" +
                            CRNewValue.LoadingInfo_A.LoadHeaderNo + "," + CRNewValue.BayNo +
                            ",:RET_CHECK,:RET_MSG,:RET_CR_MSG" +
                            ");end;";
            COracleParameter OraParam = new COracleParameter();
            OracleParameter p = null;
            OraParam.CreateParameter(3);
            try
            {
                OraParam.AddOracleParameter(0, "RET_CHECK", OracleDbType.Int16);
                OraParam.AddOracleParameter(1, "RET_MSG", OracleDbType.Varchar2, 512);
                OraParam.AddOracleParameter(2, "RET_CR_MSG", OracleDbType.Varchar2, 512);

                if (fMain.OraDb.ExecuteSQL(strSQL, OraParam))
                {

                    OraParam.GetOracleParameterValue("RET_CHECK", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        CRNewValue.RET_CHECK = Convert.ToInt16(p.Value.ToString());
                    else
                        CRNewValue.RET_CHECK = 0;

                    OraParam.GetOracleParameterValue("RET_MSG", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        CRNewValue.RET_MSG = p.Value.ToString();
                    else
                        CRNewValue.RET_MSG = "";

                    OraParam.GetOracleParameterValue("RET_CR_MSG", ref p);
                    if (p.Status != OracleParameterStatus.NullFetched)
                        CRNewValue.RET_CR_MSG = p.Value.ToString();
                    else
                        CRNewValue.RET_CR_MSG = "";


                    if (CRNewValue.RET_CHECK == 0)
                        bCheck = true;
                    else
                        bCheck = false;
                }
            }
            catch (Exception exp)
            { }
            OraParam.RemoveParameter();
            OraParam = null;
            p = null;
            return bCheck;
        }

        private void INSERT_CARDREADER_EVENT(string pMsg)
        {
            string strSQL = "begin steqi.INSERT_CARDREADER_EVENT(" +
                            DateTime.Now + "," + CRNewValue.ID + "," + pMsg +
                            ");end;";

        }
        #endregion
        #endregion

        #region Process Bay Loading
        #region "SAKCTAS"
        private void CRBayLoading()
        {

                DisplayToCardReader(CRNewValue.RET_STEP);
                while (thrRun)
                {
                    //M_CRBAY_CHECK_TOPUP();//ตรววสอบโหมด
                    if (CRNewValue.ModeStatus == 0)
                    {
                        Thread.Sleep(300);
                        switch (CRNewValue.RET_STEP)
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
                                CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                                break;
                            case 3:
                                CRBayLoading_Compartment();
                                break;
                            case (int)_CRStepProcess.LoadingStart:
                                CRBayLoading_Start();
                                break;
                            case (int)_CRStepProcess.LoadingLoad:
                                CRBayLoading_Load();
                                break;
                            case (int)_CRStepProcess.LoadingStop:
                                CRBayLoading_Stop();
                                break;
                            case (int)_CRStepProcess.OperatorConfirm:             // operator confirm
                                CRBayOperatorConfirm();
                                break;
                            case (int)_CRStepProcess.DriverConfirm:             //driver confirm
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
            DisplayToCardReader(CRNewValue.RET_STEP);
            while (thrRun)
            {
                M_CRBAY_CHECK_TOPUP();//ตรววสอบโหมด
                if (CRNewValue.ModeStatus == 1)
                {
                    Thread.Sleep(600);
                    switch (CRNewValue.RET_STEP)
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
                            CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                            break;
                        case 3:
                            CRBayLoadingTopup_Compartment();
                            break;
                        case (int)_CRStepProcess.LoadingStart:
                            CRBayLoadingTopup_Start();            //start
                            break;
                        case (int)_CRStepProcess.LoadingLoad:
                            CRBayLoadingTopup_Load();            //topup load
                            break;
                        case (int)_CRStepProcess.LoadingStop:
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
                if (Convert.ToDouble(CRNewValue.RET_LOAD_HEADER) > 0)
                {
                    CRNewValue.CompartmentNo = 1;
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
                DisplayToCardReader((int)_CRStepProcess.DisplayDateTime);
                string vRecv = "";
                SendToCardReader(mercuryProtocol.SendNextQueueBlock(),true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                //vRecv = CR_NewValue.DataReceive;
                CRNewValue.CardCode = CheckDataCard(ref CRNewValue.DataReceive);
                
                if (Convert.ToInt64(CRNewValue.CardCode) > 0)
                {
                    CROldValue.CardCode = CRNewValue.CardCode;
                   
                     M_CRBAY_CHECK_BAY();
                    if (CRNewValue.RET_LOAD_HEADER == 0)
                    {
                        CRNewValue.CardCode = CROldValue.CardCode;
                        M_CRBAY_CHECK_STEP();
                        if (CRNewValue.RET_STEP == 3)
                        {
                            if (M_CRBAY_LOAD_CHECK_TU())
                            {
                                //M_CRBAY_LOAD_CHECK_TU();
                                if (CRNewValue.RET_LOAD_TYPE == 1)
                                {
                                    CROldValue = CRNewValue;
                                    //M_CRBAY_CHECK_STEP();
                                }
                                else
                                {
                                    DisplayToCardReader(CRNewValue.RET_LOAD_TYPE);
                                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                                    SendToCardReader(mercuryProtocol.ClearDisplay());
                                    DisplayToCardReader(CRNewValue.RET_STEP);
                                }
                            }
                            else
                            {
                                DisplayToCardReader(-1);
                                //Thread.Sleep(5000);
                                ClearDataCard();
                                CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                                SendToCardReader(mercuryProtocol.ClearDisplay());
                                DisplayToCardReader(CRNewValue.RET_STEP);
                            }
                        }
                        else
                        {
                            if (CRNewValue.RET_STEP == -1)
                            {
                                DisplayToCardReader(-1);
                                ClearDataCard();
                                CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                                SendToCardReader(mercuryProtocol.ClearDisplay());
                                DisplayToCardReader(CRNewValue.RET_STEP);
                            }
                        }
                    }
                    else
                    {
                        CRNewValue.CardCode = CROldValue.CardCode;//อ่านการ์ดมาใหม่
                        M_CRBAY_CHECK_STEP();
                        if (CRNewValue.RET_STEP == -1)
                        {
                            DisplayToCardReader(-1);
                            ClearDataCard();
                            CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                            SendToCardReader(mercuryProtocol.ClearDisplay());
                            DisplayToCardReader(CRNewValue.RET_STEP);
                        }
                    }
                }
                if ((CRNewValue.RET_TOT_COMPARTMENT > 0) && (CRNewValue.RET_STEP != (int)_CRStepProcess.LoadingNone))
                {
                    if (CRNewValue.RET_IS_BLENDING == 0)
                    {
                        CROldValue = CRNewValue;
                        CRNewValue.CompartmentNo = 1;

                        //M_CRBAY_CHECK_COMPARTMENT();
                        if (M_BATCH_DETAIL_COMPARTMENT())
                        {
                            CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingStart;
                            //DisplayToCardReader(mCR_NewValue.RET_STEP);
                            CRNewValue.CardCode = CROldValue.CardCode;
                        }
                        else
                        {
                            DisplayToCardReader(-1);
                            //Thread.Sleep(5000);
                            ClearDataCard();
                            M_CRBAY_LOAD_CHECK_TU();
                            CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                            SendToCardReader(mercuryProtocol.ClearDisplay());
                            DisplayToCardReader(CRNewValue.RET_STEP);
                        }
                    }
                    else
                    {
                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingCompartment;
                        DisplayToCardReader(CRNewValue.RET_STEP);
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
                else if (CRNewValue.RET_STEP == (int)_CRStepProcess.OperatorConfirm)
                {
                    CRNewValue.RET_STEP = (int)_CRStepProcess.OperatorConfirm;
                }
                else if (CRNewValue.RET_STEP == (int)_CRStepProcess.DriverConfirm)
                {
                    CRNewValue.RET_STEP = (int)_CRStepProcess.DriverConfirm;
                }
                else
                {
                    //DisplayToCardReader(mCR_NewValue.RET_STEP);
                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                    DisplayToCardReader(CRNewValue.RET_STEP);
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
                DisplayToCardReader((int)_CRStepProcess.DisplayDateTime);
                string vRecv = "";
                SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                //vRecv = CR_NewValue.DataReceive;
                CRNewValue.CardCode = CheckDataCard(ref CRNewValue.DataReceive);

                if (Convert.ToInt64(CRNewValue.CardCode) > 0)
                {
                    CROldValue.CardCode = CRNewValue.CardCode;
                    //MessageBox.Show(mCR_OldValue.CardCode);
                    //if (mCR_NewValue.Tmode == 1) //ถ้าเท่ากับโหมด Topup
                    //{
                        M_CRBAY_TOPUP_CHECK_CARD();
                        if (CRNewValue.RET_CHECK == 0) //ตรวจสอบบัตรพนักงาน
                        {
                            if (CRNewValue.RET_BATCH_STATUS < 6)
                            {
                         
                                if (M_CRBAY_LOAD_CHECK_TU_TOPUP())
                                {
                                    //M_CRBAY_LOAD_CHECK_TU();
                                    //CRBayLoading_Topup();
                                    if (CRNewValue.RET_LOAD_TYPE == 1)
                                    {
                                        M_CRBAY_TOPUP_CHECK_COMP();
                                        RaiseEvents(CRNewValue.RET_MSG);
                                        //M_CRBAY_CHECK_COMP_TOPUP();
                                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingStart;
                                        CRNewValue.CardCode = CROldValue.CardCode;
                                        // CRBayLoading_Topup();
                                    }
                                    else
                                    {
                                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                                        // M_CRBAY_LOAD_CHECK_TU();
                                        ClearDataCard();
                                        SendToCardReader(mercuryProtocol.ClearDisplay());
                                        DisplayToCardReader(CRNewValue.RET_STEP);
                                        //CRBayLoading_Topup();
                                    }
                                }
                                else
                                {
                                    DisplayToCardReader(-1);
                                    //Thread.Sleep(5000);
                                    ClearDataCard();
                                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                                    SendToCardReader(mercuryProtocol.ClearDisplay());
                                    DisplayToCardReader(CRNewValue.RET_STEP);
                                    //CRBayLoading_Topup();
                                }
                                
                            }

                        }
                        else
                        {
                            RaiseEvents(CRNewValue.RET_MSG);
                            CRNewValue.RET_STEP = (int)_CRStepProcess.NoDataFound;
                            //MessageBox.Show(mCR_NewValue.RET_MSG);
                            ////CRBayLoading_Topup();
                            ClearDataCard();
                            SendToCardReader(mercuryProtocol.ClearDisplay());
                            DisplayToCardReader(CRNewValue.RET_STEP);
                            
                            //CRBayLoading_Topup();

                            CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                           // M_CRBAY_LOAD_CHECK_TU();
                            ClearDataCard();
                            SendToCardReader(mercuryProtocol.ClearDisplay());
                            DisplayToCardReader(CRNewValue.RET_STEP);
                            //CRBayLoading_Topup();
                        }
                }
                else
                {
                    DisplayToCardReader(CRNewValue.RET_STEP);
                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                }
            }
            catch (Exception exp)
            { }
        }
        
        private void CRBayLoading_Cancel()
        {
            DisplayToCardReader((int)_CRStepProcess.LoadingCancel);
            Thread.Sleep(5000);
            CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
            SendToCardReader(mercuryProtocol.ClearDisplay());
            DisplayToCardReader(CRNewValue.RET_STEP);
        }

        private void CRBayLoading_Compartment()
        {
            DisplayToCardReader((int)_CRStepProcess.LoadingCompartment);
            //RaiseEvents("["+ mCR_NewValue.RET_TU_ID + "]" +"Enter Compartment" + "[Load No=" + mCR_NewValue.RET_LOAD_HEADER + "]");
            RaiseEvents("" + CRNewValue.RET_TU_ID  + "Select Product " + " Load No=" + CRNewValue.RET_LOAD_HEADER + ".");
            string vRecv = "",k="";
            CROldValue = CRNewValue;
            while (CRNewValue.RET_STEP == (int)_CRStepProcess.LoadingCompartment)
            {
                if (!thrRun)
                    break;

                SendToCardReader(mercuryProtocol.SendNextQueueBlock(),true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                CRNewValue.CardCode = CheckDataCard(ref CRNewValue.DataReceive);

                if (Convert.ToInt64(CRNewValue.CardCode) > 0)      //cancel load
                {
                    //M_CRBAY_LOAD_CHECK_TU();
                    if (M_CRBAY_LOAD_CHECK_TU())
                    {
                        //DisplayToCardReader((int)LoadingStep.LoadingCancel);
                        //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        CROldValue = CRNewValue;
                        CRBayLoading_Cancel();
                    }
                    else
                        if (CRNewValue.RET_CHECK == 0)
                        {
                            CRNewValue.CardCode = CROldValue.CardCode;
                        }
                        else
                        {
                            DisplayToCardReader(-1);
                            CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingCompartment;
                        }
                }
                else
                {
                    CRNewValue.CardCode=CROldValue.CardCode;
                    if (CRNewValue.DataReceive != "")
                    {
                        k = CheckKeyPress(ref CRNewValue.DataReceive);
                    }
                }
                if (k != "") //check enter compartment
                {
                    int val;
                    if(int.TryParse(k,out val))
                        CRNewValue.CompartmentNo = Convert.ToInt16(k);
                    else
                        CRNewValue.CompartmentNo =0;
                    //M_CRBAY_CHECK_COMPARTMENT();
                    if ((CRNewValue.CompartmentNo > 0) && M_CRBAY_CHECK_COMPARTMENT())
                    {
                        DisplayToCardReader((int)_CRStepProcess.LoadingStart);
                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingStart;
                        //SendToCardReader(PMercuryFnc.ClearDisplay());
                    }
                
                    else
                    {
                        string s="";
                        DisplayToCardReader(-1);
                        //Thread.Sleep(5000);
                        SendToCardReader(mercuryProtocol.MoveCursor(8,1) + s.PadRight(40,' '));
                        DisplayToCardReader((int)_CRStepProcess.LoadingCompartment);
                    }
                }
                else//display compartment
                {
                    SendToCardReader(mercuryProtocol.MoveCursor(1, 1) +  mercuryProtocol.MoveCursor(1, 20) + DateTime.Now);
                    //M_CRBAY_CHECK_STEP();
                    //if (M_CRBAY_CHECK_STEP())
                    //{
                    //    DisplayToCardReader(CR_NewValue.RET_STEP);
                    //}
                    //else
                    //    DisplayToCardReader(-1);
                }
                if (!thrRun)
                    break;
            }
        }
        
        private void CRBayLoadingTopup_Compartment()
        {
            DisplayToCardReader((int)_CRStepProcess.LoadingCompartment);
            //RaiseEvents("["+ mCR_NewValue.RET_TU_ID + "]" +"Enter Compartment" + "[Load No=" + mCR_NewValue.RET_LOAD_HEADER + "]");
            RaiseEvents("[Top up]" + CRNewValue.RET_TU_ID  + " Select Product" + " Load No=" + CRNewValue.RET_LOAD_HEADER + ".");
            string vRecv = "", k = "";
            CROldValue = CRNewValue;
            while (CRNewValue.RET_STEP == (int)_CRStepProcess.LoadingCompartment)
            {
                if (!thrRun)
                    break;

                SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                CRNewValue.CardCode = CheckDataCard(ref CRNewValue.DataReceive);

                if (Convert.ToInt64(CRNewValue.CardCode) > 0)      //cancel load
                {
                    //M_CRBAY_LOAD_CHECK_TU();
                    if (M_CRBAY_LOAD_CHECK_TU())
                    {
                        //DisplayToCardReader((int)LoadingStep.LoadingCancel);
                        //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        CROldValue = CRNewValue;
                        CRBayLoading_Cancel();
                    }
                    else
                        if (CRNewValue.RET_CHECK == 0)
                        {
                            CRNewValue.CardCode = CROldValue.CardCode;
                        }
                        else
                        {
                            DisplayToCardReader(-1);
                            CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingCompartment;
                        }
                }
                else
                {
                    CRNewValue.CardCode = CROldValue.CardCode;
                    if (CRNewValue.DataReceive != "")
                    {
                        k = CheckKeyPress(ref CRNewValue.DataReceive);
                    }
                }
                if (k != "") //check enter compartment
                {
                    int val;
                    if (int.TryParse(k, out val))
                        CRNewValue.CompartmentNo = Convert.ToInt16(k);
                    else
                        CRNewValue.CompartmentNo = 0;
                    //M_CRBAY_CHECK_COMPARTMENT();
                    if ((CRNewValue.CompartmentNo > 0) && M_CRBAY_CHECK_COMPARTMENT())
                    {
                        DisplayToCardReader((int)_CRStepProcess.LoadingStart);
                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingStart;
                        //SendToCardReader(PMercuryFnc.ClearDisplay());
                    }

                    else
                    {
                        string s = "";
                        DisplayToCardReader(-1);
                        //Thread.Sleep(5000);
                        SendToCardReader(mercuryProtocol.MoveCursor(8, 1) + s.PadRight(40, ' '));
                        DisplayToCardReader((int)_CRStepProcess.LoadingCompartment);
                    }
                }
                else//display compartment
                {
                    SendToCardReader(mercuryProtocol.MoveCursor(1, 1) + mercuryProtocol.MoveCursor(1, 20) + DateTime.Now);
                    //M_CRBAY_CHECK_STEP();
                    //if (M_CRBAY_CHECK_STEP())
                    //{
                    //    DisplayToCardReader(CR_NewValue.RET_STEP);
                    //}
                    //else
                    //    DisplayToCardReader(-1);
                }
                if (!thrRun)
                    break;
            }
        }
        
        private void CRBayLoading_Start() //check key F1 for start ,F3 for cancel
        {
            string vRecv = "", k = "";
            CRNewValue.IsAlarm = false;
            CROldValue = CRNewValue;
            SendToCardReader(mercuryProtocol.ClearDisplay());
            DisplayToCardReader(CRNewValue.RET_STEP);
            SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
            while (CRNewValue.RET_STEP == (int)_CRStepProcess.LoadingStart)
            {
               // SendToCardReader(mMercuryLib.MoveCursor(1, 20) + DateTime.Now);
                //SendToCardReader(DisplayDateTime());
                //DisplayToCardReader((int)LoadingStep.DisplayDateTime);
                DisplayToCardReader((int)_CRStepProcess.DisplayDateTime);
                SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                CRNewValue.CardCode = CheckDataCard(ref CRNewValue.DataReceive);
                if (Convert.ToInt64(CRNewValue.CardCode) == 0)
                    k = CheckKeyPress(ref CRNewValue.DataReceive);
                //if(k!="")
                //    RaiseEvents("Enter key=" + k);
                if (!M_BATCH_CHECK_ALARM())
                {
                    if (!CRNewValue.IsAlarm)
                    {
                        DisplayMeterAlarm();
                        Thread.Sleep(500);
                        CRNewValue.IsAlarm = true;
                        //SendToCardReader(ClearLastLine());
                    }
                    //DisplayToCardReader(mCR_NewValue.RET_STEP);
                    //goto Next;
                }
                else
                {
                    if (CRNewValue.IsAlarm)
                    {
                        CRNewValue.IsAlarm = false;
                        SendToCardReader(ClearLastLine());
                    }
                }

                if(k=="F1")
                {
                    CRNewValue.CardCode = CROldValue.CardCode;
                    if (!M_BATCH_CHECK_ALARM())
                    {
                        CRNewValue.IsAlarm = true;
                        DisplayMeterAlarm();
                        Thread.Sleep(500);
                        //DisplayToCardReader(mCR_NewValue.RET_STEP);
                        goto Next;
                    }
                    if ( M_CRBAY_CHECK_COMPARTMENT())
                    {
                        if (M_CRBAY_BATCH_START()) //M_CRBAY_BATCH_START()
                        {
                            SendToCardReader(mercuryProtocol.MoveCursor(8, 1) + "Start Load");
                            Thread.Sleep(3000);
                            //M_BATCH_START_LOADING();
                            CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingLoad;
                            RaiseEvents("[Start Load]" + CRNewValue.RET_TU_ID + " Load No=" + CRNewValue.RET_LOAD_HEADER + " Comp.No=" + CRNewValue.CompartmentNo + ".");
                            SendToCardReader(ClearLastLine());
                            //DisplayToCardReader((int)LoadingStep.LoadingLoad);
                            //Thread.Sleep(5000);
                        }
                        else
                        {
                            CRNewValue.CardCode = CROldValue.CardCode;
                            DisplayToCardReader(-1);
                            DisplayToCardReader((int)_CRStepProcess.LoadingStart);
                        }
                    }
                    else
                    {
                        DisplayToCardReader(-1);
                        DisplayToCardReader((int)_CRStepProcess.LoadingStart);
                    }
                }
                else if (k == "F3")
                {
                    CRNewValue.CardCode = CROldValue.CardCode;
                    CRNewValue.RET_LOAD_HEADER = CROldValue.RET_LOAD_HEADER;
                    CRNewValue.RET_CR_MSG = "Cancel Load";
                    
                    RaiseEvents("[Cancel Load]" + CRNewValue.RET_TU_ID + " Load No=" + CRNewValue.RET_LOAD_HEADER + " Comp.No=" + CRNewValue.CompartmentNo + ".");
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    DisplayToCardReader((int)_CRStepProcess.LoadingCancel);
                    CRNewValue.RET_CR_MSG = "";
                    SendToCardReader(ClearLastLine());
                    Thread.Sleep(3000);
                    //CRBayLoading_Stop();
                    if (CRNewValue.RET_IS_BLENDING == 0)
                    {
                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                        M_CRBAY_LOAD_CHECK_TU();
                        ClearDataCard();
                        SendToCardReader(mercuryProtocol.ClearDisplay());
                        DisplayToCardReader(CRNewValue.RET_STEP);
                    }
                    else
                    {
                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingCompartment;
                    }
                }
                else if (Convert.ToInt64(CRNewValue.CardCode) > 0)
                {
                    CRNewValue.CardCode = CROldValue.CardCode;
                    CRNewValue.RET_LOAD_HEADER = CROldValue.RET_LOAD_HEADER;
                    CRNewValue.RET_CR_MSG = "Cancel Load";
                    //mCR_NewValue.CompartmentNo = 1;
                    RaiseEvents("[Cancel Load]" + CRNewValue.RET_TU_ID + " Load No=" + CRNewValue.RET_LOAD_HEADER + " Comp.No=" + CRNewValue.CompartmentNo + ".");
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    DisplayToCardReader((int)_CRStepProcess.LoadingCancel);
                    CRNewValue.RET_CR_MSG = "";
                    SendToCardReader(ClearLastLine());
                    Thread.Sleep(3000);
                    //CRBayLoading_Stop();
                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                    M_CRBAY_LOAD_CHECK_TU();
                    ClearDataCard();
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    DisplayToCardReader(CRNewValue.RET_STEP);
                    //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStop;
                }
            Next:
                if (!thrRun)
                    break;
                //SendToCardReader(DisplayDateTime());
                Thread.Sleep(600);
            }
        }

        private void CRBayLoading_Load() //check key F1 for start ,F3 for stop
        {
            string vRecv = "", k = "";
            DisplayToCardReader((int)_CRStepProcess.LoadingLoad);
            RaiseEvents("[Loading]" + CRNewValue.RET_TU_ID + " Loading in progress" + " Load No=" + CRNewValue.RET_LOAD_HEADER + " Comp.No=" + CRNewValue.CompartmentNo + ".");
            CRNewValue.IsAlarm = false;

            while (CRNewValue.RET_STEP == (int)_CRStepProcess.LoadingLoad)
            {
                if (!thrRun)
                    break;
                //SendToCardReader(PMercuryFnc.MoveCursor(1, 1) + PMercuryFnc.MoveCursor(1, 20) + DateTime.Now);
                SendToCardReader(DisplayDateTime());
                SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                k = CheckKeyPress(ref CRNewValue.DataReceive);
                if (k != "")
                    RaiseEvents("Enter key=" + k);
                if (k == "F3")
                {
                    CRNewValue.CardCode = CROldValue.CardCode;
                    RaiseEvents("[Stop Load]" + CRNewValue.RET_TU_ID + "");
                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingStop;
                    SendToCardReader(mercuryProtocol.MoveCursor(8, 1) + "Stop Load");
                     Thread.Sleep(1000);
                     SendToCardReader(ClearLastLine());
                    //if (M_BATCH_STOP())
                    //{
                    //    M_BATCH_STOP_LOADING();
                    //    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                    //    DisplayToCardReader((int)LoadingStep.LoadingStop);
                    //    RaiseEvents("Stop Load[" + mCR_NewValue.RET_TU_ID + "]"+ "[Load No=" + mCR_NewValue.RET_LOAD_HEADER + "][Comp.No=" + mCR_NewValue.CompartmentNo + "]"); 
                    //    Thread.Sleep(5000);
                    //}
                }
                else
                {
                    if (!M_BATCH_CHECK_ALARM())
                    {
                        CRNewValue.IsAlarm = true;
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

                        switch (CRNewValue.RET_BATCH_STATUS)
                        {
                            case 3:
                                DisplayToCardReader((int)_CRStepProcess.LoadingLoad);
                                break;
                            case 4:
                                DisplayToCardReader((int)_CRStepProcess.LoadingLoad);
                                break;
                            case 5:
                                CRNewValue.CardCode = CROldValue.CardCode;
                                CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingStop;
                                Thread.Sleep(1000);
                                break;
                            case 6:
                                DisplayToCardReader((int)_CRStepProcess.LoadingComplete);
                                RaiseEvents("[Complete Load]" + CRNewValue.RET_TU_ID  + " Load No=" + CRNewValue.RET_LOAD_HEADER + " Comp.No=" + CRNewValue.CompartmentNo + ".");
                                Thread.Sleep(1000);
                                SendToCardReader(mercuryProtocol.ClearDisplay());
                                M_CRBAY_CHECK_STEP();
                                if (CRNewValue.RET_STEP == (int)_CRStepProcess.LoadingNone)
                                {
                                    ClearDataCard();
                                }
                                SendToCardReader(mercuryProtocol.ClearDisplay());
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
                DisplayToCardReader((int)_CRStepProcess.LoadingStop);
                Thread.Sleep(3000);
                //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                //DisplayToCardReader((int)LoadingStep.LoadingStop);
                
                RaiseEvents("[Stop Load]" + CRNewValue.RET_TU_ID  + " Load No=" + CRNewValue.RET_LOAD_HEADER + " Comp.No=" + CRNewValue.CompartmentNo + ".");
                Thread.Sleep(2000);
                if (M_CRBAY_CHECK_STEP())
                {
                    if (CRNewValue.RET_STEP == 3)
                    {
                        M_CRBAY_GET_PRESET();
                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingStart;
                    }
                    else
                    {
                        DisplayToCardReader(CRNewValue.RET_STEP);
                    }
                }
                else
                {
                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    DisplayToCardReader(CRNewValue.RET_STEP);
                }
                SendToCardReader(mercuryProtocol.ClearDisplay());
                SendToCardReader(mercuryProtocol.SendNextQueueBlock(),true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
            }
        }

        private void CRBayOperatorConfirm()
        {
            string vRecv = "", k = "",vMsg="";
            SendToCardReader(mercuryProtocol.ClearDisplay());
            //RaiseEvents("Operator confirm");
            DisplayToCardReader((int)_CRStepProcess.OperatorConfirm);
            CROldValue = CRNewValue;
            while (CRNewValue.RET_STEP == (int)_CRStepProcess.OperatorConfirm)
            {
                DisplayToCardReader((int)_CRStepProcess.DisplayDateTime);
                SendToCardReader(mercuryProtocol.SendNextQueueBlock(),true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                //vRecv = CR_NewValue.DataReceive;
                CRNewValue.CardCode = CheckDataCard(ref CRNewValue.DataReceive);

                if (Convert.ToInt64(CRNewValue.CardCode) > 0)
                {
                   //call procedure 
                    if (M_CRBAY_OPERATOR_CONFIRM())
                    {
                        SendToCardReader(mercuryProtocol.MoveCursor(7, 1) + CRNewValue.RET_CR_MSG);
                        Thread.Sleep(3000);
                        //SendToCardReader(mMercuryLib.ClearDisplay());
                        CRNewValue.RET_STEP = (int)_CRStepProcess.DriverConfirm;
                    }
                    else
                    {
                        DisplayToCardReader((int)_CRStepProcess.NoDataFound);
                        DisplayToCardReader((int)_CRStepProcess.OperatorConfirm);
                    }
                }
                else
                {
                    k = CheckKeyPress(ref CRNewValue.DataReceive);
                    if (k == "F3")
                    {
                        RaiseEvents("Cancel operator confrim");
                        SendToCardReader(mercuryProtocol.ClearDisplay());
                        //vMsg += mMercuryLib.MoveCursor(1, 1) + "55 Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                        vMsg += mercuryProtocol.MoveCursor(5, 1) + "         Cancel operator confrim                  ";
                        SendToCardReader(vMsg.ToUpper());
                        Thread.Sleep(5000);
                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                        SendToCardReader(mercuryProtocol.ClearDisplay());
                        DisplayToCardReader(CRNewValue.RET_STEP);
                    }
                }
                //SendToCardReader(DisplayDateTime());
                if (!thrRun)
                    break;
                Thread.Sleep(100);
            }
        }
        
        private void CRBayOperatorConfirm_Topup()
        {
            string vRecv = "", k = "", vMsg = "";
            SendToCardReader(mercuryProtocol.ClearDisplay());
            //RaiseEvents("Operator confirm");
            DisplayToCardReader((int)_CRStepProcess.OperatorConfirm);
            CROldValue = CRNewValue;
            while (CRNewValue.RET_STEP == (int)_CRStepProcess.OperatorConfirm)
            {
                SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                //vRecv = CR_NewValue.DataReceive;
                CRNewValue.CardCode = CheckDataCard(ref CRNewValue.DataReceive);

                if (Convert.ToInt64(CRNewValue.CardCode) > 0)
                {
                    //call procedure 
                    if (M_CRBAY_OPERATOR_CONFIRM())
                    {
                        SendToCardReader(mercuryProtocol.MoveCursor(7, 1) + CRNewValue.RET_CR_MSG);
                        Thread.Sleep(3000);
                        //SendToCardReader(mMercuryLib.ClearDisplay());
                        CRNewValue.RET_STEP = (int)_CRStepProcess.DriverConfirm;
                    }
                }
                else
                {
                    k = CheckKeyPress(ref CRNewValue.DataReceive);
                    if (k == "F3")
                    {
                        RaiseEvents("Cancel operator confrim");
                        SendToCardReader(mercuryProtocol.ClearDisplay());
                        //vMsg += mMercuryLib.MoveCursor(1, 1) + "55 Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                        vMsg += mercuryProtocol.MoveCursor(5, 1) + "         Cancel operator confrim                  ";
                        SendToCardReader(vMsg.ToUpper());
                        Thread.Sleep(5000);
                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                        SendToCardReader(mercuryProtocol.ClearDisplay());
                        DisplayToCardReader(CRNewValue.RET_STEP);
                    }
                }
                SendToCardReader(DisplayDateTime());
                if (!thrRun)
                    break;
                Thread.Sleep(600);
            }
        }
        
        private void CRBayDriverConfirm()
        {
            string vRecv = "", k = "", vMsg = "";
            SendToCardReader(mercuryProtocol.ClearDisplay());
            //RaiseEvents("Driver confirm");
            DisplayToCardReader((int)_CRStepProcess.DriverConfirm);
            CROldValue = CRNewValue;
            while (CRNewValue.RET_STEP == (int)_CRStepProcess.DriverConfirm)
            {
                //SendToCardReader(DisplayDateTime());
                DisplayToCardReader((int)_CRStepProcess.DisplayDateTime);
                SendToCardReader(mercuryProtocol.SendNextQueueBlock(),true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                //vRecv = CR_NewValue.DataReceive;
                CRNewValue.CardCode = CheckDataCard(ref CRNewValue.DataReceive);

                if (Convert.ToInt64(CRNewValue.CardCode) > 0)
                {
                    //call procedure 
                    if (M_CRBAY_DRIVER_CONFIRM())
                    {
                        SendToCardReader(mercuryProtocol.MoveCursor(7, 1) + CRNewValue.RET_CR_MSG);
                        Thread.Sleep(3000);
                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                        SendToCardReader(mercuryProtocol.ClearDisplay());
                        DisplayToCardReader(CRNewValue.RET_STEP);
                    }
                    else
                    {
                        DisplayToCardReader((int)_CRStepProcess.NoDataFound);
                        //Thread.Sleep(3000);
                        DisplayToCardReader((int)_CRStepProcess.DriverConfirm);
                    }

                }
                else
                {
                    k = CheckKeyPress(ref CRNewValue.DataReceive);
                    if (k == "F3")
                    {
                        RaiseEvents("Cancel Driver confrim");
                        SendToCardReader(mercuryProtocol.ClearDisplay());
                        //vMsg += mMercuryLib.MoveCursor(1, 1) + "55 Bay:" + mCR_NewValue.BayNo + " " + mCR_NewValue.Name + DisplayDateTime();
                        vMsg += mercuryProtocol.MoveCursor(5, 1) + "         Cancel driver confrim                  ";
                        SendToCardReader(vMsg.ToUpper());
                        Thread.Sleep(5000);
                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                        SendToCardReader(mercuryProtocol.ClearDisplay());
                    }
                }
                //SendToCardReader(DisplayDateTime());
                if (!thrRun)
                    break;
                Thread.Sleep(100);
            }
        }

        private void CRBayLoadingTopup_Start() //check key F1 for start ,F3 for cancel
        {
            string vRecv = "", k = "";
            CRNewValue.IsAlarm = false;
            CROldValue = CRNewValue;
            SendToCardReader(mercuryProtocol.ClearDisplay());
            DisplayToCardReader(CRNewValue.RET_STEP);
            SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
            while (CRNewValue.RET_STEP == (int)_CRStepProcess.LoadingStart)
            {
                // SendToCardReader(mMercuryLib.MoveCursor(1, 20) + DateTime.Now);
                //SendToCardReader(DisplayDateTime());
                DisplayToCardReader((int)_CRStepProcess.DisplayDateTime);
                SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                CRNewValue.CardCode = CheckDataCard(ref CRNewValue.DataReceive);
                if (Convert.ToInt64(CRNewValue.CardCode) == 0)
                    k = CheckKeyPress(ref CRNewValue.DataReceive);
                //if(k!="")
                //    RaiseEvents("Enter key=" + k);
                if (!M_BATCH_CHECK_ALARM())
                {
                    if (!CRNewValue.IsAlarm)
                    {
                        DisplayMeterAlarm();
                        Thread.Sleep(500);
                        CRNewValue.IsAlarm = true;
                        DisplayToCardReader(CRNewValue.RET_STEP);
                    }
                    //DisplayToCardReader(mCR_NewValue.RET_STEP);
                    //goto Next;
                }
                else
                {
                    if (CRNewValue.IsAlarm)
                    {
                        CRNewValue.IsAlarm = false;
                       SendToCardReader(ClearLastLine());
                    }
                }

                if (k == "F1")
                {
                    CRNewValue.CardCode = CROldValue.CardCode;
                    if (!M_BATCH_CHECK_ALARM())
                    {
                        CRNewValue.IsAlarm = true;
                        DisplayMeterAlarm();
                        Thread.Sleep(500);
                        //DisplayToCardReader(mCR_NewValue.RET_STEP);
                        goto Next;
                    }
                    // M_CRBAY_CHECK_COMPARTMENT
                    if (M_CRBAY_TOPUP_CHECK_COMP())
                    {
                        CRNewValue.CardCode = CROldValue.CardCode;
                        //M_CRBAY_TOPUP_CHECK_COMP();
                        if (M_CRBAY_TOPUP_BATCH_START())//M_CRBAY_BATCH_START
                        {

                            SendToCardReader(mercuryProtocol.MoveCursor(8, 1) + "Start Load  ");
                            Thread.Sleep(3000);
                            //M_BATCH_START_LOADING();
                            CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingLoad;
                            RaiseEvents("[Top up][Start Load]" + CRNewValue.RET_TU_ID + " Load No=" + CRNewValue.RET_LOAD_HEADER + " Comp.No=" + CRNewValue.CompartmentNo + ".");
                            SendToCardReader(ClearLastLine());
                            //DisplayToCardReader((int)LoadingStep.LoadingLoad);
                            //Thread.Sleep(5000);
                        }
                        else
                        {
                            CRNewValue.CardCode = CROldValue.CardCode;
                            DisplayToCardReader(-1);
                            DisplayToCardReader((int)_CRStepProcess.LoadingStart);
                            DisplayToCardReader(CRNewValue.RET_STEP);
                        }
                    }
                    else
                    {
                        //DisplayToCardReader(-1);
                        //DisplayToCardReader((int)LoadingStep.LoadingStart);
                        RaiseEvents(CRNewValue.RET_MSG);
                        CRNewValue.RET_STEP = (int)_CRStepProcess.NoDataFound;
                        //MessageBox.Show(mCR_NewValue.RET_MSG);
                        ////CRBayLoading_Topup();
                        ClearDataCard();
                        SendToCardReader(mercuryProtocol.ClearDisplay());
                        DisplayToCardReader(CRNewValue.RET_STEP);

                        //CRBayLoading_Topup();

                        CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                        // M_CRBAY_LOAD_CHECK_TU();
                        ClearDataCard();
                        SendToCardReader(mercuryProtocol.ClearDisplay());
                        DisplayToCardReader(CRNewValue.RET_STEP);
                    }
                }
                else if (k == "F3")
                {
                    CRNewValue.CardCode = CROldValue.CardCode;
                    CRNewValue.RET_LOAD_HEADER = CROldValue.RET_LOAD_HEADER;
                    CRNewValue.RET_CR_MSG = "Cancle Load";

                    RaiseEvents("[Top up][Canel Load]" + CRNewValue.RET_TU_ID + " Load No=" + CRNewValue.RET_LOAD_HEADER + " Comp.No=" + CRNewValue.CompartmentNo + ".");
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    DisplayToCardReader((int)_CRStepProcess.LoadingCancel);
                    CRNewValue.RET_CR_MSG = "";
                    SendToCardReader(ClearLastLine());
                    Thread.Sleep(3000);

                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                    M_CRBAY_LOAD_CHECK_TU_TOPUP();
                    ClearDataCard();
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    DisplayToCardReader(CRNewValue.RET_STEP);
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
                else if (Convert.ToInt64(CRNewValue.CardCode) > 0)
                {
                    CRNewValue.CardCode = CROldValue.CardCode;
                    CRNewValue.RET_LOAD_HEADER = CROldValue.RET_LOAD_HEADER;
                    CRNewValue.RET_CR_MSG = "Cancle Load";
                    //mCR_NewValue.CompartmentNo = 1;
                    RaiseEvents("[Top up][Canel Load]" + CRNewValue.RET_TU_ID + " Load No=" + CRNewValue.RET_LOAD_HEADER + " Comp.No=" + CRNewValue.CompartmentNo + ".");
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    DisplayToCardReader((int)_CRStepProcess.LoadingCancel);
                    CRNewValue.RET_CR_MSG = "";
                    SendToCardReader(ClearLastLine());
                    Thread.Sleep(3000);
                    //CRBayLoading_Stop();
                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                    M_CRBAY_LOAD_CHECK_TU_TOPUP();
                    ClearDataCard();
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    DisplayToCardReader(CRNewValue.RET_STEP);
                   
                    //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingStop;
                }
            Next:
                if (!thrRun)
                    break;
                //SendToCardReader(DisplayDateTime());
                Thread.Sleep(600);
            }
        }

        private void CRBayLoadingTopup_Load() //check key F1 for start ,F3 for stop
        {
            string vRecv = "", k = "";
            DisplayToCardReader((int)_CRStepProcess.LoadingLoad);
            RaiseEvents("[Top up][Loading]" + CRNewValue.RET_TU_ID + " Loading in progress" + " Load No=" + CRNewValue.RET_LOAD_HEADER + " Comp.No=" + CRNewValue.CompartmentNo + ".");

            CRNewValue.IsAlarm = false;

            while (CRNewValue.RET_STEP == (int)_CRStepProcess.LoadingLoad)
            {
                if (!thrRun)
                    break;
                //SendToCardReader(PMercuryFnc.MoveCursor(1, 1) + PMercuryFnc.MoveCursor(1, 20) + DateTime.Now);
                //SendToCardReader(DisplayDateTime());
                DisplayToCardReader((int)_CRStepProcess.DisplayDateTime);
                SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
                k = CheckKeyPress(ref CRNewValue.DataReceive);
                if (k != "")
                    RaiseEvents("Enter key=" + k);
                if (k == "F3")
                {
                    CRNewValue.CardCode = CROldValue.CardCode;
                    RaiseEvents("[Top up][Stop Load]" + CRNewValue.RET_TU_ID);
                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingStop;
                    SendToCardReader(mercuryProtocol.MoveCursor(8, 1) + "Stop Load");
                    SendToCardReader(ClearLastLine());
                    Thread.Sleep(1000);
                    //if (M_BATCH_STOP())
                    //{
                    //    M_BATCH_STOP_LOADING();
                    //    mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                    //    DisplayToCardReader((int)LoadingStep.LoadingStop);
                    //    RaiseEvents("Stop Load[" + mCR_NewValue.RET_TU_ID + "]"+ "[Load No=" + mCR_NewValue.RET_LOAD_HEADER + "][Comp.No=" + mCR_NewValue.CompartmentNo + "]"); 
                    //    Thread.Sleep(5000);
                    //}
                }
                else
                {
                    if (!M_BATCH_CHECK_ALARM())
                    {
                        CRNewValue.IsAlarm = true;
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

                        switch (CRNewValue.RET_BATCH_STATUS)
                        {
                            case 3:
                                DisplayToCardReader((int)_CRStepProcess.LoadingLoad);
                                break;
                            case 4:
                                DisplayToCardReader((int)_CRStepProcess.LoadingLoad);
                                break;
                            case 5:
                                CRNewValue.CardCode = CROldValue.CardCode;
                                CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingStop;
                                Thread.Sleep(1000);
                                break;
                            case 6://เมื่อเติม TOPUP เสร็จ
                                DisplayToCardReader((int)_CRStepProcess.LoadingComplete);
                                RaiseEvents("[Top up][Complete Load]" + CRNewValue.RET_TU_ID + " Load No=" + CRNewValue.RET_LOAD_HEADER + " Comp.No=" + CRNewValue.CompartmentNo + ".");
                                Thread.Sleep(1000);
                                SendToCardReader(mercuryProtocol.ClearDisplay());
                                //M_CRBAY_TOPUP_CHECK_STEP();
                                //if (mCR_NewValue.RET_STEP == (int)LoadingStep.LoadingNone)
                                //{
                                //    ClearDataCard();
                                //}
                                //SendToCardReader(mMercuryLib.ClearDisplay());
                                ////DisplayToCardReader(mCR_NewValue.RET_STEP);
                                CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                                ClearDataCard();
                                SendToCardReader(mercuryProtocol.ClearDisplay());
                                DisplayToCardReader(CRNewValue.RET_STEP);
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
                DisplayToCardReader((int)_CRStepProcess.LoadingStop);
                Thread.Sleep(3000);
                //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingCompartment;
                //DisplayToCardReader((int)LoadingStep.LoadingStop);

                RaiseEvents("Stop Load[" + CRNewValue.RET_TU_ID + "]" + "[Load No=" + CRNewValue.RET_LOAD_HEADER + "][Comp.No=" + CRNewValue.CompartmentNo + "]");
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
                CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingStart;
                SendToCardReader(mercuryProtocol.ClearDisplay());
                DisplayToCardReader(CRNewValue.RET_STEP);

                SendToCardReader(mercuryProtocol.ClearDisplay());
                SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
                //Thread.Sleep(300);
                //ReadFromCardReader();
            }
        }
        #endregion

        #region"LLTLB"

        private void CRBayLoading_LLTLB()
        {
            try
            {
                while (thrRun)
                {
                    P_CRBAY_CHECK_ACTIVE();
                    //Check Process Mode
                    //0=Wipe Card
                    //1=Loading
                    //2=Topup
                    CRNewValue.LoadingMode = (_LoadingMode)CRNewValue.RET_ACTIVE;
                    switch (CRNewValue.LoadingMode)
                    {
                        case _LoadingMode.TouchCard:
                            WipeCardProcess();
                            break;
                        case _LoadingMode.Loading:
                            CRNewValue.CRStepProcess = _CRStepProcess.LoadingLoad;
                            LoadingProcess();
                            break;
                        case _LoadingMode.Topup:
                            break;
                    }
                    CROldValue.LoadingMode = CRNewValue.LoadingMode;
                    Thread.Sleep(500);
                }
                CRNewValue.Connect = false;
                P_UPDATE_CARDREADER_CONNECT();
            }
            catch (Exception exp)
            { }
        }
        
        void WipeCardProcess()
        {
            P_CRBAY_CHECK_PERMISSIVE();
            {
                switch (CRNewValue.CRStepProcess)
                {
                    case _CRStepProcess.WipeCard:
                        //DisplayToCardReader((int)CRNewValue.CRStepProcess);
                        CheckWipeCard();
                        break;
                    default:
                        CRNewValue.CRStepProcess = _CRStepProcess.LoadingNone;
                        DisplayToCardReader((int)CRNewValue.CRStepProcess);
                        CRNewValue.CRStepProcess = _CRStepProcess.WipeCard;
                        break;
                }
            }
            
        }

        void CheckWipeCard()
        {
            SendToCardReader(mercuryProtocol.SendNextQueueBlock(),true);
            CRNewValue.CardCode = CheckDataCard(ref CRNewValue.DataReceive);
            //if (mCR_NewValue.CardCode == "0")
            //{
            //    CheckKeyPress(ref mCR_NewValue.DataReceive,ref mCR_NewValue.KeyPress,ref mCR_NewValue.BlockFormat) ;

            //}
            //if (!CRNewValue.Permissive)
            if (Convert.ToInt64(CRNewValue.CardCode) > 0)
            {
                //if ((Convert.ToInt64(CRNewValue.CardCode) > 0) && P_CRBAY_CHECK_TU())
                if (P_CRBAY_CHECK_PERMISSIVE().Equals(false))
                {
                    if (P_CRBAY_CHECK_TU())
                    {
                        if (CRNewValue.RET_CHECK == 0)
                        {
                            CROldValue.CardCode = CRNewValue.CardCode;
                            SendToCardReader(mercuryProtocol.ClearDisplay());
                            CRNewValue.CRStepProcess = _CRStepProcess.LoadingLoad;
                        }
                    }
                    else
                    {
                        DisplayToCardReader((int)_CRStepProcess.LoadingInvalid);
                        DisplayToCardReader((int)_CRStepProcess.LoadingNone);
                        //Thread.Sleep(3000);
                    }
                   
                }
                else
                {
                    if (Convert.ToInt64(CRNewValue.CardCode) > 0)
                    {
                        DisplayToCardReader((int)_CRStepProcess.LoadingInvalid);
                        DisplayToCardReader((int)_CRStepProcess.LoadingNone);
                        //Thread.Sleep(3000);
                    }
                }
            }
            else
            {
                DisplayToCardReader((int)CRNewValue.CRStepProcess);
            }
        }

        void LoadingProcess()
        {
            string vHeaderMsg;

            while (CRNewValue.CRStepProcess == _CRStepProcess.LoadingLoad)
            {
                switch (CRNewValue.CRStepProcess)
                {
                    case _CRStepProcess.LoadingLoad:
                        P_CRBAY_CHECK_PERMISSIVE();
                        if (CRNewValue.Permissive.Equals(false))     //permissive = true -> ready to load
                        {
                            //vHeaderMsg = CRNewValue.PermissiveMsg;

                            P_CRBAY_CHECK_PERMISSIVE_CANCEL((int)_LoadingMode.CancelLoad);
                            if (CRNewValue.NewCancelLoad)
                            {
                                if (CRNewValue.NewCancelLoad != CRNewValue.OldCancelLoad)
                                {
                                    CRNewValue.DateTimeStart = DateTime.Now;
                                    CRNewValue.OldCancelLoad = CRNewValue.NewCancelLoad;
                                    if(CRNewValue.NewCancelLoad)
                                        RaiseEvents("Stop Batch." + CRNewValue.RET_CR_MSG);
                                }
                                if ((DateTime.Now - CRNewValue.DateTimeStart).TotalSeconds < 8000)
                                {
                                    P_CRBAY_CANCEL_TU();
                                    CRNewValue.CRStepProcess = _CRStepProcess.LoadingNone;
                                }
                            }
                            else
                            {
                                CRNewValue.OldCancelLoad = false;
                            }
                            P_CRBAY_CHECK_ALARM();
                            if (CRNewValue.MeterAlarm != CROldValue.MeterAlarm)
                            {
                                CROldValue.MeterAlarm = CRNewValue.MeterAlarm;
                                if(CRNewValue.MeterAlarm)
                                    RaiseEvents("Stop Batch." + CRNewValue.RET_CR_MSG);
                            }
                        }
                        else
                        {
                            //แสดงหน้า Permissive เมือมี Premissive ขณะกำลังจะจ่ายและกำลังจ่าย
                            DisplayToCardReader((int)_CRStepProcess.Permissive);
                            Thread.Sleep(5000);
                            SendToCardReader(mercuryProtocol.ClearDisplay()); 
                            DisplayToCardReader((int)_CRStepProcess.LoadingLoad);
                            Thread.Sleep(5000);

                            vHeaderMsg = CRNewValue.PermissiveMsg;
                            if (CRNewValue.Permissive != CROldValue.Permissive)
                            {
                                CROldValue.Permissive = CRNewValue.Permissive;
                                if(CRNewValue.Permissive.Equals(false))
                                    RaiseEvents("Stop Batch." + CRNewValue.RET_CR_MSG);
                            }
                            //P_CRBAY_CHECK_ALARM();
                            CRNewValue.NewCancelLoad = false;
                            CRNewValue.OldCancelLoad = false;
                        }
                        LoadingLoad();
                        LoadHeaderData();
                        LoadLineData();
                        if (CRNewValue.CRStepProcess == _CRStepProcess.LoadingLoad)
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingLoad);
                        }
                        //LoadingWipeCard();
                        break;
                    case _CRStepProcess.LoadingInvalid:
                        Thread.Sleep(3000);
                        CRNewValue.CRStepProcess = _CRStepProcess.LoadingLoad;
                        break;
                    default:
                        break;
                }
                Thread.Sleep(500);
                P_CRBAY_CHECK_ACTIVE();
                CRNewValue.LoadingMode = (_LoadingMode)CRNewValue.RET_ACTIVE;
                if (CRNewValue.LoadingMode != _LoadingMode.Loading)
                { 
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    break;
                }
                if (!thrRun)
                    break;
            }
        }

        void LoadingLoad()
        {
           //check permissive display 
            //xxDA -> key press
            //xxDD -> fuction key press
            //xxDC -> card data
            int vCompartment;
            SendToCardReader(mercuryProtocol.SendNextQueueBlock(), true);
            CRNewValue.CardCode = CheckDataCard(ref CRNewValue.DataReceive);
            if (CRNewValue.CardCode == "0")
            {
                CheckKeyPress(ref CRNewValue.DataReceive, ref CRNewValue.KeyPress, ref CRNewValue.BlockFormat);

            }
            switch (CRNewValue.BlockFormat)
            {
                case "A":
                    break;
                case "C":
                    if (P_CRBAY_CHECK_TU())
                    {
                        if (CRNewValue.RET_CHECK == 0)
                        {
                            SendToCardReader(mercuryProtocol.ClearDisplay());
                            DisplayToCardReader((int)_CRStepProcess.DisplayNoLoading);
                            CRNewValue.CRStepProcess = _CRStepProcess.WipeCard;
                            Thread.Sleep(3000);
                            //CRNewValue.CRStepProcess = _CRStepProcess.LoadingNone;
                            CRNewValue.DataReceive = "";
                            CRNewValue.Permissive = false;
                            CRNewValue.PermissiveMsg = "";
                            CRNewValue.CompartmentNo = 0;
                        }
                        else
                        {
                            SendToCardReader(mercuryProtocol.ClearDisplay());
                            DisplayToCardReader((int)_CRStepProcess.DisplayNoLoading);
                            CRNewValue.CRStepProcess = _CRStepProcess.LoadingInvalid;
                            CRNewValue.DataReceive = "";
                        }
                    }
                    break;
                case "D":
                    if (CRNewValue.Permissive.Equals(true))
                        break;
                    if (CRNewValue.KeyPress == "F1")
                    {
                        vCompartment = (CRNewValue.PageNum * 5) + 1;
                        CRNewValue.CompartmentNo = vCompartment;
                        RaiseEvents("Start Batch Compartment = " + vCompartment);
                        P_CRBAY_COMMAND_START(vCompartment);
                        RaiseEvents(CRNewValue.RET_MSG);
                        if (CRNewValue.RET_CR_MSG != "")
                            RaiseEvents(CRNewValue.RET_CR_MSG);
                        if (CRNewValue.RET_CHECK == -1)
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingInvalid);
                            Thread.Sleep(3000);
                        }
                        else
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingStart);
                            Thread.Sleep(3000);
                        }
                    }
                    if (CRNewValue.KeyPress == "F2")
                    {
                        vCompartment = (CRNewValue.PageNum * 5) + 2;
                        CRNewValue.CompartmentNo = vCompartment;
                        RaiseEvents("Start Batch Compartment = " + vCompartment);
                        P_CRBAY_COMMAND_START(vCompartment);
                        RaiseEvents(CRNewValue.RET_MSG);
                        if (CRNewValue.RET_CR_MSG != "")
                            RaiseEvents(CRNewValue.RET_CR_MSG);
                        if (CRNewValue.RET_CHECK == -1)
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingInvalid);
                            Thread.Sleep(3000);
                        }
                        else
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingStart);
                            Thread.Sleep(3000);
                        }
                    }
                    if (CRNewValue.KeyPress == "F3")
                    {
                        vCompartment = (CRNewValue.PageNum * 5) + 3;
                        CRNewValue.CompartmentNo = vCompartment;
                        RaiseEvents("Start Batch Compartment = " + vCompartment);
                        P_CRBAY_COMMAND_START(vCompartment);
                        RaiseEvents(CRNewValue.RET_MSG);
                        if (CRNewValue.RET_CR_MSG != "")
                            RaiseEvents(CRNewValue.RET_CR_MSG);
                        if (CRNewValue.RET_CHECK == -1)
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingInvalid);
                            Thread.Sleep(3000);
                        }
                        else
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingStart);
                            Thread.Sleep(3000);
                        }
                    }
                    if (CRNewValue.KeyPress == "F4")
                    {
                        vCompartment = (CRNewValue.PageNum * 5) + 4;
                        CRNewValue.CompartmentNo = vCompartment;
                        RaiseEvents("Start Batch Compartment = " + vCompartment);
                        P_CRBAY_COMMAND_START(vCompartment);
                        RaiseEvents(CRNewValue.RET_MSG);
                        if (CRNewValue.RET_CR_MSG != "")
                            RaiseEvents(CRNewValue.RET_CR_MSG);
                        if (CRNewValue.RET_CHECK == -1)
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingInvalid);
                            Thread.Sleep(3000);
                        }
                        else
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingStart);
                            Thread.Sleep(3000);
                        }
                    }
                    if (CRNewValue.KeyPress == "F5")
                    {
                        vCompartment = (CRNewValue.PageNum * 5) + 5;
                        CRNewValue.CompartmentNo = vCompartment;
                        RaiseEvents("Start Batch Compartment = " + vCompartment);
                        P_CRBAY_COMMAND_START(vCompartment);
                        RaiseEvents(CRNewValue.RET_MSG);
                        if (CRNewValue.RET_CR_MSG != "")
                            RaiseEvents(CRNewValue.RET_CR_MSG);
                        if (CRNewValue.RET_CHECK == -1)
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingInvalid);
                            Thread.Sleep(3000);
                        }
                        else
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingStart);
                            Thread.Sleep(3000);
                        }
                    }
                    if (CRNewValue.KeyPress == "F6")
                    {
                        SelectPageDisplay();
                        CRNewValue.DataReceive = "";
                        
                    }
                    if ((CRNewValue.KeyPress == "F7")) //|| (CRNewValue.KeyPress == "Stop"))
                    {
                        RaiseEvents("Stop Batch");  //sent text to GUI
                        P_CRBAY_COMMAND_STOP();     //commmand stop to database
                        RaiseEvents(CRNewValue.RET_MSG);    //read msg command stop OK/NOK
                        if (CRNewValue.RET_CR_MSG != "")
                            RaiseEvents(CRNewValue.RET_CR_MSG);
                        if (CRNewValue.RET_CHECK == -1)
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingInvalid);
                            Thread.Sleep(3000);
                        }
                        else
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingStop);
                            Thread.Sleep(3000);
                        }
                    }
                    if ((CRNewValue.KeyPress == "F8"))// || (CRNewValue.KeyPress == "Complete"))
                    {
                        RaiseEvents("Manual Complete Batch at Gantry");
                        P_CRBAY_COMMAND_END();
                        RaiseEvents(CRNewValue.RET_MSG);
                        if (CRNewValue.RET_CHECK == -1)
                        {
                            DisplayToCardReader((int)_CRStepProcess.LoadingInvalid);
                            Thread.Sleep(3000);
                            CRNewValue.CRStepProcess = _CRStepProcess.LoadingInvalid;
                        }
                        //will be display when completed.
                    }
                    break;
                default:
                    break;
            }
        }

        void LoadHeaderData()
        {
            string strSQL;
            DataSet ds = new DataSet();
            DataTable dt;
            try
            {
                strSQL = "select BAY_NO,BAY_NAME,LOAD_HEADER_NO,SHIPMENT_NO,SHIPMENT_REF,TU_CARD_NO,TU_CARD_NO1" +
                    " from load.view_scan_cr_bay " +
                    " where BAY_NO=" + CRNewValue.BayNo;
                if (fMain.OraDb.OpenDyns(strSQL, "TableName", ref ds))
                {
                    dt = ds.Tables["TableName"];
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        CRNewValue.LoadingInfo_A.LoadHeaderNo = dt.Rows[i]["LOAD_HEADER_NO"].ToString();
                        CRNewValue.LoadingInfo_A.ShipmentNo = dt.Rows[i]["SHIPMENT_NO"].ToString();
                        CRNewValue.LoadingInfo_A.TUCardNo = dt.Rows[i]["TU_CARD_NO"].ToString();
                        CRNewValue.LoadingInfo_A.TUCardNo1 = dt.Rows[i]["TU_CARD_NO1"].ToString();
                    }
                }
            }
            catch (Exception exp)
            { }
            ds = null;
            dt = null;
        }

        void LoadLineData()
        {
            string strSQL,vUnit;
            DataSet ds = new DataSet();
            DataTable dt;
            int vIndex,vCompartment;
            try
            {
                for (vIndex = 0; vIndex < CRNewValue.MaxLoad; vIndex++)
                {
                    CRNewValue.LoadingDetail[vIndex].Enable = false;
                    CRNewValue.LoadingDetail[vIndex].MeterNo = "**";
                    CRNewValue.LoadingDetail[vIndex].Product = "";
                    CRNewValue.LoadingDetail[vIndex].Advice = "";
                    CRNewValue.LoadingDetail[vIndex].Loaded = "";
                    CRNewValue.LoadingDetail[vIndex].Unit = "";
                    CRNewValue.LoadingDetail[vIndex].Additive = "";
                    CRNewValue.LoadingDetail[vIndex].Message="";
                }
                strSQL = "select LOAD_HEADER_NO,DO_NO,COMPARTMENT_NO,METER_NO,BASE_PRODUCT_ID,ADVICE,PRESET,LOADED_GROSS,UNIT,BATCH_STATUS" +
                    " from load.view_scan_loading_line " +
                    " where LOAD_HEADER_NO=" + CRNewValue.LoadingInfo_A.LoadHeaderNo +
                    " order by COMPARTMENT_NO";
                if (fMain.OraDb.OpenDyns(strSQL, "TableName", ref ds))
                {
                    dt = ds.Tables["TableName"];
                    for (vIndex = 0; vIndex < dt.Rows.Count; vIndex++)
                    {
                        vCompartment = Convert.ToInt16(dt.Rows[vIndex]["COMPARTMENT_NO"])-1;
                        CRNewValue.LoadingDetail[vCompartment].Enable = true;
                        CRNewValue.LoadingDetail[vCompartment].MeterNo = dt.Rows[vIndex]["METER_NO"].ToString();
                        CRNewValue.LoadingDetail[vCompartment].Product = dt.Rows[vIndex]["BASE_PRODUCT_ID"].ToString();
                        CRNewValue.LoadingDetail[vCompartment].Advice = dt.Rows[vIndex]["ADVICE"].ToString();
                        CRNewValue.LoadingDetail[vCompartment].Loaded = dt.Rows[vIndex]["LOADED_GROSS"].ToString();
                        vUnit = dt.Rows[vIndex]["UNIT"].ToString();
                        if(vUnit.ToUpper()=="LITRE")
                            CRNewValue.LoadingDetail[vCompartment].Unit = "L";
                        else
                            CRNewValue.LoadingDetail[vCompartment].Unit = vUnit;
                        CRNewValue.LoadingDetail[vCompartment].Message = dt.Rows[vIndex]["BATCH_STATUS"].ToString();
                    }
                }
            }
            catch (Exception exp)
            { }
            ds = null;
            dt = null;
        }

        string LoadingLineFormat(string pSelectComp,string pCompartment,string pMeterNo,string pProduct,string pAdvice
                                ,string pLoaded,string pUnit,string pMessage)
        {
            string vMsg = pSelectComp.PadLeft(2, ' ') + pCompartment.PadLeft(3, ' ') + pMeterNo.PadLeft(3, ' ') + pProduct.PadLeft(9, ' ') + pAdvice.PadLeft(6, ' ') +
                    pLoaded.PadLeft(6, ' ') + pUnit.PadLeft(2, ' ') + pMessage.PadLeft(9, ' ');
            return vMsg;
        }

        void DisplayLoadingLoad()
        {
            string vMsg="";
            int vIndex;
            try
            {
                if (CRNewValue.Permissive.Equals(true))
                {
                    vMsg = mercuryProtocol.MoveCursor(1, 1) + "Permissive".PadRight(30, ' ');
                }
                else
                {
                    if (CRNewValue.MeterAlarm)
                    {
                        vMsg += mercuryProtocol.MoveCursor(1, 1) + CRNewValue.AlarmMsg.PadRight(40,' ');
                    }
                    else
                        vMsg += mercuryProtocol.MoveCursor(1, 1) + "05 LOADING".PadRight(40, ' ');
                }
                SendToCardReader(vMsg.ToUpper());
                vMsg = mercuryProtocol.MoveCursor(2, 1) + LoadingLineFormat("#", "CP", "MT", "PRODUCT", "  P", "LOAD", "U", "MSG");
                SendToCardReader(vMsg.ToUpper());
                for (int i = 0; i <= 4; i++)
                {
                    vIndex = i + (CRNewValue.PageNum * 5);
                    vMsg = mercuryProtocol.MoveCursor(i + 3, 1) +
                        LoadingLineFormat((i + 1).ToString(), (vIndex + 1).ToString(), CRNewValue.LoadingDetail[vIndex].MeterNo, CRNewValue.LoadingDetail[vIndex].Product
                                            , CRNewValue.LoadingDetail[vIndex].Advice, CRNewValue.LoadingDetail[vIndex].Loaded
                                            , CRNewValue.LoadingDetail[vIndex].Unit, CRNewValue.LoadingDetail[vIndex].Message);
                    SendToCardReader(vMsg.ToUpper());
                }
            }
            catch (Exception exp)
            { }
        }

        void SelectPageDisplay()
        {
            CRNewValue.PageNum++;
            if (CRNewValue.PageNum > 2)
                CRNewValue.PageNum = 0;
        }
        #endregion
        #endregion

        #region Change process or comport
        void ChangeProcess()
        {
            Thread.Sleep(3000);
            bool vRet = !CRNewValue.Connect;
            var vDiff = (DateTime.Now - datetimeResponse).TotalMinutes;
            if ((vRet) && (vDiff >= 1))
            {
                datetimeResponse = DateTime.Now;
                //thrShutdown = true;
                ChangeComport();
            }
        }

        void ChangeComport()
        {
            int vControlComport;
            if (processId == 1)
                vControlComport = 0;
            else
                vControlComport = 1;

            string strSQL = "begin steqi.P_CHANGE_COMPORT_CARDREADER(" +
                             CRNewValue.ID + "," + vControlComport.ToString() +
                             ");end;";

            fMain.OraDb.ExecuteSQL(strSQL);

            RaiseEvents("Communication changed.");

            crPort.CloseComPort();
            crPort.StopThread();
            processId += 1;
            if (processId > 1)
                processId = 0;
            crPort = CRComport[processId];
            crPort.StartThread();
        }

        public bool IsThreadAlive
        {
            get
            {
                try
                {
                    return thrMain.IsAlive;
                }
                catch (Exception exp)
                { return false; }
            }
        }
        #endregion

        #region Diagnostic
        public string DiagnosticSend()
        {
            string vRet = crPort.DiagMsgSend();
            fMain.LogFile.WriteComportLog(crPort.ComportNo, vRet);
            return vRet;
        }

        public string DiagnosticReceive()
        {
            string vRet = crPort.DiagMsgReceive();
            fMain.LogFile.WriteComportLog(crPort.ComportNo, vRet);
            return vRet;
        }
        #endregion
        
        private void CheckResponse(bool pResponse)
        {
            if (pResponse)
            {
                countResponse = 0;
                datetimeResponse = DateTime.Now;
                if (chkResponse != pResponse)
                {
                    CRNewValue.Connect = true;
                    P_UPDATE_CARDREADER_CONNECT();
                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    DisplayToCardReader((int)CRNewValue.CRStepProcess);
                    //if(mThread.ThreadState != ThreadState.Aborted)
                    //    mThread.Abort();
                    //StartThread();
                    chkResponse = pResponse;
                }
            }
            else
            {
                countResponse += 1;
                if (countResponse <= 5)
                    return;

                DateTime vDateTime = DateTime.Now;

                var vDiff = (vDateTime - datetimeResponse).TotalSeconds;

                if ((vDiff > 30) && (chkResponse = true))
                {
                    datetimeResponse = DateTime.Now;
                    CRNewValue.Connect = false;
                    chkResponse = false;
                    P_UPDATE_CARDREADER_CONNECT();
                    CRNewValue.RET_STEP = (int)_CRStepProcess.LoadingNone;
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    DisplayToCardReader(CRNewValue.RET_STEP);
                    ChangeComport();
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
            Encoding objEncoding = Encoding.GetEncoding("Windows-874");

            byte[] b = objEncoding.GetBytes(pMsg);
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
                        a = objEncoding.GetBytes(pCR_address.ToString());
                        vMsg[i + 1] = a[0];
                        a = objEncoding.GetBytes("0");
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
                PositionSTX = pRecv.IndexOf(char.ConvertFromUtf32(2));
                PositionETX = pRecv.IndexOf(char.ConvertFromUtf32(3));
                vCheckPos = pRecv.IndexOf(char.ConvertFromUtf32(2) + CRNewValue.Address.ToString("00D"));
                if ((vCheckPos >= 0) && (PositionETX > PositionSTX))
                {
                    //DATA_Position = CheckPos;
                    CRNewValue.DataReceive = pRecv.Substring(PositionSTX, PositionETX - vCheckPos + 1);
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
                PositionSTX = pRecv.IndexOf(char.ConvertFromUtf32(2));
                PositionETX = pRecv.IndexOf(char.ConvertFromUtf32(3));
                vCheckPos = pRecv.IndexOf(char.ConvertFromUtf32(2) + CRNewValue.Address.ToString("00D"));
                if ((vCheckPos >= 0) && (PositionETX > PositionSTX))
                {
                    //DATA_Position = CheckPos;
                    CRNewValue.DataReceive = pRecv.Substring(PositionSTX, PositionETX + 1);
                    vCheck = true;
                }
            }
            return vCheck;
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
                string addr = CRNewValue.Address.ToString("00D");
                int vIndex = pRecv.IndexOf(char.ConvertFromUtf32(2) + addr);
                if ((vIndex > -1) && pRecv.Length > 5)
                {
                    //01D\0Y , 01DC005748EB\0W -> touch card
                    //01DAR\0F -> press R
                    //01DAA\0W -> press A
                    //01DDOP\0[ -> press F1
                    if ((PositionSTX < 0) || (PositionETX < PositionSTX))
                    {
                        PositionSTX = pRecv.IndexOf(char.ConvertFromUtf32(2));
                        PositionETX = pRecv.IndexOf(char.ConvertFromUtf32(3));
                        if (PositionETX < PositionSTX)
                        {
                            PositionETX = pRecv.IndexOf(char.ConvertFromUtf32(3), PositionSTX);
                        }
                        if ((PositionSTX == -1) || (PositionETX == -1))
                        {
                            return "0";
                        }
                    }
                    //temp = pRecv.Substring(mSTX_Position, mETX_Position - mSTX_Position + 1);
                    vIndex = pRecv.IndexOf("DC");

                    if ((vIndex == 3) || (vIndex - PositionSTX == 3))
                    {
                        temp = pRecv.Substring(vIndex + 2, pRecv.Length - 2 - vIndex).Trim();
                        if ((temp.IndexOf("\0") > -1) && pRecv.Length > 10)
                        {
                            temp = temp.Substring(0, temp.IndexOf("\0"));
                            //CardCode = int.Parse(temp, System.Globalization.NumberStyles.HexNumber).ToString();       //for prox. format
                            CardCode = int.Parse(temp.Substring(temp.Length - 4, 4), System.Globalization.NumberStyles.HexNumber).ToString();       //for hid format
                            RaiseEvents("Card Code = " + CardCode);
                            CRNewValue.BlockFormat = "C";
                        }
                        else
                        {
                            CardCode = "0";
                        }

                    }
                    else
                    {
                        CheckKeyPress(ref pRecv);
                    }
                }
            }
            //else
            //    crPort.ReceiveData();

            return CardCode;
        }

        private string CheckEnterCardCode(ref string pData)
        {
            CRNewValue.TimeOut=false;
            string k="",vRecv="";
            k = CheckKeyPress(ref pData);
            if (k == "F6")
            {
                CheckIsKeypad = true;
                SendToCardReader(mercuryProtocol.DeleteAllStoreMessage());
                Thread.Sleep(300);
                SendToCardReader(mercuryProtocol.ClearDisplay());
                CROldValue.RET_STEP = CRNewValue.RET_STEP;
                CRNewValue.RET_STEP = (int)_CRStepProcess.EnterCard;
                DisplayToCardReader(CRNewValue.RET_STEP);
                RaiseEvents("Enter key=" + k);
                RaiseEvents("Enter Card Code");
                DisplayToCardReader(CRNewValue.RET_STEP);
                //Thread.Sleep(500);
                CRNewValue.DateTimeStart = DateTime.Now;
               
                //while ((DateTime.Now - vTimeActive).TotalSeconds < 30)
                //mCR_OldValue.RET_STEP = mCR_NewValue.RET_STEP;
                while(!CRNewValue.TimeOut)
                {
                    //SendToCardReader(mMercuryLib.MoveCursor(1, 20) + DateTime.Now);
                    //Thread.Sleep(500);
                    SendToCardReader(DisplayDateTime());
                    SendToCardReader(mercuryProtocol.SendNextQueueBlock(),true);
                    //Thread.Sleep(300);
                    //ReadFromCardReader();
                    k = CheckKeyPress(ref CRNewValue.DataReceive);
                    if (k == "F7")
                    {
                        CheckIsKeypad = false;
                        RaiseEvents("Enter key=" + k);
                        RaiseEvents("Cancel Enter Card Code");
                        //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                        CRNewValue.RET_STEP = CROldValue.RET_STEP;
                        SendToCardReader(mercuryProtocol.ClearDisplay());
                        SendToCardReader(ClearLastLine());
                        //DisplayToCardReader(mCR_NewValue.RET_STEP);
                        break;
                    }
                    else
                    {
                        if (k != "")
                        {
                            RaiseEvents("Enter key=" + k);
                            CRNewValue.RET_STEP = CROldValue.RET_STEP;
                            SendToCardReader(mercuryProtocol.ClearDisplay());
                            SendToCardReader(ClearLastLine());
                            if(CRNewValue.RET_STEP != (int)_CRStepProcess.LoadingNone)
                                DisplayToCardReader(CRNewValue.RET_STEP);
                            return k;
                        }
                    }

                    if (CRNewValue.TimeOut = (DateTime.Now - CRNewValue.DateTimeStart).TotalSeconds < 30)
                        CRNewValue.TimeOut = false;
                    else
                        CRNewValue.TimeOut = true;

                    //SendToCardReader(DisplayDateTime());
                    if (!thrRun)
                        break;
                    Thread.Sleep(600);
                }
                if (CRNewValue.TimeOut)
                {
                    RaiseEvents("Cancel Enter Card Code[Time out]");
                    CRNewValue.TimeOut = false;
                    CRNewValue.RET_STEP = CROldValue.RET_STEP;
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    SendToCardReader(ClearLastLine());
                    DisplayToCardReader(CRNewValue.RET_STEP);
                    SendToCardReader(ClearLastLine());
                    //mCR_NewValue.RET_STEP = (int)LoadingStep.LoadingNone;
                }
            }
            else
            {
                if (k != "")
                {
                    RaiseEvents("Enter key=" + k);
                    CRNewValue.RET_STEP = CROldValue.RET_STEP;
                    SendToCardReader(ClearLastLine());
                    DisplayToCardReader(CRNewValue.RET_STEP);
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
                    string addr = CRNewValue.Address.ToString("00D");
                    int vIndex = pData.IndexOf(char.ConvertFromUtf32(2) + addr);
                    if ((vIndex > -1) && pData.Length > 5)
                    {
                        //01D\0Y -> touch card
                        //01DAR\0F -> press R
                        //01DAA\0W -> press A
                        //01DDOP\0[ -> press F1
                        vKeyPress = mercuryProtocol.CheckKeyPress(CRNewValue.Address, pData);
                        if (vKeyPress != "")
                        {
                            vIndex = pData.IndexOf("DA");

                            if ((vIndex == 3) || (vIndex - PositionSTX == 3))
                            {
                                CRNewValue.BlockFormat = "A";
                            }

                            vIndex = pData.IndexOf("DD");

                            if ((vIndex == 3) || (vIndex - PositionSTX == 3))
                            {
                                CRNewValue.BlockFormat = "D";
                            }
                            //RaiseEvents("Enter key=" + vKeyPress);
                        }
                        else
                        {
                            CRNewValue.BlockFormat = ""; 
                        }
                    }
                }
            }
            catch (Exception exp)
            {
            }

            return vKeyPress;
        }

        private void CheckKeyPress(ref string pData,ref string pKeyPress,ref string pBlockFormat)
        {
            try
            {
                if (pData != null)
                {
                    string addr = CRNewValue.Address.ToString("00D");
                    int vIndex = pData.IndexOf(char.ConvertFromUtf32(2) + addr);
                    if ((vIndex > -1) && pData.Length > 5)
                    {
                        //01D\0Y -> touch card
                        //01DAR\0F -> press R
                        //01DAA\0W -> press A
                        //01DDOP\0[ -> press F1
                        pKeyPress = mercuryProtocol.CheckKeyPress(CRNewValue.Address, pData);
                        if (pKeyPress != "")
                        {
                            vIndex = pData.IndexOf("DA");

                            if ((vIndex == 3) || (vIndex - PositionSTX == 3))
                            {
                                pBlockFormat = "A";
                            }

                            vIndex = pData.IndexOf("DD");

                            if ((vIndex == 3) || (vIndex - PositionSTX == 3))
                            {
                                pBlockFormat = "D";
                            }
                            RaiseEvents("Enter key=" + pKeyPress);
                        }
                        else
                        {
                            pBlockFormat = "";
                        }
                    }
                }
            }
            catch (Exception exp)
            {
            }
        }

        private bool SendToCardReader(string pMsg,bool pRead)
        {
            lock (thrLock)
            {
                try
                {
                    //if (!crPort.IsOpen())
                    //    return false;
                    byte[] b = BuildMsg((byte)CRNewValue.Address, pMsg);

                    crPort.SendData(b);
                    CRNewValue.DataSend = Encoding.ASCII.GetString(b);

                    Thread.Sleep(300);
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
            lock (thrLock)
            {
                try
                {
                    //if (!crPort.IsOpen())
                    //    return false;
                    Encoding objEncoding = Encoding.GetEncoding("Windows-874");
                    byte[] b = BuildMsg((byte)CRNewValue.Address, pMsg);

                    crPort.SendData(b);
                    CRNewValue.DataSend = objEncoding.GetString(b);

                    return true;
                }
                catch (Exception exp)
                { return false; }
            }
        }

        private bool ReadFromCardReader()
        {
            lock (thrLock)
            {
                bool vCheck = false;
                try
                {
                    if (!crPort.IsOpen())
                    {
                        CheckResponse(false);
                        //return false;
                    }
                    //string s = PPort.ReceiveData();
                    string s = crPort.ReceiveData();
                    //mCR_NewValue.DataReceive = mPort.ReceiveData();
                    //string s = mCR_NewValue.DataReceive;
                    if ((s != "") && (s != null))
                    {
                        if (CheckBlockRecv(s))
                        {
                            //mCR_NewValue.DataReceive = s;
                            CRNewValue.Connect = true;
                            CheckResponse(true);
                            //pRecv = s;
                            vCheck = true;
                        }
                        //return true;
                    }
                    else
                    {
                        CRNewValue.DataReceive = " ";
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
        private void SetDisplayMsgThai()
        {
            SendToCardReader(mercuryProtocol.SetLargeCharacterSize());
            SendToCardReader(mercuryProtocol.SetFontThai());
        }
        private void SetDisplayMsgEnglish()
        {
            SendToCardReader(mercuryProtocol.SetStandardCharacterSize());
            SendToCardReader(mercuryProtocol.SetFontEnglish());
        }
        private void DisplayToCardReader(int pDisplay)  //SAKCTAS
        {
            string vMsg = "";
            string s = "";
            //SendToCardReader(mMercuryLib.MoveCursor(8, 1) + s.PadRight(40, ' '));  //clear last line
            //M_CRBAY_GET_METER_NAME();
            
            switch (pDisplay)
            {
                case (int)_CRStepProcess.NoDataFound:
                    vMsg += mercuryProtocol.ClearDisplay();
                    vMsg += mercuryProtocol.MoveCursor(1, 1) + "เกิดข้อผิดพลาด";
                    vMsg += mercuryProtocol.MoveCursor(4, 1) + " Card: " + CRNewValue.CardCode;
                    vMsg += mercuryProtocol.MoveCursor(5, 1) + " " + CRNewValue.RET_CR_MSG;
                    vMsg += mercuryProtocol.MoveCursor(6, 1) + " " + CRNewValue.RET_MSG_BATCH1 + " " + CRNewValue.RET_MSG_BATCH2;
                    SendToCardReader(vMsg.ToUpper());
                    Thread.Sleep(5000);
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    Thread.Sleep(500);
                    vMsg = "";
                    SendToCardReader(mercuryProtocol.DeleteAllStoreMessage());
                    SendToCardReader(mercuryProtocol.MoveCursor(3, 1) + s.PadRight(20, ' '));  //clear last line
                    break;
                case (int)_CRStepProcess.SystemOffLine:
                    vMsg += mercuryProtocol.ClearDisplay();
                    vMsg += mercuryProtocol.MoveCursor(1, 1) + "99  ระบบหยุดทำงาน    ";
                    SendToCardReader(mercuryProtocol.SendNextQueueBlock(),false);
                    SendToCardReader(ClearLastLine());
                    vMsg = "";
                    break;
                case (int)_CRStepProcess.LoadingNone:
                    SetDisplayMsgThai();
                    //vMsg = mercuryProtocol.MoveCursor(1, 1) + "กรุณารอสักครู่.".PadRight(20,' ');
                    //SendToCardReader(vMsg.ToUpper());
                    vMsg = mercuryProtocol.MoveCursor(2, 1) + "     ***กรุณารอสักครู่***     ";
                    SendToCardReader(vMsg.ToUpper());
                    //SendToCardReader(DisplayDateTime());
                    //vMsg = mercuryProtocol.MoveCursor(3, 1) + "* ไม่มีการจ่าย * ".PadRight(20,' ');
                    //SendToCardReader(vMsg.ToUpper());
                    vMsg = "";
                    break;
                case (int)_CRStepProcess.DisplayDateTime:
                    CRNewValue.TimeSend = DateTime.Now;
                    if ((CRNewValue.TimeSend - CROldValue.TimeSend).TotalSeconds > 1)
                    {
                        SendToCardReader(DisplayDateTime());
                        CROldValue.TimeSend = CRNewValue.TimeSend;
                    }
                    break;
                case (int)_CRStepProcess.WipeCard:
                    SetDisplayMsgThai();
                    if (CRNewValue.Permissive)
                        vMsg = mercuryProtocol.MoveCursor(1, 1) + CRNewValue.PermissiveMsg.PadRight(32,' ');
                    else
                        vMsg = mercuryProtocol.MoveCursor(1, 1) + "กรุณาแตะบัตร".PadRight(32,' ');
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = mercuryProtocol.MoveCursor(3, 1) + "* ไม่มีการจ่าย * ".PadRight(32,' ');
                    SendToCardReader(vMsg.ToUpper());
                    SendToCardReader(DisplayDateTime());
                    vMsg = "";
                    break;
                case (int)_CRStepProcess.LoadingLoad:
                    SetDisplayMsgEnglish();
                    DisplayLoadingLoad();
                    break;
                case (int)_CRStepProcess.DisplayNoLoading:
                    SetDisplayMsgThai();
                    vMsg += mercuryProtocol.MoveCursor(1, 1) + CRNewValue.RET_CR_MSG.PadRight(30,' ');
                    SendToCardReader(vMsg.ToUpper());
                    SendToCardReader(DisplayDateTime());
                    vMsg = mercuryProtocol.MoveCursor(3, 1) + "* ไม่มีการจ่าย * ".PadRight(30,' ');
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = "";
                    break;
                case (int)_CRStepProcess.LoadingInvalid:
                    SetDisplayMsgThai();
                    //vMsg = mercuryProtocol.MoveCursor(1, 1) + CRNewValue.RET_CR_MSG.PadRight(20,' ');
                    vMsg = mercuryProtocol.ClearDisplay();
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = mercuryProtocol.MoveCursor(1, 1) + "เกิดข้อผิดพลาด";
                    SendToCardReader(vMsg.ToUpper());
                    vMsg += mercuryProtocol.MoveCursor(2, 1) + " " + CRNewValue.RET_CR_MSG;
                    SendToCardReader(vMsg.ToUpper());
                    Thread.Sleep(3000);
                    SendToCardReader(mercuryProtocol.ClearDisplay());
                    //Thread.Sleep(500);
                    vMsg = "";
                    break;
                case (int)_CRStepProcess.Permissive:
                    SetDisplayMsgThai();
                    vMsg = mercuryProtocol.ClearDisplay();
                    SendToCardReader(vMsg.ToUpper());
                    vMsg = mercuryProtocol.MoveCursor(1, 1) + "การเติมไม่ถูกต้อง";
                    SendToCardReader(vMsg.ToUpper());
                    vMsg += mercuryProtocol.MoveCursor(2, 1) + " " + CRNewValue.PermissiveMsg.PadRight(30, ' ');
                    SendToCardReader(vMsg.ToUpper());
                    break;
                case (int)_CRStepProcess.LoadingStart:
                    SetDisplayMsgEnglish();
                    vMsg = mercuryProtocol.MoveCursor(1, 1) + CRNewValue.RET_CR_MSG.PadRight(30,' ');
                    SendToCardReader(vMsg.ToUpper());
                    break;
                case (int)_CRStepProcess.LoadingStop:
                    SetDisplayMsgEnglish();
                    vMsg = mercuryProtocol.MoveCursor(1, 1) + CRNewValue.RET_CR_MSG.PadRight(30, ' ');
                    SendToCardReader(vMsg.ToUpper());
                    break;
                default:
                    break;
            }
            if (vMsg.Length > 0)
            {
                //SetDisplayMsgEnglish();
                //SendToCardReader(vMsg.ToUpper());
            }
        }

        private void ClearDataCard()
        {
            CRNewValue.CompartmentNo = 0;
            CRNewValue.RET_BATCH_STATUS = 0;
            CRNewValue.RET_CHECK = -1;
            CRNewValue.RET_COMPARTMENT_LIST = "";
            CRNewValue.RET_CR_MSG = "";
            CRNewValue.RET_DENSITY30C = 0;
            CRNewValue.RET_FLOWRATE = "0";
            CRNewValue.RET_LOAD_COUNT = 0;
            CRNewValue.RET_LOAD_HEADER = 0;
            CRNewValue.RET_LOAD_LINE = 0;
            CRNewValue.RET_LOAD_STATUS = 0;
            CRNewValue.RET_LOADED_MASS = "0";
            CRNewValue.RET_METER_NO = "";
            CRNewValue.RET_MSG = "";
            CRNewValue.RET_MSG_BATCH1 = "";
            CRNewValue.RET_MSG_BATCH2 = "";
            CRNewValue.RET_PRESET = 0;
            CRNewValue.RET_RECIPES_NO = 0;
            CRNewValue.RET_TOT_COMPARTMENT = 0;
            CRNewValue.RET_VCF30 = 0;
            CRNewValue.RET_TU_ID = "";
            CRNewValue.RET_SEAL_USE = "";
            CRNewValue.RET_SEAL_NUMBER = "";
            CRNewValue.PermissiveMsg = "";
            CRNewValue.Permissive = false;
            CROldValue = CRNewValue;
        }

        private string DisplayDateTime()
        {
            //Thread.Sleep(50);
            return mercuryProtocol.MoveCursor(2, 1) + "BAY-" + CRNewValue.BayName + " " + DateTime.Now;
        }
       
        private string DisplayDateTime(int pRow,int pCol)
        {
            //Thread.Sleep(50);
            return mercuryProtocol.MoveCursor(pRow, pCol) +DateTime.Now;
        }
        
        private void DisplayMeterAlarm()
        {
            string[] s; 
            string vMsg = "";
            //M_CRBAY_GET_METER_NAME();
            RaiseEvents("[" + CRNewValue.RET_METER_NAME + "] " + CRNewValue.RET_MSG);
            s=CRNewValue.RET_MSG.Split('-');
            //vMsg += mMercuryLib.ClearDisplay();
            //vMsg += mMercuryLib.MoveCursor(1, 1) + "99                                       ";
            //vMsg += mMercuryLib.MoveCursor(3, 1) + "            [" + mCR_NewValue.RET_METER_NO + "]Meter Alarm" ;
            //vMsg += mMercuryLib.MoveCursor(4, 1) + " " + s[1];
            SendToCardReader(mercuryProtocol.MoveCursor(8, 1) + CRNewValue.RET_MSG);  
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
            return mercuryProtocol.MoveCursor(8, 1) + s.PadRight(38,' ') + mercuryProtocol.MoveCursor(8,1);
        }

        private bool CheckIsKeypad
        {
            get { return CRNewValue.IsKeypad; }
            set { CRNewValue.IsKeypad = value; }
        }

        public void InitialCardReader(string pCardReaderID)
        {
            try
            {
                string strSQL = "select t.card_reader_name,t.card_reader_address,t.bay_no,t.bay_name " +
                                " from tas.VIEW_CR_BAY t" +
                                " where t.card_reader_id=" + pCardReaderID;

                DataSet ds = new DataSet();
                DataTable dt;
                string vMsg = "";

                if (fMain.OraDb.OpenDyns(strSQL, "TableName", ref ds))
                {
                    dt = ds.Tables["TableName"];
                    if (dt.Rows.Count > 0)
                    {
                        CRNewValue.Address = Convert.ToInt32(dt.Rows[0]["card_reader_address"]);
                        CRNewValue.ID = pCardReaderID;
                        CRNewValue.Name = dt.Rows[0]["card_reader_name"].ToString();
                        CRNewValue.BayNo = dt.Rows[0]["bay_no"].ToString();
                        CRNewValue.BayName = dt.Rows[0]["bay_name"].ToString();
                        vMsg = "Create " + CRNewValue.Name + " successful.[Adddress=" + CRNewValue.Address + "]";
                    }
                    else
                    {
                        vMsg = "Cannot find Card Reader id[" + pCardReaderID + "]";
                    }
                    RaiseEvents(vMsg);
                }
            }
            catch (Exception exp)
            {
                fMain.LogFile.WriteErrLog(exp.Message);
            }

        }

        public bool InitialComportCardReader()
        {
            string strSQL = "select" +
                            " t.comp_id,t.comport_no,t.comport_setting" +
                            " from tas.VIEW_CR_COMPORT_BAY t " +
                            " where t.card_reader_id=" + CRNewValue.ID +
                            " order by t.card_reader_id";
            DataSet vDataset = null;
            DataTable dt;
            bool vRet = false;
            try
            {
                if (fMain.OraDb.OpenDyns(strSQL, "TableName", ref vDataset))
                {
                    dt = vDataset.Tables["TableName"];
                    CRComport = new Comport[dt.Rows.Count];
                    crSerialPort = new SerialPort[dt.Rows.Count];
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        crSerialPort[i] = new SerialPort();
                        CRComport[i] = new Comport(fMain, ref crSerialPort[i], Convert.ToDouble(dt.Rows[i]["comp_id"].ToString()));
                        CRComport[i].InitialPort();
                        //CRComport[i].StartThread();
                    }
                    CRComport[processId].StartThread();
                    crPort = CRComport[processId];
                    vRet = true;
                }
            }
            catch (Exception exp)
            {  fMain.LogFile.WriteErrLog(exp.Message); }
            vDataset = null;
            dt = null;
            return vRet;
        }
    }
}
