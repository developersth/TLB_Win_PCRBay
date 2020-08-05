using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data;
using System.Net.Sockets;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;
using System.Net.NetworkInformation;

namespace PCRBay
{
	public class cPorts :IDisposable
	{
        public Action<string,string> DataReceived;

        struct _argumentPort
        {
            public bool IsTCP;
            public string portNo;
            public string portSetting;
            public string portType;
            public int IP_Port;
            public string IP_Address;
            public bool IsOpen;
            public bool IsIdle;
            public bool IsEnable;
        }

        _argumentPort mArgumentPort;
        frmCRBay mFMercury;
        clogfile mLog;
        //cAccuLoads[] cAccu;
        DateTime mResponseTime;
        Thread mThread,pThread;

        bool mConnect;
        bool mShutdown;
        bool mRunning;
        bool mRunn;
        //private bool istcp;
        //private string port;

        private SerialPort mSp;
        private TcpClient tc;
        Stream mStm;
        private Double mComp_id;
        private int mTotalMeter;

        public string mRecv;
        private byte[] mRecbyte = new byte[512];
        private byte[] mSenbyte = new byte[512];
        private string mBay;
        private string mIsland;
        cCRProcess mCRPRocess;
        string mMsg;
        string mOwnerName;
        bool mResponse;
        int mCountWriteLog;

        #region construct and deconstruct
            private bool IsDisposed = false;
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
                //mRunn = false;
            }
            protected void Dispose(bool Diposing)
        {
            if (!IsDisposed)
            {
                if (Diposing)
                {
                    //Clean Up managed resources
                    mRunn = false;
                    mShutdown = true;
                    mArgumentPort.IsIdle = true;
                    if ((mThread != null) && (mThread.ThreadState == ThreadState.Running))
                        mThread.Abort();
                    Thread.Sleep(5);
                    if (IsOpen())
                        ClosePort();
                    
                    mLog = null;
                    //fMain = null;
                    tc = null;
                    //if(mSp!=null)
                    //    mSp.Dispose();
                    mSp = null;
                    mStm = null;
                    
                }
                //Clean up unmanaged resources
            }
            IsDisposed = true;
        }
            public cPorts(frmCRBay f, cCRProcess pCRProcess)
            {
                //fMain = new frmMain();
                mFMercury = f;
                mLog = new clogfile();
                mCRPRocess = pCRProcess;
                //Addlistbox("Initial ports...");
                //StartThread();
            }
            ~cPorts()
            {
                mShutdown = true;
                //clog = null;
                //fMain = null;
                //tc = null;
                //sp = null;
                //stm = null;
                //if (IsOpen())
                //    ClosePort();
            }
        #endregion

        public void Addlistbox(string pMsg)
        {
            try
            {
                mCRPRocess.WriteLogCardReader(pMsg);
                //fMain.AddListBox = (object)DateTime.Now + ">Initial ports...";
                //mFMercury.AddListBox = (object)DateTime.Now + "->" + iMsg;
            }
            catch (Exception exp)
            { }
        }

        public void StartThread()
        {
            mRunn = true;
            mResponseTime = DateTime.Now;
            mRunning = false;
            try
            {
                if (mRunning)
                {
                    return;
                }
                mThread = new Thread(this.RunProcess);
                mRunning = true;
                mThread.Name = "Com_id[" + mComp_id + "]";
                mThread.Start();
            }
            catch (Exception exp)
            {
                mRunning = false;
            }

        }

        private void RunProcess()
        {
            int vCount=0;
            Thread.Sleep(500);
            while (true)
            {
                if (mArgumentPort.IsEnable)
                {
                    if (vCount == 0)
                    {
                        OpenPort();
                        vCount++;
                    }
                    else
                    {
                        vCount += 1;
                        if (vCount > 6)
                            vCount = 0;
                    }
                }
                else
                    InitialPort(mComp_id);
                //if (mConnect || mShutdown)
                if(mConnect || !mRunn)
                    break;
                Thread.Sleep(1000);
                
            }
            //ClosePort();
            Thread.Sleep(100);
            mRunning = false;
        }

        public void CheckResponse(bool pResponse)
        {
            if (pResponse)
            {
                mResponseTime = DateTime.Now;
                mResponse = pResponse;
            }
            else
            {
                DateTime vDateTime = DateTime.Now;
                var vDiff = (vDateTime - mResponseTime).TotalSeconds;

                if ((vDiff > 5) && (mResponse=true))
                {
                    mResponse = false;
                    //if (sp.IsOpen)
                    //{
                    //    ClosePort();
                    //    RunProcess();
                    //}
                }
            }

        }

        public void InitialPort(Double pComp_ID)
        {
            DataSet vDataSet = new DataSet();
            DataTable dt;
            bool b;

            mComp_id = pComp_ID;

            string strSQL = "select t.comport_no,t.comport_type,t.comport_setting,t.is_enable" +
                            " from tas.VIEW_CR_BAY_COMPORT t" +
                            " where t.comp_id=" + mComp_id +
                            " and rownum=1";
            if(mFMercury.mOraDb.OpenDyns(strSQL, "TableName",ref vDataSet))
            {
                dt = vDataSet.Tables["TableName"];
                //if(!(Convert.IsDBNull(dt.Rows[0]["ip_port"])))
                //    argumentPort.IP_Port = Convert.ToInt16(dt.Rows[0]["ip_port"]);
                mArgumentPort.portNo = dt.Rows[0]["comport_no"].ToString();
                if(!(Convert.IsDBNull(dt.Rows[0]["comport_setting"])))
                    mArgumentPort.portSetting = dt.Rows[0]["comport_setting"].ToString();
                mArgumentPort.portType = dt.Rows[0]["comport_type"].ToString();

                mArgumentPort.IsEnable = Convert.ToBoolean(dt.Rows[0]["is_enable"]);
                //if(!(Convert.IsDBNull(dt.Rows[0]["ip_address"])))
                //    argumentPort.IP_Address = dt.Rows[0]["ip_address"].ToString();
            }

            if (mArgumentPort.portType.ToUpper().IndexOf("TCP/IP") == 0)
            {
                mArgumentPort.IsTCP = true;
                mMsg = "Open comport =" + mArgumentPort.IP_Address + ":" + mArgumentPort.IP_Port;
            }
            else
            {
                mArgumentPort.IsTCP = false;
                mMsg = "Open comport =" + mArgumentPort.portNo + ":" + mArgumentPort.portSetting;
            }

            mMsg = mMsg + (mArgumentPort.IsEnable ? "" : "[Comport disable!!!]").ToString();
            Addlistbox(mMsg);
            vDataSet = null;
            //OpenPort();
        }

        #region Comport & TCP/IP

            private bool OpenPort()
            {
                bool vIsAvailable = true;
                try
                {
                    if (mArgumentPort.IsTCP)
                    {
                        Application.DoEvents();
                        IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                        TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
                        foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
                        {
                            if (tcpi.LocalEndPoint.Port == mArgumentPort.IP_Port)
                            {
                                vIsAvailable = false;
                                break;
                            }
                        }

                        if (vIsAvailable)
                        {
                            tc = new TcpClient(mArgumentPort.IP_Address, mArgumentPort.IP_Port);
                            //tc = new TcpClient("192.168.1.193", 7734);
                            tc.SendTimeout = 1000;
                            mStm = tc.GetStream();
                            mMsg = "Comport =" + mArgumentPort.IP_Address + " : " + mArgumentPort.IP_Port + " Open successfull.";
                            Addlistbox(mMsg);
                        }
                    }
                    else//serial port
                    {
                        string[] spli = mArgumentPort.portSetting.Split(',');
                        if (spli.Length != 4)
                            return false;
                        mSp = new SerialPort();
                        //mSp.DataReceived += SerialPort_DataReceived;
                        mSp.PortName = mArgumentPort.portNo;
                        mSp.BaudRate = Int32.Parse(spli[0]);
                        switch (spli[1].ToString().ToUpper().Trim())
                        {
                            case "E": mSp.Parity = Parity.Even; break;
                            case "M": mSp.Parity = Parity.Mark; break;
                            case "O": mSp.Parity = Parity.Odd; break;
                            case "S": mSp.Parity = Parity.Space; break;
                            default: mSp.Parity = Parity.None; break;
                        }
                        mSp.DataBits = Int32.Parse(spli[2]);
                        switch (spli[3].Trim())
                        {
                            case "1": mSp.StopBits = StopBits.One; break;
                            case "1.5": mSp.StopBits = StopBits.OnePointFive; break;
                            case "2": mSp.StopBits = StopBits.Two; break;
                            default: mSp.StopBits = StopBits.None; break;
                        }
                        mSp.ReadTimeout = 500;
                        mSp.WriteTimeout = 500;
                        mSp.Open();
                        mSp.DiscardInBuffer();
                        mSp.DiscardOutBuffer();
                    }
                    mArgumentPort.IsOpen = true;
                    mArgumentPort.IsIdle = true;
                    mConnect = true;
                    mMsg = "Comport =" + mArgumentPort.portNo + " : " + mArgumentPort.portSetting + " Open successfull.";
                    Addlistbox(mMsg);
                }
                catch (Exception exp)
                {
                    //Addlistbox(exp.Message.ToString());
                    if (mCountWriteLog > 120)
                    {
                        mCountWriteLog = 0;
                        mLog.WriteErrLog(exp.ToString());
                        mArgumentPort.IsOpen = false;
                    }
                    else
                        mCountWriteLog += 1;
                    return false;
                }
                return true;
            }

            public void ClosePort()
            {
                try
                {
                    if (mArgumentPort.IsTCP)
                    {
                        tc.Close();
                        mMsg = "Comport =" + mArgumentPort.IP_Address + " : " + mArgumentPort.IP_Port +
                          " Close successfull.";
                    }
                    else
                    {
                        if (mSp.IsOpen)
                        {
                            mSp.DataReceived -= new SerialDataReceivedEventHandler(SerialPort_DataReceived);
                        }
                        mSp.DiscardInBuffer();
                        mSp.DiscardOutBuffer();
                        mSp.Close();

                        mArgumentPort.IsOpen = false;
                        mMsg = "Comport =" + mArgumentPort.portNo + " : " + mArgumentPort.portSetting +
                              " Close successfull.";
                    }
                    Addlistbox(mMsg);
                }

                catch (Exception exp) { /*PLog.WriteErrLog(exp.ToString());*/ }
            }

            public  bool IsOpen()
            {
                return mArgumentPort.IsOpen;
            }

            public bool IsIdle
            {
                get { return mArgumentPort.IsIdle; }
                set { mArgumentPort.IsIdle = value; }
            }
            //private bool TestConnection()
            //{
                //try
                //{
                //    return !(TCPSocket.Poll(1000, SelectMode.SelectRead) && TCPSocket.Available == 0);
                //}
                //catch (SocketException) { return false; }
            //}
        #endregion

            public void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
            {
                try
                {
                    //no. of data at the port
                    int ByteToRead = mSp.BytesToRead;

                    //create array to store buffer data
                    byte[] inputData = new byte[ByteToRead];

                    //read the data and store
                    mSp.Read(inputData, 0, ByteToRead);

                    string s = Encoding.ASCII.GetString(inputData);
                    var copy = DataReceived;
                    if (copy != null) copy(s,"");

                }
                catch (SystemException exp)
                {
                    mLog.WriteErrLog(exp.ToString());
                    
                }
            }

            public void SerialPort_DataReceived1(string pOwnerName)
            {
                try
                {
                    //no. of data at the port
                    int ByteToRead = mSp.BytesToRead;

                    //create array to store buffer data
                    byte[] inputData = new byte[ByteToRead];

                    //read the data and store
                    mSp.Read(inputData, 0, ByteToRead);

                    string s = Encoding.ASCII.GetString(inputData);
                    var copy = DataReceived;
                    if (copy != null) copy(s, pOwnerName);

                }
                catch (SystemException ex)
                {
                    //MessageBox.Show(ex.Message, "Data Received Event");
                }
            }

        public string ReceiveData()
        {
            mRecv = "";
            //Thread.Sleep(70);
            if (IsOpen())
            {
                try
                {
                    if (mArgumentPort.IsTCP)
                    {
                        mStm.Read(mRecbyte, 0, mRecbyte.Length);
                        mRecv = ASCIIEncoding.UTF8.GetString(mRecbyte);
                    }
                    else
                    {
                        mRecv = mSp.ReadExisting();
                        //mSp.DiscardInBuffer();
                        //var copy = DataReceived;
                        //if (copy != null) copy(recv);
                    }
                }
                catch (Exception exp)
                {
                    CheckResponse();
                    mRecv = "";
                }
            }
            else
            {
                CheckResponse();
            }
            return mRecv;
        }

        public void SendData(string iMsg)
        {
            byte[] vSend;
            if (IsOpen())
            {
                try
                {
                    if (mArgumentPort.IsTCP)
                    {
                        mSenbyte = new byte[iMsg.Length * sizeof(char)];
                        System.Buffer.BlockCopy(iMsg.ToCharArray(), 0, mSenbyte, 0, mSenbyte.Length);
                        mStm.Write(mSenbyte, 0, mSenbyte.Length);
                    }
                    else
                    {
                        mSp.Write(iMsg);
                    }
                }
                catch (Exception exp)
                {
                    CheckResponse();
                }
            }
        }

        public void SendData(byte[] iMsg)
        {
            //Application.DoEvents();
            byte[] vSend;
            if (IsOpen())
            {
                try
                {
                    if (mArgumentPort.IsTCP)
                    {  
                    }
                    else
                    {
                        Thread.Sleep(100);
                        //mSp.DiscardOutBuffer();
                        string s = Encoding.ASCII.GetString(iMsg);
                        mSp.Write(iMsg, 0, iMsg.Length);
                        
                    }
                }
                catch (Exception exp)
                {
                    CheckResponse();
                }
            }
        }

        public void SendData(byte[] iMsg,ref string pOwnerName)
        {
            //Application.DoEvents();
            byte[] vSend;
            if (mSp.IsOpen)
            {
                mOwnerName = pOwnerName;
                try
                {
                    if (mArgumentPort.IsTCP)
                    {
                    }
                    else
                    {
                        mSp.Write(iMsg, 0, iMsg.Length);
                    }
                }
                catch (Exception exp)
                {
                    CheckResponse();
                }
                Thread.Sleep(100);
                SerialPort_DataReceived1(pOwnerName);
                //ReceiveThread(pOwnerName);
                //string s = pOwnerName;
                //pThread = new Thread( () => this.ReceiveThread(s));
                //pThread.Start();
            }
        }

        private byte[] BuildMsg(byte pCR_address, string pMsg)
        {
            //string strData;
            byte[] vCR_msg = null;
            //byte[] cr_data = null;

            //CRSendNextBlock(cr_address, ref cr_msg, msg);

            byte mSTX = 2;
            byte R = 82;
            byte mETX = 3;

            byte[] asc = Encoding.ASCII.GetBytes(pMsg);

            byte[] vMsg = new byte[1 + 2 + 1 + asc.Length + 1 + 1 + 1];
            for (int i = 0; i < vMsg.Length; i++)
            {
                switch (i)
                {   //string.Format("{0:X}",(int)(msg[i-1]));
                    case 0:
                        vMsg[i] = mSTX;
                        break;
                    case 1:
                        asc = Encoding.ASCII.GetBytes(pCR_address.ToString());
                        vMsg[i + 1] = asc[0];
                        asc = Encoding.ASCII.GetBytes("0");
                        vMsg[i] = asc[0];
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
                            vMsg[i] = mETX;
                        }
                        //else if (i == 4)    
                        //{

                        //    bMsg[i] = ESC;
                        //}
                        else
                            vMsg[i] = (asc[i - 4]);
                        break;
                }
            }
            vCR_msg = vMsg;
            CalCSUM(vCR_msg, ref vCR_msg[vCR_msg.Length - 2]);
            return vCR_msg;
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

        private void CheckResponse()
        {
            DateTime vDateTime = DateTime.Now;
            var vDiff = (vDateTime - mResponseTime).TotalSeconds;

            if ((vDiff > 5) && (mResponse==true))
            {
                mResponseTime = DateTime.Now;
                mResponse = false;
                if (mSp.IsOpen)
                {
                    ClosePort();
                    StartThread();
                }
            }
        }
	}
}
