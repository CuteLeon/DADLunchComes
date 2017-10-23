﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Vision.Motion;
using System.Diagnostics;
using System.Threading;

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
                AppendLog("注册摄像头刷新事件...");
                VideoSource.NewFrame += new NewFrameEventHandler((v,f)=> this.BackgroundImage = f.Frame.Clone() as Image );
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
            LunchDetector = new MotionDetector(
                new SimpleBackgroundModelingDetector(),
                new MotionAreaHighlighting()
            );
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
            if (LunchDetector.ProcessFrame(e.Frame.Clone() as Bitmap) > 0.01)
            {
                if (IgnoreAlert) return;
                IgnoreAlert = true;
                AppendLog("发现移动对象");
                
                //TODO:触发监视警报

                //解除警报事件，防止瞬间重复触发
                new Thread(new ThreadStart(delegate {
                    Thread.Sleep(1000);
                    IgnoreAlert = false;
                })).Start();
            }
            GC.Collect();
        }

    }
}