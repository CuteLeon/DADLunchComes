using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Vision.Motion;

namespace DADLunchComes
{
    public partial class MotionForm : Form
    {
        /*
         * DOC:
         * http://www.aforgenet.com/framework/features/motion_detection_2.0.html
         */

        //忽略警报事件，防止瞬间多次触发警报
        private bool IgnoreAlert = false;

        //摄像头
        private FilterInfoCollection VideoDevicesList;
        private IVideoSource VideoSource;

        //运动监视
        private MotionDetector LunchDetector;
        private readonly IMotionDetector motionDetector = new TwoFramesDifferenceDetector();//new SimpleBackgroundModelingDetector()
        private readonly IMotionProcessing motionProcessing = new MotionAreaHighlighting();//new MotionAreaHighlighting()

        public MotionForm()
        {
            this.InitializeComponent();
        }

        private void MotionForm_Shown(object sender, EventArgs e)
        {
            this.AppendLog("程序已加载...");
            try
            {
                this.AppendLog("正在读取摄像头设备列表...");
                //初始化摄像头设备
                this.VideoDevicesList = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                this.AppendLog("使用默认摄像头设备...");
                //使用默认设备
                this.VideoSource = new VideoCaptureDevice(this.VideoDevicesList[0].MonikerString);
                //绑定事件
                this.AppendLog("注册警报事件");
                this.VideoSource.NewFrame += new NewFrameEventHandler(this.Alert);
                this.AppendLog("正在启动摄像头...");
                //启动摄像头
                this.VideoSource.Start();
            }
            catch (Exception ex)
            {
                this.AppendLog("无法连接或启动摄像头，程序即将退出...\n{0}", ex.Message);
                MessageBox.Show(string.Format("无法连接摄像头:\n{0}", ex.Message));
                Application.Exit();
            }
            this.AppendLog("设备初始化完成！开始监视...");

            //运动监视
            this.LunchDetector = new MotionDetector(this.motionDetector, this.motionProcessing);
            this.AppendLog("运行监视创建完成...");
            this.AppendLog("————————————");
        }

        private void MotionForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                this.VideoSource.SignalToStop();//关闭摄像头
                this.AppendLog("关闭摄像头...");
                Application.Exit();
            }
            catch (Exception ex)
            {
                this.AppendLog("关闭摄像头遇到错误：\n{0}", ex.Message);
            }
        }

        private void AppendLog(string LogMessage, params object[] LogValues)
        {
            this.AppendLog(string.Format(LogMessage, LogValues));
        }

        private void AppendLog(string LogMessage)
        {
            Debug.Print(string.Format("{0} : {1}", DateTime.Now.ToString(), LogMessage));

            this.Invoke(new Action(delegate
            {
                //LogLabel.Text += string.Format("{0} : {1}\n", DateTime.Now.ToString(), LogMessage);
                this.LogLabel.Text = string.Format("{0} : {1}\n", DateTime.Now.ToString(), LogMessage) + this.LogLabel.Text;
                this.LogLabel.Invalidate();
            }));
        }

        private void Alert(object sender, NewFrameEventArgs e)
        {
            this.BackgroundImage = e.Frame.Clone() as Image;
            float Result = this.LunchDetector.ProcessFrame(e.Frame.Clone() as Bitmap);
            if (Result > 0.0001)
            {
                if (this.IgnoreAlert)
                {
                    return;
                }

                this.IgnoreAlert = true;
                this.AppendLog("发现移动对象 " + Result.ToString());

                //TODO:触发监视警报
                Debug.Print(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " : " + Result.ToString());
                this.SendAlert(Result, new Bitmap(e.Frame, 640, 360));

                //解除警报事件，防止瞬间重复触发
                new Thread(new ThreadStart(delegate
                {
                    Thread.Sleep(3000);
                    this.IgnoreAlert = false;
                })).Start();
            }
            this.MotionPictureBox.BackgroundImage = (this.motionDetector as TwoFramesDifferenceDetector).MotionFrame.ToManagedImage();
            //GC.Collect();
        }

        private void SendAlert(float value, Bitmap Frame)
        {
            try
            {
                Socket AlertSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                AlertSocket.Connect("", 17417);
                AlertSocket.Send(Encoding.UTF8.GetBytes(string.Format(
                    "DAD_LUNCH={0}_FRAME={1}\n",
                    value,
                    this.BitmapToString(Frame)
                    )));
                AlertSocket?.Close();
                this.AppendLog("警报发送成功！");
            }
            catch (Exception ex)
            {
                this.AppendLog("发送警报出错：{0}", ex.Message);
            }
        }

        private string BitmapToString(Bitmap Frame)
        {
            MemoryStream memoryStream = new MemoryStream();
            Frame.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToBase64String(memoryStream.GetBuffer());
        }

    }
}
