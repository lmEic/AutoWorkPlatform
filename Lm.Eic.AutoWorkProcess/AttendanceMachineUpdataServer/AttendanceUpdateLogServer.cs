using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Lm.Eic.AutoWorkProcess.AttendanceMachineUpdataServer
{
    /// <summary>
    /// 时间日志回调
    /// </summary>
    /// <returns></returns>

    public delegate void PingCallback
         (String TerminalType, Int32 TerminalID, String SerialNumber, Int32 TransactionID);
    public delegate Boolean AlarmLogCallback
        (String TerminalType, Int32 TerminalID, String SerialNumber, Int32 TransactionID,
        DateTime LogTime, Int64 UserID, Int32 DoorID,
        String AlarmType);
    public delegate Boolean AdminLogCallback
        (String TerminalType, Int32 TerminalID, String SerialNumber, Int32 TransactionID,
        DateTime LogTime, Int64 AdminID, Int64 UserID,
        String Action,
        Int32 Result);
    public delegate Boolean TimeLogCallback
        (String TerminalType, Int32 TerminalID, String SerialNumber, Int32 TransactionID,
        DateTime LogTime, Int64 UserID, Int32 DoorID,
        String AttendanceStatus,
        String VerifyMode,
        Int32 JobCode,
        String Antipass,
        Byte[] Photo);
    public class AttendanceUpdateLogServer
    {

        public Boolean m_Disposed;
        public UInt16 m_PortNo;
        public TcpListener m_Listner;
        static LinkedList<AttendanceUpdateTerminal> m_TerminalList = new LinkedList<AttendanceUpdateTerminal>();

        public TimeLogCallback m_TimeLogCallBack = null;
        public AdminLogCallback m_AdminLogCallBack = null;
        public AlarmLogCallback m_AlarmLogCallBack = null;
        public PingCallback m_PingCallBack = null;

        public AttendanceUpdateLogServer(UInt16 portNo,
            TimeLogCallback timeLogCallback,
            AdminLogCallback adminLogCallback,
            AlarmLogCallback alarmLogCallback,
            PingCallback pingCallback)
        {
            // Initialize objects.
            m_Disposed = false;
            m_PortNo = portNo;
            m_TimeLogCallBack = timeLogCallback;
            m_AdminLogCallBack = adminLogCallback;
            m_AlarmLogCallBack = alarmLogCallback;
            m_PingCallBack = pingCallback;

            // Start TCP Listner.
            m_Listner = new TcpListener(IPAddress.Any, m_PortNo);
            m_Listner.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            m_Listner.Start();

            // Begin Accept.
            m_Listner.BeginAcceptTcpClient(new AsyncCallback(AttendanceUpdateLogServer.OnAccept), this);
        }

        ~AttendanceUpdateLogServer()
        {
            CleanUp(false);
        }

        private void CleanUp(bool dispose)
        {
            if (m_Disposed)
                return;

            m_Disposed = true;

            if (dispose)
            {
                // Dispose the listener and terminals.
                try
                {
                    m_Listner.Stop();
                    foreach (AttendanceUpdateTerminal e in m_TerminalList)
                    {
                        if (e != null)
                            e.Dispose();
                    }
                }
                catch
                {

                }
            }
        }

        public void Dispose()
        {
            CleanUp(true);
        }

        public static void OnAccept(IAsyncResult iar)
        {
            AttendanceUpdateLogServer server = (AttendanceUpdateLogServer)iar.AsyncState;
            AttendanceUpdateTerminal term = new AttendanceUpdateTerminal(server.m_TimeLogCallBack,
                server.m_AdminLogCallBack,
                server.m_AlarmLogCallBack,
                server.m_PingCallBack);

            try
            {
                // Establish connection and add a terminal into the list.
                term.EstablishConnect(server.m_Listner.EndAcceptTcpClient(iar));
                m_TerminalList.AddLast(term);
            }
            catch
            {
                term.Dispose();
            }

            try
            {
                // For disposed listener.
                server.m_Listner.BeginAcceptTcpClient(new AsyncCallback(AttendanceUpdateLogServer.OnAccept), server);
            }
            catch
            {

            }
        }
    }
}
