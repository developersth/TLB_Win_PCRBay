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

namespace PCRBAY
{
    public class Comport : IDisposable
    {
        public Action<string, string> DataReceived;
        public Action<string, string> OnCommEvents;

        #region Constant, Struct and Enum
        enum _ComportType : int
        {
            SerialPort = 0
            ,
            TCP_IPPort = 1
        }

        struct _ComportBuffer
        {
            public bool IsTCP;
            public string PortNo;
            public string PortSetting;
            public _ComportType PortType;
            public int IP_Port;
            public string IP_Address;
            public bool IsOpen;
            public bool IsIdle;
            public bool IsEnable;
            public double ComportID;
        }
        #endregion

        _ComportBuffer comportBuffer;
        frmMain fMain;
        Logfile logFile;
        //cAccuLoads[] cAccu;
        DateTime datetimeResponse;
        Encoding objEncoding = Encoding.GetEncoding("Windows-874");
        //private bool istcp;
        //private string port;

        public SerialPort Sp;
        private TcpClient tcpPort;
        Stream stm;
        //public Double ComprtID;

        public string TimeSend;
        public string TimeRecv;
        string dataRecv = "";
        string dataSend = "";
        public bool DiagComport;

        private byte[] byteRecv = new byte[512];
        private byte[] byteSend = new byte[512];
        //private string mBay;
        //private string mIsland;
        string comportMsg;
        //string mOwnerName;
        bool comportResponse;
        int countWriteLog;
        //string mMeterName;
        private int totalCharacterBit = 11; //default value -> one start, 8 data, 1 parity, 1 stop
        private Single characterTime;
        

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
                    thrRunn = false;
                    thrShutdown = true;
                    comportBuffer.IsIdle = true;
                    if ((thrProcessPort != null) && (thrProcessPort.ThreadState == ThreadState.Running))
                        thrProcessPort.Abort();
                    Thread.Sleep(5);
                    if (IsOpen())
                        CloseComPort();

                    logFile = null;
                    tcpPort = null;
                    Sp = null;
                    stm = null;

                }
                //Clean up unmanaged resources
            }
            thrShutdown = true;
            IsDisposed = true;
        }
        
        public Comport(frmMain pFrm, ref System.IO.Ports.SerialPort pPort)
        {
            fMain = pFrm;
            Sp = pPort;
            logFile = new Logfile();
        }

        public Comport(frmMain pFrm, ref System.IO.Ports.SerialPort pPort,double pCompID)
        {
            fMain = pFrm;
            Sp = pPort;
            comportBuffer.ComportID = pCompID;
            logFile = new Logfile();
        }
        ~Comport()
        {
            thrShutdown = true;
        }
        #endregion

        #region property
        public double ComportID
        {
            get { return comportBuffer.ComportID; }
            set { comportBuffer.ComportID = value; }
        }

        public string ComportNo
        {
            get { return comportBuffer.PortNo; }
        }

        public string DiagMsgSend()
        {
            //string[] vMsg = new string[3];
            //vMsg[0] = string.Format("{0}> Send {1}, Len={2} bytes", TimeSend, comportBuffer.PortNo, byteSend.Length.ToString());
            //vMsg[1] = " ".PadLeft(TimeSend.Length) + string.Format("{0}", dataSend);
            //vMsg[2] = " ".PadLeft(TimeSend.Length) + BitConverter.ToString(byteSend).Replace('-', ' ');
            //return vMsg;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("{0}> Send {1}, Len={2} bytes", TimeSend, comportBuffer.PortNo, byteSend.Length.ToString()));
            sb.AppendLine(" ".PadLeft(TimeSend.Length) + string.Format("{0: }", dataSend));
            //sb.AppendLine(" ".PadLeft(TimeSend.Length) + BitConverter.ToString(byteSend).Replace('-', ' '));
            return sb.ToString();
        }

        public string DiagMsgReceive()
        {
            //string[] vMsg = new string[3];
            //vMsg[0] = string.Format("{0}> Receive {1}, Len={2} bytes", TimeRecv, comportBuffer.PortNo, byteRecv.Length.ToString());
            //vMsg[1] = " ".PadLeft(TimeRecv.Length) + string.Format("{0}", dataRecv);
            //vMsg[2] = " ".PadLeft(TimeRecv.Length) + BitConverter.ToString(byteRecv).Replace('-', ' ');
            //return vMsg;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("{0}> Receive {1}, Len={2} bytes", TimeRecv, comportBuffer.PortNo, byteRecv.Length.ToString()));
            sb.AppendLine(" ".PadLeft(TimeRecv.Length) + string.Format("{0: }", dataRecv));
            //sb.AppendLine(" ".PadLeft(TimeRecv.Length) + BitConverter.ToString(byteRecv).Replace('-', ' '));
            return sb.ToString();
        }
        #endregion

        #region class events
        public delegate void ComportEventsHandler(object sender, string message);
        public event ComportEventsHandler OnComportEvents;
        string logFileName = "";

        void RaiseEvents(string pSender, string pMsg)
        {
            string vMsg = DateTime.Now + ">[" + pSender + "]" + pMsg;
            //logFileName = comportBuffer.PortNo;
            try
            {
                fMain.AddListBox = "" + logFileName + ">" + vMsg;
                //fMain.LogFile.WriteLog(logFileName, vMsg);
            }
            catch (Exception exp)
            { }
        }

        void RaiseEvents(string pMsg)
        {
            string vMsg = logFileName + ">" + DateTime.Now + ">" + pMsg;
            //logFileName = comportBuffer.PortNo;
            try
            {
                //fMain.DisplayMessage(logFileName, vMsg);
                //fMain.LogFile.WriteLog(logFileName, vMsg);
                fMain.AddListBox = "" + logFileName + ">" + vMsg;
                fMain.LogFile.WriteLog(logFileName, vMsg);
            }
            catch (Exception exp)
            { }
        }
        //void RaiseEvents(string pMsg)
        //{
        //    if (OnComportEvents != null)
        //    {
        //        OnComportEvents((string)ComportMember.PortNo, pMsg);
        //    }
        //}
        #endregion

        #region thread

        Thread thrProcessPort;

        bool connect;
        bool thrShutdown;
        bool thrRunning;
        bool thrRunn;
        private static object thrLock = new object();

        public void StartThread()
        {
            thrRunn = true;
            thrShutdown = false;
            datetimeResponse = DateTime.Now;
            thrRunning = false;
            try
            {
                if (thrRunning)
                {
                    return;
                }
                thrProcessPort = new Thread(this.RunProcess);
                thrRunning = true;
                thrProcessPort.Name = "Com_id[" + comportBuffer.ComportID + "]";
                thrProcessPort.Start();
            }
            catch (Exception exp)
            {
                thrRunning = false;
            }

        }

        public void StartThread(string pMeterName)
        {
            thrRunn = true;
            thrShutdown = false;
            datetimeResponse = DateTime.Now;
            thrRunning = false;
            //mMeterName = pMeterName;

            if (comportBuffer.IsTCP)
            {
                comportMsg = "Open comport =" + comportBuffer.IP_Address + ":" + comportBuffer.IP_Port;
            }
            else
            {
                comportMsg = "Open comport =" + comportBuffer.PortNo + ":" + comportBuffer.PortSetting;
            }

            comportMsg = comportMsg + (comportBuffer.IsEnable ? "" : "[Comport disable!!!]").ToString();
            RaiseEvents(comportMsg);

            try
            {
                if (thrRunning)
                {
                    return;
                }
                thrProcessPort = new Thread(this.RunProcess);
                thrRunning = true;
                thrProcessPort.Name = "Com_id[" + comportBuffer.ComportID + "]";
                thrProcessPort.Start();
            }
            catch (Exception exp)
            {
                thrRunning = false;
            }

        }

        public void StopThread()
        {
            thrShutdown = true;
        }

        private void RunProcess()
        {
            int vCount = 0;
            Thread.Sleep(500);
            while (!thrShutdown)
            {
                if (comportBuffer.IsEnable)
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
                    InitialPort(comportBuffer.ComportID);
                //if (mConnect || mShutdown)
                if (connect || !thrRunn)
                    break;
                Thread.Sleep(1000);

            }
            //ClosePort();
            Thread.Sleep(100);
            thrRunning = false;
        }

        #endregion

        public void CheckResponse(bool pResponse)
        {
            if (pResponse)
            {
                datetimeResponse = DateTime.Now;
                comportResponse = pResponse;
            }
            else
            {
                DateTime vDateTime = DateTime.Now;
                var vDiff = (vDateTime - datetimeResponse).TotalSeconds;

                if ((vDiff > 5) && (comportResponse = true))
                {
                    comportResponse = false;
                    //if (sp.IsOpen)
                    //{
                    //    ClosePort();
                    //    RunProcess();
                    //}
                }
            }

        }

        public void InitialPort(Double pCompID)
        {
            DataSet vDataSet = new DataSet();
            DataTable dt;
            bool b;

            comportBuffer.ComportID = pCompID;
            string strSQL = "select t.comport_no,t.comport_setting,t.comport_type" +
                            " from tas.VIEW_ATG_COMPORT t" +
                            " where t.comp_id=" + comportBuffer.ComportID;

            if (fMain.OraDb.OpenDyns(strSQL, "TableName", ref vDataSet))
            {
                dt = vDataSet.Tables["TableName"];
                comportBuffer.PortNo = dt.Rows[0]["comport_no"].ToString();
                if (!(Convert.IsDBNull(dt.Rows[0]["comport_setting"])))
                    comportBuffer.PortSetting = dt.Rows[0]["comport_setting"].ToString();
                comportBuffer.PortType = CheckComportType(dt.Rows[0]["comport_type"].ToString());

                comportBuffer.IsEnable = Convert.ToBoolean(dt.Rows[0]["enabled"]);
                //if (!(Convert.IsDBNull(dt.Rows[0]["ip_address"])))
                //    argumentPort.IP_Address = dt.Rows[0]["ip_address"].ToString();
            }

            if (comportBuffer.PortType == _ComportType.TCP_IPPort)
            {
                comportBuffer.IsTCP = true;
                comportMsg = "Open comport =" + comportBuffer.IP_Address + ":" + comportBuffer.IP_Port;
            }
            else
            {
                comportBuffer.IsTCP = false;
                comportMsg = "Open comport =" + comportBuffer.PortNo + ":" + comportBuffer.PortSetting;
            }

            comportMsg = comportMsg + (comportBuffer.IsEnable ? "" : "[Comport disable!!!]").ToString();
            //Addlistbox(mMsg);
            vDataSet = null;
            //OpenPort();
        }

        public void InitialPort()
        {
            DataSet vDataSet = new DataSet();
            DataTable dt;
            bool b;

            string strSQL = "select t.comport_no,t.comport_setting,t.comport_type,t.card_reader_name" +
                            " from tas.VIEW_CR_COMPORT_BAY t" +
                            " where t.comp_id=" + comportBuffer.ComportID;

            if (fMain.OraDb.OpenDyns(strSQL, "TableName", ref vDataSet))
            {
                dt = vDataSet.Tables["TableName"];
                comportBuffer.PortNo = dt.Rows[0]["comport_no"].ToString();
                if (!(Convert.IsDBNull(dt.Rows[0]["comport_setting"])))
                    comportBuffer.PortSetting = dt.Rows[0]["comport_setting"].ToString();
                comportBuffer.PortType = CheckComportType(dt.Rows[0]["comport_no"].ToString());
                logFileName = dt.Rows[0]["card_reader_name"].ToString();
                comportBuffer.IsEnable = true;
            }

            if (comportBuffer.PortType == _ComportType.TCP_IPPort)
            {
                comportBuffer.IsTCP = true;
                comportMsg = "Initial comport =" + comportBuffer.IP_Address + ":" + comportBuffer.IP_Port;
            }
            else
            {
                comportBuffer.IsTCP = false;
                comportMsg = "Initial comport id=" + comportBuffer.ComportID + ":" + comportBuffer.PortNo + "," + comportBuffer.PortSetting;
            }

            comportMsg = comportMsg + (comportBuffer.IsEnable ? "" : "[Comport disable!!!]").ToString();
            RaiseEvents(comportMsg);
            vDataSet = null;
            //OpenPort();
        }

        #region Comport & TCP/IP

        private bool OpenPort()
        {
            bool vIsAvailable = true;
            try
            {
                if (comportBuffer.IsTCP)
                {
                    comportMsg = "Open comport =" + comportBuffer.IP_Address + " : " + comportBuffer.IP_Port + ".";
                    Application.DoEvents();
                    IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                    TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
                    foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
                    {
                        if (tcpi.LocalEndPoint.Port == comportBuffer.IP_Port)
                        {
                            vIsAvailable = false;
                            break;
                        }
                    }

                    if (vIsAvailable)
                    {
                        tcpPort = new TcpClient(comportBuffer.IP_Address, comportBuffer.IP_Port);
                        //tc = new TcpClient("192.168.1.193", 7734);
                        tcpPort.SendTimeout = 1000;
                        stm = tcpPort.GetStream();
                        comportMsg = "Comport =" + comportBuffer.IP_Address + " : " + comportBuffer.IP_Port + " Open successfull.";
                        RaiseEvents(comportMsg);
                        comportBuffer.IsOpen = true;
                    }
                }
                else//serial port
                {
                    if (Sp.IsOpen)
                    {
                        //ComportMember.IsOpen = true;
                        comportMsg = "Port is opened by another application!";
                        RaiseEvents(comportMsg);
                        return true;
                    }
                    comportMsg = "Open comport =" + comportBuffer.PortNo + ":" + comportBuffer.PortSetting + ".";
                    RaiseEvents(comportMsg);
                    string[] spli = comportBuffer.PortSetting.Split(',');       //9600,N,8,1 -> baud,parity,data,stop
                    if (spli.Length != 4)
                        return false;
                    //mSp = new SerialPort();
                    //mSp.DataReceived += SerialPort_DataReceived;
                    Sp.PortName = comportBuffer.PortNo;
                    Sp.BaudRate = Int32.Parse(spli[0]);
                    switch (spli[1].ToString().ToUpper().Trim())
                    {
                        case "E": Sp.Parity = Parity.Even; break;
                        case "M": Sp.Parity = Parity.Mark; break;
                        case "O": Sp.Parity = Parity.Odd; break;
                        case "S": Sp.Parity = Parity.Space; break;
                        default: Sp.Parity = Parity.None; break;
                    }
                    Sp.DataBits = Int32.Parse(spli[2]);
                    switch (spli[3].Trim())
                    {
                        case "1": Sp.StopBits = StopBits.One; break;
                        case "1.5": Sp.StopBits = StopBits.OnePointFive; break;
                        case "2": Sp.StopBits = StopBits.Two; break;
                        default: Sp.StopBits = StopBits.None; break;
                    }

                    Sp.ReadTimeout = 100;
                    Sp.WriteTimeout = 500;
                    Sp.Open();
                    Sp.DiscardInBuffer();
                    Sp.DiscardOutBuffer();
                }
                comportBuffer.IsOpen = true;
                comportBuffer.IsIdle = true;
                connect = true;
                comportMsg = "Comport =" + comportBuffer.PortNo + ":" + comportBuffer.PortSetting + " Open successfull.";
                RaiseEvents(comportMsg);
            }
            catch (Exception exp)
            {
                //Addlistbox(exp.Message.ToString());
                if (countWriteLog > 120)
                {
                    countWriteLog = 0;
                    logFile.WriteErrLog(exp.ToString());
                    comportBuffer.IsOpen = false;
                }
                else
                    countWriteLog += 1;
                return false;
            }
            InitialCharacterTime();
            return true;
        }

        void InitialCharacterTime()
        {
            if (Sp.DataBits == 8)
                totalCharacterBit = 11;
            else
                totalCharacterBit = 10;

            characterTime = Convert.ToSingle((totalCharacterBit * (Single)1000) / (Single)Sp.BaudRate); //in ms
        }

        public int CalculateSilentTime(int pCharacterLen) //return in ms
        {
            //nitialCharacterTime();
            if (pCharacterLen <= 8)
                pCharacterLen = 8;
            return Convert.ToInt16(characterTime * (pCharacterLen + 1) * 8 * 4.5);
        }

        public string ComportInfo()
        {
            string vMsg = "";
            if (comportBuffer.IsTCP)
            {
                vMsg = "Comport:" + comportBuffer.IP_Address + ":" + comportBuffer.IP_Port;
            }
            else
            {
                vMsg = "Comport:" + comportBuffer.PortNo + ":" + comportBuffer.PortSetting;
            }
            return vMsg;
        }

        public void CloseComPort()
        {
            try
            {
                if (comportBuffer.IsTCP)
                {
                    tcpPort.Close();
                    comportMsg = "Comport =" + comportBuffer.IP_Address + " : " + comportBuffer.IP_Port +
                        " close successfull.";
                }
                else
                {
                    if (Sp.IsOpen)
                    {
                        Sp.DataReceived -= new SerialDataReceivedEventHandler(SerialPort_DataReceived);
                    }
                    Sp.DiscardInBuffer();
                    Sp.DiscardOutBuffer();
                    Sp.Close();

                    comportBuffer.IsOpen = false;
                    comportMsg = "Comport =" + comportBuffer.PortNo + " : " + comportBuffer.PortSetting +
                            " close successfull.";
                }
                RaiseEvents(comportMsg);
            }

            catch (Exception exp) { /*PLog.WriteErrLog(exp.ToString());*/ }
        }

        public bool IsOpen()
        {
            return comportBuffer.IsOpen;
        }

        public bool IsIdle
        {
            get { return comportBuffer.IsIdle; }
            set { comportBuffer.IsIdle = value; }
        }

        private _ComportType CheckComportType(string pCompNo)
        {
            if (pCompNo.IndexOf("COM") >= 0)
            {
                return _ComportType.SerialPort;
            }
            else
            {

                return _ComportType.TCP_IPPort;
            }
        }

        #endregion

        public void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                //no. of data at the port
                int ByteToRead = Sp.BytesToRead;

                //create array to store buffer data
                byte[] inputData = new byte[ByteToRead];

                //read the data and store
                Sp.Read(inputData, 0, ByteToRead);

                string s = Encoding.ASCII.GetString(inputData);
                var copy = DataReceived;
                if (copy != null) copy(s, "");

            }
            catch (SystemException exp)
            {
                logFile.WriteErrLog(exp.ToString());

            }
        }

        public void SerialPort_DataReceived1(string pOwnerName)
        {
            try
            {
                //no. of data at the port
                int ByteToRead = Sp.BytesToRead;

                //create array to store buffer data
                byte[] inputData = new byte[ByteToRead];

                //read the data and store
                Sp.Read(inputData, 0, ByteToRead);

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
            //string recvData = "";
            //Thread.Sleep(70);
            int vLength;
            lock (thrLock)
            {
                TimeRecv = DateTime.Now.TimeOfDay.ToString();
                if (IsOpen())
                {
                    try
                    {
                        if (comportBuffer.IsTCP)
                        {
                            vLength = stm.ReadByte();
                            if (vLength < 0)
                            {
                                byteRecv = new byte[1];
                            }
                            else
                            {
                                byteRecv = new byte[vLength];
                                stm.Read(byteRecv, 0, byteRecv.Length);
                            }
                            dataRecv = ASCIIEncoding.UTF8.GetString(byteRecv);
                        }
                        else
                        {
                            int OldByteToRead = -1;
                            int NewByteToRead = 0;
                            int i = 0;
                            while ((OldByteToRead < NewByteToRead) && i <= 30)
                            {
                                Thread.Sleep(10);
                                i++;
                                if (i > 2)
                                {
                                    if (NewByteToRead != Sp.BytesToRead)
                                        NewByteToRead = Sp.BytesToRead;
                                    else
                                        OldByteToRead = NewByteToRead;
                                }
                            }
                            //create array to store buffer data
                            byteRecv = new byte[NewByteToRead];

                            //read the data and store
                            Sp.Read(byteRecv, 0, NewByteToRead);
                            int index = Array.IndexOf<byte>(byteRecv, 2);
                            dataRecv = Encoding.ASCII.GetString(byteRecv,index,byteRecv.Length-index);
                            Sp.DiscardInBuffer();
                        }
                    }
                    catch (Exception exp)
                    {
                        CheckResponse();
                        byteRecv = new byte[1];
                        dataRecv = Encoding.ASCII.GetString(byteRecv);
                    }
                }
                else
                {
                    CheckResponse();
                    byteRecv = new byte[1];
                    dataRecv = Encoding.ASCII.GetString(byteRecv);
                }
            }
            return dataRecv;
        }

        public void ReceiveData(ref byte[] pByteRecv)
        {
            //Thread.Sleep(70);
            int vLength;
            pByteRecv = new byte[1];
            TimeRecv = DateTime.Now.TimeOfDay.ToString();
            if (IsOpen())
            {
                try
                {
                    if (comportBuffer.IsTCP)
                    {
                        vLength = stm.ReadByte();
                        if (vLength < 0)
                        {
                            byteRecv = new byte[1];
                        }
                        else
                        {
                            stm.Read(byteRecv, 0, byteRecv.Length);
                            pByteRecv = new byte[byteRecv.Length];
                            Array.Copy(byteRecv, pByteRecv, byteRecv.Length);
                        }
                        dataRecv = ASCIIEncoding.UTF8.GetString(byteRecv);
                    }
                    else
                    {
                        //mRecv = mSp.ReadExisting();
                        //mSp.DiscardInBuffer();
                        //var copy = DataReceived;
                        //if (copy != null) copy(recv);
                        //no. of data at the port
                        int ByteToRead = Sp.BytesToRead;

                        //create array to store buffer data
                        pByteRecv = new byte[ByteToRead];

                        //read the data and store
                        Sp.Read(byteRecv, 0, ByteToRead);
                        pByteRecv = new byte[byteRecv.Length];
                        Array.Copy(byteRecv, pByteRecv, byteRecv.Length);
                        //vRecv = Encoding.ASCII.GetString(inputData);
                        dataRecv = ASCIIEncoding.UTF8.GetString(byteRecv);
                        Sp.DiscardInBuffer();
                    }
                }
                catch (Exception exp)
                {
                    CheckResponse();
                    byteRecv = pByteRecv;
                    //vRecv = "";
                }
            }
            else
            {
                CheckResponse();
                byteRecv = pByteRecv;
            }
        }

        public void SendDataTransaction(string pCmd, ref byte[] pRevcByte)
        {
            byte[] vSend;
            pRevcByte = new byte[1];
            lock (thrLock)
            {
                if (IsOpen())
                {
                    try
                    {
                        TimeSend = DateTime.Now.TimeOfDay.ToString();
                        if (comportBuffer.IsTCP)
                        {
                            byteSend = new byte[pCmd.Length * sizeof(char)];
                            System.Buffer.BlockCopy(pCmd.ToCharArray(), 0, byteSend, 0, byteSend.Length);
                            stm.Write(byteSend, 0, byteSend.Length);
                        }
                        else
                        {
                            Sp.Write(pCmd);
                        }
                        Thread.Sleep(500);
                        TimeRecv = DateTime.Now.TimeOfDay.ToString();

                        ReceiveData(ref pRevcByte);
                    }
                    catch (Exception exp)
                    {
                        CheckResponse();
                    }
                }
            }
        }

        private string SendData(string pData)
        {
            byte[] vSend;
            TimeSend = DateTime.Now.TimeOfDay.ToString();
            dataSend = pData;
            byteSend = Encoding.ASCII.GetBytes(pData);
            lock (thrLock)
            {
                if (IsOpen())
                {
                    try
                    {
                        //Thread.Sleep(CalculateSilentTime(iMsg.Length));
                        TimeSend = DateTime.Now.TimeOfDay.ToString();
                        if (comportBuffer.IsTCP)
                        {
                            byteSend = new byte[pData.Length * sizeof(char)];
                            System.Buffer.BlockCopy(pData.ToCharArray(), 0, byteSend, 0, byteSend.Length);
                            stm.Write(byteSend, 0, byteSend.Length);
                        }
                        else
                        {
                            Sp.Write(pData);
                        }
                        Thread.Sleep(300);
                        Thread.Sleep(CalculateSilentTime(pData.Length));
                        //ReceiveData();
                        //TimeRecv = DateTime.Now.TimeOfDay.ToString();

                        //Thread.Sleep(200);
                        //Thread.Sleep(CalculateSilentTime(mRecv.Length));
                    }
                    catch (Exception exp)
                    {
                        CheckResponse();
                        return "";
                    }
                }
            }
            return dataRecv;
        }

        public void SendData(byte[] pData)
        {
            //Application.DoEvents();
            byte[] vSend = null;
            TimeSend = DateTime.Now.TimeOfDay.ToString();
            dataSend = objEncoding.GetString(pData);
            byteSend = pData;
            lock (thrLock)
            {
                if (IsOpen())
                {
                    try
                    {
                        if (comportBuffer.IsTCP)
                        {
                            Array.Copy(pData, vSend, pData.Length);
                            System.Buffer.BlockCopy(pData, 0, vSend, 0, pData.Length);
                        }
                        else
                        {
                            Thread.Sleep(100);
                            //mSp.DiscardOutBuffer();
                            //dataSend = Encoding.ASCII.GetString(pData);
                            Sp.Write(pData, 0, pData.Length);
                        }
                    }
                    catch (Exception exp)
                    {
                        CheckResponse();
                    }
                }
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
            return;
            DateTime vDateTime = DateTime.Now;
            var vDiff = (vDateTime - datetimeResponse).TotalSeconds;

            if ((vDiff > 5) && (comportResponse == true))
            {
                datetimeResponse = DateTime.Now;
                comportResponse = false;
                if (comportBuffer.IsOpen)
                {
                    CloseComPort();
                    StartThread();
                }
            }
        }

    }
}
