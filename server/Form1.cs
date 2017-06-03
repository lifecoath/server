using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using AForge;
using AForge.Video.DirectShow;
using System.Drawing.Imaging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace server
{
    public partial class Form1 : Form
    {
        private Socket socket;

        #region 图像处理

        private Socket ImageServer;
        private IPEndPoint IPImageServer;
        private Thread ImageSendThread;
        private Thread ImageRecThread;
        private Thread TcpListen;
        private byte draw;
        private FilterInfoCollection videoDevices;
        private string[] CameraName = new string[2];
        #endregion

        #region 事件处理
        private Socket EventSocket;
        private IPEndPoint IpServer = null;
        private IPEndPoint IpClient = null;
        private string IpAdd = string.Empty;
        private EndPoint ServerPoint;
        private EndPoint ClientPoint;
        private Thread EventThread;

        #endregion
        #region 其他
        private string UserName = string.Empty;
        private string Message = string.Empty;
        private string InfoType = string.Empty;
        #endregion
        public Form1()
        {
            InitializeComponent();

        }
        #region 连接
        private void SetImageTcp()
        {
            IPImageServer = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1111);
            socket=new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(IPImageServer);
            socket.Listen(10);

            TcpListen = new Thread(listen);
            TcpListen.IsBackground = true;
            TcpListen.Start();

        }
        private void listen()
        {
            ImageServer = socket.Accept();
            byte[] words = new byte[1024];
            int Byte = ImageServer.Receive(words);

            MessageBox.Show(Encoding.ASCII.GetString(words, 0, Byte));
        }
        private void SetEventUDP()
        {
            IpServer = new IPEndPoint(IPAddress.Any,1000);
            ServerPoint = (EndPoint)IpServer;
            EventSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            EventSocket.Bind(IpServer);

            EventThread = new Thread(Accepting);
            EventThread.IsBackground = true;
            EventThread.Start();

        }
        private void Accepting()
        {
            while (true)
            {
                EndPoint point = new IPEndPoint(IPAddress.Any, 0);
                byte[] RecvByte = new byte[256];
                EventSocket.Blocking = true;
                EventSocket.ReceiveFrom(RecvByte, ref point);


                JObject json = JObject.Parse(Encoding.UTF8.GetString(RecvByte));

                InfoType = json.SelectToken("InfoType").ToString();
                
                if (InfoType == "parameter")
                {
                    if (IpAdd == string.Empty)//第一次连接
                    {
                        IpAdd = json.SelectToken("IpClient").ToString();
                        UserName = json.SelectToken("UserName").ToString();

                        IpClient = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1001);
                        ClientPoint = (EndPoint)IpClient;

                        AnswerReq(UserName, ClientPoint);
                    }
                }
              
                else
                {
                    Message = json.SelectToken("Message").ToString();
                    ShowInfomation(Message);
                    //MessageBox.Show(Message);
                }
            }
             
        }
        private void ShowInfomation(string info)
        {
            Thread ShowText = new Thread(new ParameterizedThreadStart(UpdaterichTextBox_message));
            ShowText.Start(info);
        }
        private void UpdaterichTextBox_message(object str)
        {
            if (richTextBox_message.InvokeRequired)
            {
                Action<string> actionDelegate = (x) => { this.richTextBox_message.AppendText(x.ToString()); };
                this.richTextBox_message.Invoke(actionDelegate, str);
            }
            else
            {
                this.richTextBox_message.AppendText(str.ToString());
            }
        }
        /// <summary>
        /// 发送消息
        /// </summary>
        private void SendMessage(string info,EndPoint Point)
        {
            JObject json = new JObject();
            json.Add("InfoType", "message");
            json.Add("Message", info);
            EventSocket.SendTo(Encoding.UTF8.GetBytes(json.ToString()), Point);

        }
        /// <summary>
        /// 回复客户端请求
        /// </summary>
        /// <param name="user"></param>
        /// <param name="Point"></param>
        private void AnswerReq(string user,EndPoint Point)
        {
            JObject AnswerJson = new JObject();

            MessageBoxButtons messButton = MessageBoxButtons.OKCancel;
            DialogResult dr = MessageBox.Show("客户"+user+"请求连接，是否同意？", "连接请求", messButton);
            if (dr == DialogResult.OK)//如果点击“确定”按钮
            {
                AnswerJson.Add("InfoType", "parameter");
                AnswerJson.Add("Answer", "yes");
                EventSocket.SendTo(Encoding.UTF8.GetBytes(AnswerJson.ToString()), Point);
                SetImageTcp();
            }
            else//如果点击“取消”按钮
            {
                AnswerJson.Add("InfoType", "parameter");
                AnswerJson.Add("Answer", "no");
                EventSocket.SendTo(Encoding.UTF8.GetBytes(AnswerJson.ToString()), Point);
            }
        }
        #endregion
      
        /// <summary>
        /// 视频通话
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void camera_Click(object sender, EventArgs e)
        {

            loadcamera();

            ImageSendThread = new Thread(new ThreadStart(OpenCamrea));
            ImageSendThread.IsBackground = true;
            ImageSendThread.Start();

            ImageRecThread = new Thread(new ThreadStart(getimg));
            ImageRecThread.IsBackground = true;
            ImageRecThread.Start();
        }
        /// <summary>
        /// 读取摄像头
        /// </summary>
        private void loadcamera()
        {
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            for (int i = 0; i < 2; i++)
            {
                CameraName[i] = videoDevices[i].MonikerString;
            }
        }
        /// <summary>
        /// 打开摄像头
        /// </summary>
        private void OpenCamrea()
        {
            VideoCaptureDevice videoSource = new VideoCaptureDevice(CameraName[0]);
            videoSourcePlayer1.VideoSource = videoSource;
            videoSourcePlayer1.Start();
        }
        private void videoSourcePlayer1_NewFrame(object sender, ref Bitmap image)
        {
            fuction(image);
        }
        private void fuction(Bitmap bitmap)
        {
            byte[] data;
            MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Jpeg);
            data = new byte[ms.Length + 1];
            ms.Position = 0;
            data[0] = draw;
            ms.Read(data, 1, data.Length - 1);//读取数据流
            Send(data);//发送流
        }
        public void Send(byte[] data)//发送函数
        {
            int i, l;
            byte[] value, temp = data;
            l = temp.Length;
            value = new byte[l + 4];
            for (i = 0; i < l; i++)
                value[i + 4] = (byte)((temp[i] + i) % 256);
            for (i = 3; i >= 0; i--)
            {
                value[i] = (byte)(l % 256);
                l /= 256;
            }
            ImageServer.Send(value);

        }
        byte[] Receive()//接收函数
        {
            byte[] data = new byte[4];
            int i, size = 0;
            for (i = ImageServer.Receive(data); i < 4; i = i + ImageServer.Receive(data, i, 4 - i, SocketFlags.None)) Thread.Sleep(10);//获取四字节头
            for (i = 0; i < 4; i++) size = size * 256 + data[i];//计算分组大小
            data = new byte[size];
            for (i = 0; i < size; i = i + ImageServer.Receive(data, i, size - i, SocketFlags.None)) Thread.Sleep(10);//读取流
            for (i = 0; i < size; i++) data[i] = (byte)((data[i] - i) % 256);//解密
            return data;
        }
        private void getimg()
        {
            try
            {
                byte[] data;
                MemoryStream stream;
                while (true)
                {
                    data = Receive();
                    if (data.Length > 0 && data[0] == draw)
                    {
                        stream = new MemoryStream(data, 1, data.Length - 1);//还原图像的编码流                 
                        pictureBox1.Image = Image.FromStream(stream);
                    }
                }
            }
            catch (Exception e1)
            {

                MessageBox.Show(e1.ToString());
            }
        }
        /// <summary>
        /// 连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            SetEventUDP();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            JObject json = new JObject();
            json.Add("InfoType", "Info");
            json.Add("Message", "accpet");
            EventSocket.SendTo(Encoding.UTF8.GetBytes(json.ToString()), ClientPoint);
        }

        private void button_Send_Click(object sender, EventArgs e)
        {
            DateTime time = DateTime.Now;
            String InputInfo = string.Empty;
            InputInfo = time + "\r\n" + textBox_Input.Text+"\r\n";
            richTextBox_message.AppendText(InputInfo);

            SendMessage(InputInfo,ClientPoint);
        }
    }
}
