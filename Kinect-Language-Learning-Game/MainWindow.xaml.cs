    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;

namespace ColorBasics
{
    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for color frames
        /// </summary>
        private ColorFrameReader colorFrameReader = null;

        private BodyFrameReader bodyFrameReader = null;

        private CoordinateMapper coordinateMapper = null;

        private Bitmap bitmap;

        // TODO lock access to this
        private Dictionary<JointType, Tuple<CameraSpacePoint, ColorSpacePoint>> handJoints = new Dictionary<JointType, Tuple<CameraSpacePoint, ColorSpacePoint>>();

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap colorBitmap = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // open the reader for the color frames
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();

            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();
            this.bodyFrameReader.FrameArrived += BodyFrameReader_FrameArrived;

            // wire handler for frame arrival
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // create the colorFrameDescription from the ColorFrameSource using Bgra format
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            // create the bitmap to display
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Microsoft.Samples.Kinect.ColorBasics.Properties.Resources.RunningStatusText
                                                            : Microsoft.Samples.Kinect.ColorBasics.Properties.Resources.NoSensorStatusText;

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        private void BodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;
            Body[] bodies = null;
            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (null == bodyFrame)
                {
                    return;
                }

                if (null == bodies)
                {
                    bodies = new Body[bodyFrame.BodyCount];
                }

                bodyFrame.GetAndRefreshBodyData(bodies);
                dataReceived = true;
            }

            if (!dataReceived)
            {
                return;
            }

            foreach(Body body in bodies)
            {
                if (!body.IsTracked)
                {
                    continue;
                }

                // Map from camera coordinate system to color image
                CameraSpacePoint[] jointPoints = new CameraSpacePoint[]
                {
                    body.Joints[JointType.HandLeft].Position,
                    body.Joints[JointType.HandTipLeft].Position,
                    body.Joints[JointType.HandRight].Position,
                    body.Joints[JointType.HandTipRight].Position,
                    body.Joints[JointType.ShoulderRight].Position
                };

                ColorSpacePoint[] colorSpacePoints = new ColorSpacePoint[jointPoints.Length];
                this.coordinateMapper.MapCameraPointsToColorSpace(jointPoints, colorSpacePoints);

                lock (handJoints)
                {
                    handJoints[JointType.HandLeft] = new Tuple<CameraSpacePoint, ColorSpacePoint>(jointPoints[0], colorSpacePoints[0]);
                    handJoints[JointType.HandTipLeft] = new Tuple<CameraSpacePoint, ColorSpacePoint>(jointPoints[1], colorSpacePoints[1]);
                    handJoints[JointType.HandRight] = new Tuple<CameraSpacePoint, ColorSpacePoint>(jointPoints[2], colorSpacePoints[2]);
                    handJoints[JointType.HandTipRight] = new Tuple<CameraSpacePoint, ColorSpacePoint>(jointPoints[3], colorSpacePoints[3]);
                    handJoints[JointType.ShoulderRight] = new Tuple<CameraSpacePoint, ColorSpacePoint>(jointPoints[4], colorSpacePoints[4]);
                }

                // Console.WriteLine(handJoints[JointType.HandRight].Item1.X + ", " + handJoints[JointType.HandRight].Item1.Y + ", " + handJoints[JointType.HandRight].Item1.Z);
                Console.WriteLine(handJoints[JointType.HandRight].Item1.Z + ", " + handJoints[JointType.ShoulderRight].Item1.Z);
                float x = handJoints[JointType.HandRight].Item2.X;
                float y = handJoints[JointType.HandRight].Item2.Y;
                float z = handJoints[JointType.HandRight].Item1.Z;
                float pivot = handJoints[JointType.ShoulderRight].Item1.Z;

                if (pivot - z >= 0.3)
                {
                    trail.Points.Add(new System.Windows.Point { X = x, Y = y });
                }
            }
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.colorBitmap;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.colorFrameReader != null)
            {
                // ColorFrameReder is IDisposable
                this.colorFrameReader.Dispose();
                this.colorFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the user clicking on the screenshot button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.colorBitmap != null)
            {
                // create a png bitmap encoder which knows how to save a .png file
                BitmapEncoder encoder = new PngBitmapEncoder();

                // create frame from the writable bitmap and add to encoder
                encoder.Frames.Add(BitmapFrame.Create(this.colorBitmap));

                string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

                string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

                string path = Path.Combine(myPhotos, "KinectScreenshot-Color-" + time + ".png");

                // write the new file to disk
                try
                {
                    // FileStream is IDisposable
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }

                    this.StatusText = string.Format(Microsoft.Samples.Kinect.ColorBasics.Properties.Resources.SavedScreenshotStatusTextFormat, path);
                }
                catch (IOException)
                {
                    this.StatusText = string.Format(Microsoft.Samples.Kinect.ColorBasics.Properties.Resources.FailedScreenshotStatusTextFormat, path);
                }
            }
        }

        /// <summary>
        /// Handles the color frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {

            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        if (null == bitmap || colorFrameDescription.Width != bitmap.Width || colorFrameDescription.Height != bitmap.Height)
                        {
                            bitmap = new Bitmap(colorFrameDescription.Width, colorFrameDescription.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        }

                        BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        colorFrame.CopyConvertedFrameDataToIntPtr(bmpData.Scan0,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                        bitmap.UnlockBits(bmpData);

                        Graphics graphics = Graphics.FromImage(bitmap);

                        lock (handJoints)
                        {
                            foreach (KeyValuePair<JointType, Tuple<CameraSpacePoint, ColorSpacePoint>> pair in handJoints)
                            {
                                float x = pair.Value.Item2.X;
                                float y = pair.Value.Item2.Y;
                                graphics.DrawEllipse(new System.Drawing.Pen(System.Drawing.Color.Red, 5), x, y, 10, 10);
                            }
                        }

                        this.colorBitmap.Lock();

                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == bitmap.Width) && (colorFrameDescription.Height == bitmap.Height))
                        {
                            bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            colorBitmap.WritePixels(new Int32Rect(0, 0, colorFrameDescription.Width, colorFrameDescription.Height), bmpData.Scan0, bmpData.Stride * bmpData.Width, bmpData.Stride);
                            bitmap.UnlockBits(bmpData);
                            this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));

                        }

                        this.colorBitmap.Unlock();
                    }
                }
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Microsoft.Samples.Kinect.ColorBasics.Properties.Resources.RunningStatusText
                                                            : Microsoft.Samples.Kinect.ColorBasics.Properties.Resources.SensorNotAvailableStatusText;
        }
    }
}
