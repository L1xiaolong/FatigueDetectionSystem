using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms.DataVisualization.Charting;

using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;

using System.Data.SqlClient;


namespace FatigueDetection_1._0
{   
    
    public partial class mainForm : Form
    {
        //=============全局变量start========================

        private VideoCapture capture = null;
        public Mat frame;
        public struct faceDetection
        {
            public Mat srcImage;
            public Mat faceRoi;
            public int coor_x;
            public int coor_y;
            public bool FLAG;
        };
        public struct eyeDetection
        {
            public Mat faceImage;
            public Mat eyeRoi;
            public bool FLAG;
        };
        public struct EllipseEye
        {
            public Mat res;
            public float width;
            public float height;
            public bool FLAG;
        };

        public Mat resShowImage;
        public Mat eyeROI;
        public bool eyeFLAG;
        public Mat faceRectImage;
        public Mat faceROI;
        public bool faceFLAG;
        public int coor_x;
        public int coor_y;
        public bool flag;   // 是否戴眼镜，true为不带，false为带

        public int nHeadPosture = 0;
        public float PERCLOS;
        public bool ellipseFLAG;

        public float eyeOpenDegree;
        public float mouthOpenDegree;

        public CascadeClassifier cascadeFace;
        public CascadeClassifier cascadeEye;
        public CascadeClassifier cascadeEyeWithGlasses;

        public const string cascadeNameFace = "haarcascade_frontalface_alt2.xml";
        public const string cascadeNameEyeWithGlasses = "haarcascade_eye_tree_eyeglasses.xml";
        public const string cascadeNameEyeNoGlasses = "haarcascade_eye.xml";

        Timer chartTimer = new Timer();
        List<float> eye = new List<float>(new float[20]);
        TimeSpan ts;

        //===================全局变量end=======================================================

        public mainForm()
        {
            cascadeFace = new CascadeClassifier(cascadeNameFace);
            cascadeEye = new CascadeClassifier(cascadeNameEyeNoGlasses);
            cascadeEyeWithGlasses = new CascadeClassifier(cascadeNameEyeWithGlasses);
            
            InitializeComponent();
            

        }
        
        private void startButton_Click(object sender, EventArgs e)
        {
            InitChart();
            capture = new VideoCapture();
            capture.SetCaptureProperty(CapProp.FrameHeight, 240);
            capture.SetCaptureProperty(CapProp.FrameWidth, 320);

            capture.ImageGrabbed += ProcessFrame;

            frame = new Mat();
            if (capture != null)
                capture.Start();         
        }
        private void ProcessFrame(object sender, EventArgs e)
        {
            DateTime start = System.DateTime.Now;

            if (capture != null && capture.Ptr != IntPtr.Zero)
            {
                
                capture.Retrieve(frame, 0);
                resShowImage = process(frame, flag);
                imageBox.Image = resShowImage;      //imageBox显示控件
            }

            DateTime end = System.DateTime.Now;
            ts = end.Subtract(start);

            textBox1.Text = Convert.ToInt32(ts.TotalMilliseconds).ToString();   // 算法速度
        }

        public Mat process(Mat image, bool flag)
        {
            // 人脸检测 START...

            flag = glassCheckBox.Checked ? false : true;

            faceDetection face = new faceDetection();

            face = facedetection(image, cascadeFace);
            faceFLAG = face.FLAG;
            faceRectImage = face.srcImage;
            faceROI = face.faceRoi;
            coor_x = face.coor_x;
            coor_y = face.coor_y;

            // 人脸检测 END...
            // 眼睛检测 START...

            if (faceFLAG)
            {
                if (flag)          // 不戴眼镜
                {
                    eyeDetection eye = eyedetection(faceROI, cascadeEye, faceRectImage, coor_x, coor_y);
                    resShowImage = eye.faceImage;
                    eyeROI = eye.eyeRoi;
                    eyeFLAG = eye.FLAG;
                }
                else              // 戴眼镜
                {
                    eyeDetection eye = eyedetection(faceROI, cascadeEyeWithGlasses, faceRectImage, coor_x, coor_y);
                    resShowImage = eye.faceImage;
                    eyeROI = eye.eyeRoi;
                    eyeFLAG = eye.FLAG;
                }

                // 眼睛检测 END...
                // 椭圆拟合 START...

                if (eyeFLAG)    // 检测到眼睛
                {
                    CvInvoke.Resize(eyeROI, eyeROI, new Size(24, 24));
                    EllipseEye e = eyeEllipse(eyeROI);
                    if (e.FLAG)        // 椭圆拟合完成
                    {
                        eyeOpenDegree = e.width / e.height;
                        
                        if (eye.Count == 20)
                        {
                            //eye.ToArray();
                            eye.Reverse();
                            eye.RemoveAt(19);
                            eye.Reverse();
                            
                        }
                        eye.Add(eyeOpenDegree);
                        
                    }
                    else        // 椭圆拟合未完成
                    {
                        eyeOpenDegree = 0.1F;
                        
                        if (eye.Count == 20)
                        {
                            //eye.ToArray();
                            eye.Reverse();
                            eye.RemoveAt(19);
                            eye.Reverse();
                        }
                            
                        eye.Add(eyeOpenDegree);
                        
                    }
                }
                else     // 未检测到眼睛
                {
                    eyeOpenDegree = 0.1F;
                    
                    if (eye.Count == 20)
                    {
                        //eye.ToArray();
                        eye.Reverse();
                        eye.RemoveAt(19);
                        eye.Reverse();
                    }
                    eye.Add(eyeOpenDegree);
                    

                }
            }
            else   // 未检测到人脸
            {
                nHeadPosture++;
            }

            return resShowImage;
        }

/////////////////////////////////////////////////////////////////////////////////////
        public faceDetection facedetection(Mat image, CascadeClassifier cascade)
        {
            faceDetection face = new faceDetection();
            Mat tmp = new Mat();
            image.CopyTo(tmp);
            Mat grayImage = new Mat();
            List<Rectangle> faces = new List<Rectangle>();
            CvInvoke.CvtColor(tmp, grayImage, ColorConversion.Bgr2Gray);
            CvInvoke.EqualizeHist(grayImage, grayImage);     // 均衡化

            Rectangle[] facedetected = cascade.DetectMultiScale(grayImage, 1.1, 3, new Size(80, 80));
            faces.AddRange(facedetected);
            int rect_x, rect_y, rect_width, rect_height;

            if (faces.Count > 0)
            {       
                rect_x = faces[0].X;
                rect_y = faces[0].Y;
                rect_width = faces[0].Width;
                rect_height = faces[0].Height;

                CvInvoke.Rectangle(tmp, faces[0], new Bgr(Color.Red).MCvScalar, 1);

                face.faceRoi = new Mat(image, faces[0]);
                face.coor_x = rect_x;
                face.coor_y = rect_y;
                face.srcImage = tmp;
                face.FLAG = true;

                return face;              
            }
            else
            {
                face.srcImage = tmp;
                face.FLAG = false;

                return face;
            }
        }
/////////////////////////////////////////////////////////////////////////////////////////
        public eyeDetection eyedetection(Mat image, CascadeClassifier cascade, Mat faceRectImage, int coor_x, int coor_y)
        {
            eyeDetection eye = new eyeDetection();
            Mat tmp = new Mat();
            image.CopyTo(tmp);
            Mat grayImage = new Mat();
            List<Rectangle> eyes = new List<Rectangle>();

            CvInvoke.CvtColor(tmp, grayImage, ColorConversion.Bgr2Gray);
            CvInvoke.EqualizeHist(grayImage, grayImage);     // 均衡化

            Rectangle[] eyedetected = cascade.DetectMultiScale(grayImage, 1.1, 3, new Size(20, 20));
            eyes.AddRange(eyedetected);

            int rect_x, rect_y, rect_width, rect_height;

            if (eyes.Count > 0)
            {
                rect_x = eyes[0].X;
                rect_y = eyes[0].Y;
                rect_width = eyes[0].Width;
                rect_height = eyes[0].Height;

                CvInvoke.Rectangle(faceRectImage, new Rectangle(rect_x + coor_x, rect_y + coor_y, rect_width, rect_height), new Bgr(Color.Blue).MCvScalar, 1);

                eye.eyeRoi = new Mat(image, eyes[0]);
                eye.faceImage = faceRectImage;
                eye.FLAG = true;

                return eye;
            }
            else
            {
                eye.faceImage = faceRectImage;
                eye.FLAG = false;
                return eye;
            }
        }
        /////////////////////////////////////////////////////////////////////////

        public EllipseEye eyeEllipse(Mat image)
        {
            Mat tmp = new Mat();
            image.CopyTo(tmp);

            EllipseEye e = new EllipseEye();
            Mat hsv = new Mat();
            CvInvoke.CvtColor(tmp, hsv, ColorConversion.Bgr2Hsv);

            VectorOfMat m = new VectorOfMat();
            CvInvoke.Split(hsv, m);

            OutputArray channels = m.GetOutputArray();
            Mat H_channel = new Mat();
            H_channel = channels.GetMat(0);

            Mat dst = new Mat();
            CvInvoke.Threshold(H_channel, dst, 0, 255, ThresholdType.Otsu);

            Mat ele = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(3, 3), new Point(1, 1));
            CvInvoke.Erode(dst, dst, ele, new Point(1, 1), 1, BorderType.Default, new MCvScalar(0));

            CvInvoke.BitwiseNot(dst, dst);  // 反色

            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            
            CvInvoke.FindContours(dst, contours, null, RetrType.List, ChainApproxMethod.ChainApproxNone);
            
            for (var i = 0; i < contours.Size; i++)
            {
                var count = contours[i].Size;
                if (count < 6)
                    continue;
              
                RotatedRect box = CvInvoke.FitEllipse(contours[i]);
                var area = box.Size.Height * box.Size.Width;

                if (area >= 192 || box.Size.Height == box.Size.Width)
                {
                    continue;
                }

                if (!box.Size.IsEmpty)
                {
                    e.width = box.Size.Width;
                    e.height = box.Size.Height;

                    CvInvoke.Ellipse(tmp, box, new MCvScalar(0, 0, 255), 2);
                    e.res = tmp;
                    e.FLAG = true;
                    return e;
                }
                else
                {
                    e.FLAG = false;
                    return e;
                }

            }
            e.FLAG = false;
            return e;
        }
        /////////////////////////////////////////////////////////////////////////////
        private void quitButton_Click(object sender, EventArgs e)
        {
            capture.Stop();
            Application.Exit();
        }
       
        private void InitChart()
        {
            eyeChart.ChartAreas["ChartArea1"].AxisY.Minimum = 0;
            eyeChart.ChartAreas["ChartArea1"].AxisY.Maximum = 1;

            int x = Convert.ToInt32(ts.TotalMilliseconds);
            if (x == 0) x = 1;
            chartTimer.Interval = x;
            chartTimer.Tick += Timer_Tick;
            chartTimer.Start();
        }
               
        private void Timer_Tick(object sender, EventArgs e)
        {
            Series series = eyeChart.Series[0];
            series.Points.DataBindY(eye);          
        }




        

    }
}
