using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Oracle.DataAccess.Client;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;

namespace PCRBay
{
    public class cCRProcess :IDisposable
    {
        frmCRBay mFMercury;
        cPorts mPort;
        public cCardReader[] mCardReader;
        clogfile mLog;

        public  List<string> mMyList=new List<string>();

        string mLogFileName;

        #region construct and deconstruct
        private bool IsDisposed = false;
        public void Dispose()
        {
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
                    WriteLogCardReader("<-------------System offline------------->");
                    if (mCardReader != null)
                    {
                        foreach (cCardReader c in this.mCardReader)
                        {
                            if (c != null)
                            {
                                c.Dispose();
                                Thread.Sleep(100);
                            }
                        }
                    }
                    mCardReader = null;
                    if (mPort != null)
                    {
                        mPort.Dispose();
                    }
                    mPort = null;
                    mLog = null;
                }
                //Clean up unmanaged resources
            }
            IsDisposed = true;
        }

        public cCRProcess(frmCRBay f)
        {
            mLog = new clogfile();
            mFMercury = f;
            //PPort = new cPorts(fMercury);
        }

        ~cCRProcess()
        {
            mPort.Dispose();
            mCardReader = null;
        }
        #endregion

        public void WriteLogCardReader(string pMsg)
        {
            try
            {
                string vMsg;
                if ((pMsg.ToLower().Contains("comport")) || pMsg.ToLower().Contains("system"))
                    vMsg = DateTime.Now + ">>[" + mLogFileName + "]" + pMsg;
                else
                    vMsg = DateTime.Now + ">" + pMsg;
                if (mMyList.Count > 1000)
                    mMyList.Clear();
                mMyList.Insert(0, vMsg);
                mFMercury.AddListBox = (object)vMsg;
                mLog.WriteLog(mLogFileName, vMsg);
            }
            catch (Exception exp)
            { }
        }

        private void Addlistbox(string pMsg)
        {
            try
            {
                //myList.Insert(0, (object)DateTime.Now + ">" + iMsg);
                mFMercury.AddListBox = (object)DateTime.Now + ">>" + pMsg;
                //PLog.WriteLog(vLogFileName,DateTime.Now+">" + iMsg);
            }
            catch (Exception exp)
            { }
        }

        public void InitialProcess(Int32 pCompID)
        {
            //Addlistbox(fMercury.OraDb.ConnectStatus().ToString());
            //PPort = new cPorts(fMercury);
            //PPort.InitialPort(pCompID);
            //PPort.StartThread();
            Thread.Sleep(500);
            string strSQL = "select t.* from tas.VIEW_CR_BAY t" +
                            " where t.comp_id=" + pCompID;
            DataSet ds = new DataSet();
            DataTable dt;
            string vMsg = "";

            try
            {
                if (mFMercury.mOraDb.OpenDyns(strSQL,"TableName",ref ds))
                {
                    dt = ds.Tables["TableName"];
                    mLogFileName = "CRBay" + dt.Rows[0]["bay_no"].ToString();
                    WriteLogCardReader("<-------------System online------------->");
                    mCardReader = new cCardReader[dt.Rows.Count];
                    
                    for (int i = 0; i < mCardReader.Length; i++)
                    {
                        vMsg = "Create " + dt.Rows[i]["card_reader_name"];
                        
                        mCardReader[i] = new cCardReader(this.mFMercury,this);

                        mCardReader[i].mCR_NewValue.IsEnable = Convert.ToBoolean(dt.Rows[i]["is_enabled"]);
                        mCardReader[i].mCR_NewValue.IsLock = Convert.ToBoolean(dt.Rows[i]["is_locked"]);
                        mCardReader[i].mCR_NewValue.IsProcess=Convert.ToBoolean(dt.Rows[i]["is_process"]);
                        mCardReader[i].mCR_NewValue.ID = dt.Rows[0]["card_reader_id"].ToString();
                        vMsg = vMsg + (mCardReader[i].mCR_NewValue.IsEnable ? "" : "[Card Reader disable!!!]").ToString();
                        WriteLogCardReader(vMsg);
                        if(mCardReader[i].mCR_NewValue.IsEnable)
                        {                      
                            mCardReader[i].InitialCardReader(dt.Rows[i]["card_reader_id"].ToString());
                        }
                        //Thread.Sleep(500);
                        //PCardReader[i].StartThread();
                    }
                }
                WriteLogCardReader("Database connect = " + mFMercury.mOraDb.ConnectStatus().ToString()+"[" + 
                                    mFMercury.mOraDb.GetConnectDBName()+"]");
                mPort = new cPorts(mFMercury,this);
                mPort.DataReceived += OnReceive;
                mPort.InitialPort(pCompID);
                mPort.StartThread();
                Thread.Sleep(1000);
                for (int i = 0; i < mCardReader.Length; i++)
                {
                    if (mCardReader[i].mCR_NewValue.IsEnable)
                    {
                        Thread.Sleep(50);
                        mCardReader[i].StartThread(ref mPort);
                    }
                }
            }
            catch (Exception exp)
            {
                WriteLogCardReader(exp.ToString());
            }
            ds = null;
            dt = null;
        }

        public void ReStartProcess(int pCRIndex)
        {
            DataSet ds = new DataSet();
            DataTable dt;
            try
            {
                string vCrID = mCardReader[pCRIndex].mCR_NewValue.ID;

                string strSQL = "select t.* from tas.VIEW_CR_BAY t" +
                                " where t.card_reader_id=" + vCrID;

                string vMsg = "";

                if (mFMercury.mOraDb.OpenDyns(strSQL, "TableName", ref ds))
                {
                    dt = ds.Tables["TableName"];
                    mLogFileName = "CRBay" + dt.Rows[0]["bay_no"].ToString();
                    WriteLogCardReader("<-------------System online[restart]------------->");

                    vMsg = "Create " + dt.Rows[0]["card_reader_name"];

                    mCardReader[pCRIndex].mCR_NewValue.IsEnable = Convert.ToBoolean(dt.Rows[0]["is_enable"]);
                    mCardReader[pCRIndex].mCR_NewValue.IsLock = Convert.ToBoolean(dt.Rows[0]["is_lock"]);
                    mCardReader[pCRIndex].mCR_NewValue.IsProcess = Convert.ToBoolean(dt.Rows[0]["is_process"]);
                    vMsg = vMsg + (mCardReader[pCRIndex].mCR_NewValue.IsEnable ? "" : "[Card Reader disable!!!]").ToString();
                    WriteLogCardReader(vMsg);

                    if (mCardReader[pCRIndex].mCR_NewValue.IsEnable)
                    {
                        mCardReader[pCRIndex].InitialCardReader(dt.Rows[0]["card_reader_id"].ToString());
                        mCardReader[pCRIndex].StartThread(ref mPort);
                    }
                }
            }
            catch (Exception exp)
            {
                WriteLogCardReader(exp.ToString());
            }
            ds = null;
            dt = null;
        }

        private void OnReceive(string pRecv,string pOwnerName)
        {
            string s;;
            for (int i = 0; i < mCardReader.Length; i++)
            {
                s = mCardReader[i].mCR_NewValue.Address.ToString("00")+"D";
                if (pRecv.IndexOf(s) == 1)
                {
                    mCardReader[i].mCR_NewValue.DataReceive = pRecv;
                }
                else
                    mCardReader[i].mCR_NewValue.DataReceive = "";
                //if (mCardReader[i].mCR_NewValue.Name == pOwnerName)
                //{
                //    mCardReader[i].mCR_NewValue.DataReceive = pRecv;
                //    break;
                //}
            }
            Thread.Sleep(100);
        }

    }

    
}
