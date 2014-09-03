using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocket4Net;
using System.IO;
using System.Net;
using Newtonsoft.Json;

namespace btcchina_websocket_api
{
    class wsConfigHelper
    {
        public string sid { get; set; }
        public List<string> upgrades { get; set; }
        public int pingInterval { get; set; }
        public int pingTimeout { get; set; }
    }

    class Program
    {
        //v1.0 message types
        public enum engineioMessageType
        {
            OPEN = 0,//non-ws
            CLOSE = 1,//non-ws
            /*
             * Pings server every "pingInterval" and expects response
             * within "pingTimeout" or closes connection.
             * 
             * client sends ping, waiting for server's pong.
             * socket.io message type is not necessary in ping/pong.
             * 
             * client sends pong after receiving server's ping.
             */
            PING = 2,
            PONG = 3,

            MESSAGE = 4,//TYPE_EVENT in v0.9.x
            UPGRADE = 5, //new in v1.0
            NOOP = 6
        }
        public enum socketioMessageType
        {
            CONNECT = 0,//right after engine.io UPGRADE
            DISCONNECT = 1,
            EVENT = 2,
            ACK = 3,
            ERROR = 4,
            BINARY_EVENT = 5,
            BINARY_ACK = 6
        }
        //every transport between server and client is formatted as: "engine.io Message Type" + "socket.io Message Type" + json-encoded content.

        private static WebSocket btc;

        private static Timer pingIntervalTimer, pingTimeoutTimer;
        private static bool pong;

        static void Main(string[] args)
        {
            const string httpScheme = "https://";
            const string wsScheme = "wss://";
            const string url = "websocket.btcchina.com/socket.io/";

            #region handshake
            string polling = string.Empty;
            try
            {
                WebClient wc = new WebClient();
                polling = wc.DownloadString(httpScheme + url + "?transport=polling");
                if (string.IsNullOrEmpty(polling))
                {
                   Console.WriteLine("failed to download config");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            string config = polling.Substring(polling.IndexOf('{'), polling.IndexOf('}') - polling.IndexOf('{') + 1);
            wsConfigHelper wsc = JsonConvert.DeserializeObject<wsConfigHelper>(config);

            #endregion handshake

            //set timers
            pingTimeoutTimer = new Timer(_ =>
            {
                if (pong)
                {
                    pong = false; //waiting for another ping
                }
                else
                {
                    Console.WriteLine("Ping Timeout!");
                }
            }, null, Timeout.Infinite, Timeout.Infinite);

            pingIntervalTimer = new Timer(_ =>
            {
                btc.Send(string.Format("{0}", (int)engineioMessageType.PING));
                pingTimeoutTimer.Change(wsc.pingTimeout, Timeout.Infinite);
                pong = false;
            }, null, wsc.pingInterval, wsc.pingInterval);

            //setup websocket connections and events
            btc = new WebSocket(wsScheme + url + "?transport=websocket&sid=" + wsc.sid);
            btc.Opened += btc_Opened;
            btc.Error += btc_Error;
            btc.MessageReceived += btc_MessageReceived;
            btc.DataReceived += btc_DataReceived;
            btc.Closed += btc_Closed;
            btc.Open();

            Console.ReadKey();

            //close the connection.
            btc.Send(string.Format("{0}{1}", (int)engineioMessageType.MESSAGE, (int)socketioMessageType.DISCONNECT));

            Console.ReadKey();
            
            pingIntervalTimer.Dispose();
            pingTimeoutTimer.Dispose();
            btc.Close();
        }

        static void btc_Closed(object sender, EventArgs e)
        {
            Console.WriteLine("websocket closed.");
        }

        static void btc_DataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("data:"+e.Data.ToString());
        }

        static void btc_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            int eioMessageType = -1;
            if (int.TryParse(e.Message.Substring(0, 1), out eioMessageType))
            {
                switch ((engineioMessageType)eioMessageType)
                {
                    case engineioMessageType.PING:
                        //replace incoming PING with PONG in incoming message and resend it.
                        btc.Send(string.Format("{0}{1}", (int)engineioMessageType.PONG, e.Message.Substring(1, e.Message.Length - 1)));
                        break;
                    case engineioMessageType.PONG:
                        pong = true;
                        break;

                    case engineioMessageType.MESSAGE:
                        int sioMessageType = -1;
                        if (int.TryParse(e.Message.Substring(1, 1), out sioMessageType))
                        {
                            switch ((socketioMessageType)sioMessageType)
                            {
                                case socketioMessageType.CONNECT:
                                    //Send "42["subscribe",["marketdata_cnybtc","marketdata_cnyltc","marketdata_btcltc"]]"
                                    btc.Send(string.Format("{0}{1}{2}", (int)engineioMessageType.MESSAGE,
                                                                       (int)socketioMessageType.EVENT,
                                                                       "[\"subscribe\",[\"marketdata_cnybtc\",\"marketdata_cnyltc\",\"marketdata_btcltc\"]]"));
                                    break;
                                case socketioMessageType.EVENT:
                                    if (e.Message.Substring(4, 5) == "trade")//listen on "trade"
                                        Console.WriteLine(e.Message.Substring(e.Message.IndexOf('{'), e.Message.LastIndexOf('}') - e.Message.IndexOf('{') + 1));
                                    break;
                                default:
                                    Console.WriteLine("error switch socket.io messagetype:" + e.Message);
                                    break;
                            }
                        }
                        else
                        {
                            Console.WriteLine("error parse socket.io messagetype!");
                        }
                        break;

                    default:
                        Console.WriteLine("error switch engine.io messagetype");
                        break;
                }
            }
            else
            {
                Console.WriteLine("error parsing engine.io messagetype!");
            }
        }

        static void btc_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            Console.WriteLine("error:" + e.Exception.Message);
        }

        static void btc_Opened(object sender, EventArgs e)
        {
            //send upgrade message:"52"
            //server responses with message: "40" - message/connect
            Console.WriteLine("opened.");
            btc.Send(string.Format("{0}{1}",(int)engineioMessageType.UPGRADE,(int)socketioMessageType.EVENT));
        }
    }
}
