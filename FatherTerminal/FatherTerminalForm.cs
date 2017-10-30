using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FatherTerminal
{

    public partial class FatherTerminalForm : Form
    {
        private Socket ServerSocket;
        private Socket ClientSocket;
        private Thread ListenThread;
        private Thread ReceiveMessageThread = null;
        private Regex MessageRegex = new Regex("DAD_LUNCH=(?<LunchValue>.+?)_FRAME=(?<Frame>.+?)\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private Match MessageMatchResult;

        public FatherTerminalForm()
        {
            InitializeComponent();
        }

        private void FatherTerminalForm_Load(object sender, EventArgs e)
        {
            this.Location = new Point(Screen.PrimaryScreen.Bounds.Width-this.Width, (Screen.PrimaryScreen.Bounds.Height - this.Height) / 2);

            //创建服务端
            try
            {
                Debug.Print("正在尝试建立TCP协议服务端...");
                //初始化服务端Socket，绑定端口并监听
                ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                EndPoint TCPIPAndPort = new IPEndPoint(IPAddress.Any, 17417);
                ServerSocket.Bind(TCPIPAndPort);
                ServerSocket.Listen(1);
                Debug.Print("TCP协议服务端建立完成...\n正在监听请求...");
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法启动TCP协议监听！\n" + ex.Message + "\n程序即将退出...");
            }

            //开始监听连接请求
            ListenThread = new Thread(new ThreadStart(ListenClientConnect));
            ListenThread.Start();
        }

        /// <summary>
        /// 监听客户端连接
        /// </summary>
        void ListenClientConnect()
        {
            while (true)
            {
                try
                {
                    ClientSocket = ServerSocket.Accept();
                    ReceiveMessageThread?.Abort();
                    ReceiveMessageThread = new Thread(new ThreadStart(delegate { ReceiveClientMessage(ClientSocket); }));
                    ReceiveMessageThread.Start();

                    Debug.Print("同意 {0} 的请求，已开始接收消息...", ClientSocket.RemoteEndPoint.ToString());
                }
                catch (Exception ex)
                {
                    Debug.Print("创建Socket连接失败：{0}", ex.Message);
                }
            }
        }

        void ReceiveClientMessage(Socket ClientSocket)
        {
            while (true)
            {
                string ClientMessagePackage = "";
                try
                {
                    byte[] MessageBuffer = new byte[ClientSocket.ReceiveBufferSize - 1];
                    int MessageBufferSize = ClientSocket.Receive(MessageBuffer);
                    ClientMessagePackage = Encoding.UTF8.GetString(MessageBuffer, 0, MessageBufferSize);
                }
                catch (ThreadAbortException) { return; }
                catch (Exception ex)
                {
                    Debug.Print("接收客户端消息时遇到错误：{0}", ex.Message);
                    return;
                }

                try
                {
                    Debug.Print("收到客户端发送来的数据：{0}", ClientMessagePackage);
                    if (string.IsNullOrEmpty(ClientMessagePackage)) continue;

                    ProcessMessage(ClientMessagePackage);
                }
                catch (ThreadAbortException) { return; }
                catch (Exception ex)
                {
                    Debug.Print("分析并回应用户消息时发生错误：{0}", ex.Message);
                }

            }
        }

        private void ProcessMessage(string MessageData)
        {
            try
            {
                MessageMatchResult = MessageRegex.Match(MessageData);
                string LunchValue = MessageMatchResult.Groups["LunchValue"].Value;
                string Frame = MessageMatchResult.Groups["Frame"].Value;

                this.Invoke(new Action(()=> {
                    Speak();
                    this.BackgroundImage = StringToBitmap(Frame);
                    this.Activate();
                    Vibration(this);
                }));
            }
            catch (Exception ex)
            {
                Debug.Print("解析消息遇到错误：{0}", ex.Message);
            }
        }

        private Bitmap StringToBitmap(string FrameData)
        {
            try
            {
                return Bitmap.FromStream(new MemoryStream(Convert.FromBase64String(FrameData))) as Bitmap;
            }
            catch (Exception ex)
            {
                Debug.Print("Base64转换为图像时遇到错误：" + ex.Message);
                return new Bitmap(0, 0);
            }
        }

        private void Vibration(Form sender, int Radius = 2, int Count = 3)
        {
            Point InitPoint = sender.Location;
            sender.Refresh();
            int X = 0, Y = 0;
            for (int VibrationIndex = 0; VibrationIndex < Count; VibrationIndex++)
            {
                for (int Index = -Radius; Index < Radius; Index++)
                {
                    X = Convert.ToInt32(Math.Sqrt(Radius * Radius - Index * Index));
                    sender.Location = new Point(InitPoint.X + X, InitPoint.Y + Index);
                    Thread.Sleep(10);
                }
                for (int Index = Radius; Index > -Radius; Index--)
                {
                    X = Convert.ToInt32(Math.Sqrt(Radius * Radius - Index * Index));
                    sender.Location = new Point(InitPoint.X - X, InitPoint.Y + Y);
                    Thread.Sleep(10);
                }
            }
            sender.Location = InitPoint;
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            this.Close();
            Application.Exit();
        }

        private void FatherTerminalForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ClientSocket?.Close();
            ServerSocket?.Close();
            ReceiveMessageThread?.Abort();
            ListenThread?.Abort();
        }

        private void FatherTerminalForm_Click(object sender, EventArgs e)
        {
            
        }
        private void Speak()
        {
            //using (SpeechSynthesizer Speaker = new SpeechSynthesizer())
            SpeechSynthesizer Speaker = new SpeechSynthesizer();
            Speaker.SpeakCompleted += new EventHandler<SpeakCompletedEventArgs>((s, e) => { (s as SpeechSynthesizer).Dispose(); });
            Speaker.SpeakAsync("爸爸，吃饭！");
        }


    }
}
