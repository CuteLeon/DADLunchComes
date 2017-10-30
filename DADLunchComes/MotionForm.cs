using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Speech.Synthesis;

using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Vision.Motion;
using System.Diagnostics;
using System.Threading;
using System.Net.Sockets;
using System.IO;

namespace DADLunchComes
{
    public partial class MotionForm : Form
    {
        /*
         * DOC:
         * http://www.aforgenet.com/framework/features/motion_detection_2.0.html
         */
         
        //忽略警报事件，防止瞬间多次触发警报
        bool IgnoreAlert = false;
        //摄像头
        FilterInfoCollection VideoDevicesList ;
        IVideoSource VideoSource;
        //运动监视
        MotionDetector LunchDetector;

        IMotionDetector motionDetector = new TwoFramesDifferenceDetector();//new SimpleBackgroundModelingDetector()
        IMotionProcessing motionProcessing = new MotionAreaHighlighting();//new MotionAreaHighlighting()

        public MotionForm()
        {
            InitializeComponent();
        }

        private void MotionForm_Shown(object sender, EventArgs e)
        {   
            AppendLog("程序已加载...");
            try
            {
                AppendLog("正在读取摄像头设备列表...");
                //初始化摄像头设备
                VideoDevicesList = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                AppendLog("使用默认摄像头设备...");
                //使用默认设备
                VideoSource = new VideoCaptureDevice(VideoDevicesList[0].MonikerString);
                //绑定事件
                AppendLog("注册警报事件");
                VideoSource.NewFrame += new NewFrameEventHandler(Alert);
                AppendLog("正在启动摄像头...");
                //启动摄像头
                VideoSource.Start();
            }
            catch (Exception ex)
            {
                AppendLog("无法连接或启动摄像头，程序即将退出...\n{0}",ex.Message);
                MessageBox.Show(string.Format("无法连接摄像头:\n{0}",ex.Message));
                Application.Exit();
            }
            AppendLog("设备初始化完成！开始监视...");

            //运动监视
            LunchDetector = new MotionDetector(motionDetector,motionProcessing);
            AppendLog("运行监视创建完成...");
            AppendLog("————————————");
        }

        private void MotionForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                VideoSource.SignalToStop();//关闭摄像头
                AppendLog("关闭摄像头...");
                Application.Exit();
            }
            catch (Exception ex)
            {
                AppendLog("关闭摄像头遇到错误：\n{0}",ex.Message);
            }
        }

        private void AppendLog(string LogMessage, params object[] LogValues)
        {
            AppendLog(string.Format(LogMessage,LogValues));
        }

        private void AppendLog(string LogMessage)
        {
            Debug.Print(string.Format("{0} : {1}", DateTime.Now.ToString(), LogMessage));

            this.Invoke(new Action(delegate{
                //LogLabel.Text += string.Format("{0} : {1}\n", DateTime.Now.ToString(), LogMessage);
                LogLabel.Text = string.Format("{0} : {1}\n", DateTime.Now.ToString(), LogMessage) + LogLabel.Text;
                LogLabel.Invalidate();
            }));
        }

        private void Alert(object sender,NewFrameEventArgs e)
        {
            this.BackgroundImage = e.Frame.Clone() as Image;
            float Result = LunchDetector.ProcessFrame(e.Frame.Clone() as Bitmap);
            if ( Result > 0.0001)
            {
                if (IgnoreAlert) return;
                IgnoreAlert = true;
                AppendLog("发现移动对象 " + Result.ToString());

                //TODO:触发监视警报
                Debug.Print(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " : " + Result.ToString());
                SendAlert(Result, new Bitmap(e.Frame,640,360));

                //解除警报事件，防止瞬间重复触发
                new Thread(new ThreadStart(delegate {
                    Thread.Sleep(3000);
                    IgnoreAlert = false;
                })).Start();
            }
            MotionPictureBox.BackgroundImage = (motionDetector as TwoFramesDifferenceDetector).MotionFrame.ToManagedImage();
            //GC.Collect();
        }

        private void SendAlert(float value,Bitmap Frame)
        {
            try
            {
                Socket AlertSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                AlertSocket.Connect("", 17417);
                AlertSocket.Send(Encoding.UTF8.GetBytes(string.Format(
                    "DAD_LUNCH={0}_FRAME={1}\n",
                    value,
                    BitmapToString(Frame)
                    )));
                AlertSocket?.Close();
                AppendLog("警报发送成功！");
            }
            catch (Exception ex)
            {
                AppendLog("发送警报出错：{0}",ex.Message);
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
