//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
namespace ColorBasics
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using Microsoft.Speech.AudioFormat;
    using Microsoft.Speech.Recognition;
    using System.Threading;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Drawing.Text;
    using Microsoft.Samples.Kinect;
    using System.Windows.Shapes;
    using System.Reflection;
    using System.Windows.Interop;



    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window
    {

        ImageConverter converter;
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;


        private SpeechRecognitionEngine speechEngine = null;

        private Kinect.KinectAudioStream audioStream = null;

        /// <summary>
        /// Reader for color frames
        /// </summary>
        private ColorFrameReader colorFrameReader = null;

        private BodyFrameReader bodyFrameReader = null;

        private CoordinateMapper coordinateMapper = null;

        private Bitmap bitmap;

        private SpeechToText speechToText;

        private object textOnScreenLock = new object();
        private string textOnScreen;

        private Polygon[] leftIndexFingerLines = new Polygon[8];
        private Polygon[] rightIndexFingerLines = new Polygon[8];
        private SolidColorBrush[] playerBrushes = new SolidColorBrush[]
        {
            System.Windows.Media.Brushes.Red,
            System.Windows.Media.Brushes.Green,
            System.Windows.Media.Brushes.Blue,
            System.Windows.Media.Brushes.Orange,
            System.Windows.Media.Brushes.DarkGreen,
            System.Windows.Media.Brushes.DarkMagenta,
            System.Windows.Media.Brushes.Azure,
            System.Windows.Media.Brushes.White
        };

        /// <summary>
        /// Number of bytes in each Kinect audio stream sample (32-bit IEEE float).
        /// </summary>
        private const int BytesPerAudioSample = sizeof(float);
        /// <summary>
        /// Will be allocated a buffer to hold a single sub frame of audio data read from audio stream.
        /// </summary>
        private readonly byte[] audioBuffer = null;
        /// <summary>
        /// Reader for audio frames
        /// </summary>
        private AudioBeamFrameReader audioBeamReader = null;
        private MemoryStream audioSnippet;
        private int maxAudioSamples;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap colorBitmap = null;

        private bool reading;

        private bool readyToDrawNewSymbol = true;
        private Image currentlyDisplayedSymbol = null;
        private bool redraw = false;
        private Stopwatch stopwatch = new Stopwatch();

        int rec_time = 2 * 16000;
        private Font simp;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // open the reader for the color frames
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;

            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();
            this.bodyFrameReader.FrameArrived += BodyFrameReader_FrameArrived;

            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // create the colorFrameDescription from the ColorFrameSource using Bgra format
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            // create the bitmap to display
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            speechToText = new SpeechToText();
            speechToText.TextReceived += SpeechToText_TextReceived;

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // Get its audio source
            AudioSource audioSource = this.kinectSensor.AudioSource;
            this.audioBuffer = new byte[audioSource.SubFrameLengthInBytes];
            this.maxAudioSamples = (int)((audioSource.SubFrameLengthInBytes / sizeof(float)) * 512);
            this.audioSnippet = new MemoryStream(maxAudioSamples * 2);
            this.audioBeamReader = audioSource.OpenReader();
            this.audioBeamReader.FrameArrived += this.Reader_AudioFrameArrived;

            converter = new ImageConverter(); 

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

            for (int i=0; i<bodies.Length; i++)
            {
                Body body = bodies[i];
                if (!body.IsTracked)
                {
                    leftIndexFingerLines[i] = null;
                    rightIndexFingerLines[i] = null;
                    continue;
                }

                float opacity = 0.5f;
                int strokeThickness = 10;

                

                // Work on left finger
                if (IsLeftHandTracked(body))
                {
                    ColorSpacePoint handTipLeftColorSpace = coordinateMapper.MapCameraPointToColorSpace(body.Joints[JointType.HandTipLeft].Position);

                    Polygon polygon = null;
                    if (null == leftIndexFingerLines[i])
                    {
                        polygon = new Polygon();
                        polygon.Stroke = playerBrushes[i];
                        polygon.Opacity = opacity;
                        polygon.StrokeThickness = strokeThickness;
                        polygon.HorizontalAlignment = HorizontalAlignment.Left;
                        polygon.VerticalAlignment = VerticalAlignment.Center;
                        leftIndexFingerLines[i] = polygon;
                    }
                    else
                    {
                        polygon = leftIndexFingerLines[i];
                    }

                    polygon.Points.Add(new System.Windows.Point(handTipLeftColorSpace.X, handTipLeftColorSpace.Y));

                    if (polygon.Points.Count >= 5)
                    {
                        canvas.Children.Add(polygon);
                        System.Windows.Controls.Canvas.SetZIndex(polygon, 5);
                        leftIndexFingerLines[i] = new Polygon();
                        leftIndexFingerLines[i].Stroke = playerBrushes[i];
                        leftIndexFingerLines[i].Opacity = opacity;
                        leftIndexFingerLines[i].StrokeThickness = strokeThickness;
                        leftIndexFingerLines[i].HorizontalAlignment = HorizontalAlignment.Left;
                        leftIndexFingerLines[i].VerticalAlignment = VerticalAlignment.Center;
                        leftIndexFingerLines[i].Points.Add(new System.Windows.Point(handTipLeftColorSpace.X, handTipLeftColorSpace.Y));
                    }
                }
                else
                {
                    Polygon polygon = leftIndexFingerLines[i];
                    if (null != polygon)
                    {
                        // End and draw polygon
                        canvas.Children.Add(polygon);
                        System.Windows.Controls.Canvas.SetZIndex(polygon, 5);
                        leftIndexFingerLines[i] = null;
                    }
                }


                // Work on right finger
                if (IsRightHandTracked(body))
                {
                    ColorSpacePoint handTipRightColorSpace = coordinateMapper.MapCameraPointToColorSpace(body.Joints[JointType.HandTipRight].Position);
                    Polygon polygon = null;
                    if (null == rightIndexFingerLines[i])
                    {
                        polygon = new Polygon();
                        polygon.Stroke = playerBrushes[i];
                        polygon.Opacity = opacity;
                        polygon.StrokeThickness = strokeThickness;
                        rightIndexFingerLines[i] = polygon;
                    }
                    else
                    {
                        polygon = rightIndexFingerLines[i];
                    }

                    polygon.Points.Add(new System.Windows.Point(handTipRightColorSpace.X, handTipRightColorSpace.Y));

                    if (polygon.Points.Count >= 5)
                    {
                        canvas.Children.Add(polygon);
                        System.Windows.Controls.Canvas.SetZIndex(polygon, 5);
                        rightIndexFingerLines[i] = new Polygon();
                        rightIndexFingerLines[i].Stroke = playerBrushes[i];
                        rightIndexFingerLines[i].Opacity = opacity;
                        rightIndexFingerLines[i].StrokeThickness = strokeThickness;
                        rightIndexFingerLines[i].HorizontalAlignment = HorizontalAlignment.Left;
                        rightIndexFingerLines[i].VerticalAlignment = VerticalAlignment.Center;
                        rightIndexFingerLines[i].Points.Add(new System.Windows.Point(handTipRightColorSpace.X, handTipRightColorSpace.Y));
                    }
                }
                else
                {
                    Polygon polygon = rightIndexFingerLines[i];
                    if (null != polygon)
                    {
                        // End and draw polygon
                        canvas.Children.Add(polygon);
                        System.Windows.Controls.Canvas.SetZIndex(polygon, 5);
                        rightIndexFingerLines[i] = null;
                    }
                }
            }
        }

        private bool IsLeftHandTracked(Body body)
        {
            return IsHandTracked(body.Joints[JointType.HandTipLeft], body.Joints[JointType.ShoulderLeft]);
        }

        private bool IsRightHandTracked(Body body)
        {
            return IsHandTracked(body.Joints[JointType.HandTipRight], body.Joints[JointType.ShoulderRight]);
        }

        private bool IsHandTracked(Joint handTip, Joint shoulderTip, float minZDistance=0.4f)
        {
            return handTip.TrackingState == TrackingState.Tracked && Math.Abs(handTip.Position.Z - shoulderTip.Position.Z) > minZDistance;
        }

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

            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }

            if (null != this.audioStream)
            {
                this.audioStream.SpeechActive = false;
                this.audioStream.Dispose();
                this.audioStream = null;
            }

            if (null != this.audioBeamReader)
            {
                this.audioBeamReader.Dispose();
                this.audioBeamReader = null;
            }

            if (null != this.speechEngine)
            {
                this.speechEngine.SpeechRecognized -= this.SpeechRecognized;
                this.speechEngine.SpeechRecognitionRejected -= this.SpeechRejected;
                this.speechEngine.RecognizeAsyncStop();
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

                        BitmapData bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        colorFrame.CopyConvertedFrameDataToIntPtr(bmpData.Scan0,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                        bitmap.UnlockBits(bmpData);

                        Graphics graphics = Graphics.FromImage(bitmap);

                        if (null != textOnScreen && redraw)
                        {
                            lock (textOnScreenLock)
                            {
                                // Draw letters
                                if (null != textOnScreen)
                                {
                                    currentlyDisplayedSymbol = DrawLetter(textOnScreen);
                                    var bitmap = new System.Drawing.Bitmap(currentlyDisplayedSymbol);
                                    var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(),
                                                                                                          IntPtr.Zero,
                                                                                                          Int32Rect.Empty,
                                                                                                          BitmapSizeOptions.FromEmptyOptions()
                                          );
                                    bitmap.Dispose();

                                    int symbolWidth = currentlyDisplayedSymbol.Width;
                                    int symbolHeight = currentlyDisplayedSymbol.Height;
                                    int symbolXPosition = colorFrameDescription.Width / 2 - symbolWidth / 2;
                                    int symbolYPosition = colorFrameDescription.Height / 2 - symbolHeight / 2;

                                    System.Windows.Controls.Canvas.SetTop(Template, symbolYPosition);
                                    System.Windows.Controls.Canvas.SetLeft(Template, symbolXPosition);
                                    Template.Width = symbolWidth;
                                    Template.Height = symbolHeight;
                                    Template.Fill = new ImageBrush(bitmapSource);
                                    redraw = false;
                                }
                            }
                        }
                                             

                        /*
                        using (Font drawFont = new Font("Microsoft JhengHei", 120))
                        {
                            using (SolidBrush drawBrush = new SolidBrush(System.Drawing.Color.FromArgb(128, System.Drawing.Color.Red)))
                            {
                                lock (textOnScreenLock)
                                {
                                    if (null != textOnScreen)
                                    {
                                        graphics.DrawString(textOnScreen, drawFont, drawBrush, new RectangleF(colorFrameDescription.Width / 2, colorFrameDescription.Height / 2, 400, 400));
                                    }
                                }
                            }
                        }*/

                        this.colorBitmap.Lock();

                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == bitmap.Width) && (colorFrameDescription.Height == bitmap.Height))
                        {
                            bmpData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            colorBitmap.WritePixels(new Int32Rect(0, 0, colorFrameDescription.Width, colorFrameDescription.Height), bmpData.Scan0, bmpData.Stride * bmpData.Width, bmpData.Stride);
                            bitmap.UnlockBits(bmpData);
                            this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));
                        }

                        this.colorBitmap.Unlock();
                    }
                }

                if (stopwatch.ElapsedMilliseconds >= 15000)
                {
                    readyToDrawNewSymbol = true;
                    if (canvas.Children.Count > 2)
                    {
                        canvas.Children.RemoveRange(2, canvas.Children.Count - 1);
                    }
                    Template.Fill = System.Windows.Media.Brushes.Transparent;
                }
            }
        }

        private float percentageComplete()
        {
            //Template.Fill.
            //    currentlyDisplayedImage
            return 0.0f;
        }


        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // Do nothings
        }

        /// <summary>
        /// Execute initialization tasks.
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            PrivateFontCollection privateFont = new PrivateFontCollection();

            privateFont.AddFontFile(System.IO.Path.Combine(Environment.CurrentDirectory, "font.ttf"));

            simp = new Font(privateFont.Families[0], 108, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel);



            if (this.kinectSensor != null)
            {
                // grab the audio stream
                System.Collections.Generic.IReadOnlyList<AudioBeam> audioBeamList = this.kinectSensor.AudioSource.AudioBeams;
                System.IO.Stream audioStream = audioBeamList[0].OpenInputStream();

                // create the convert stream
                this.audioStream = new Kinect.KinectAudioStream(audioStream);
            }

            RecognizerInfo ri = GetKinectRecognizer();

            if (null != ri)
            {
                this.speechEngine = new SpeechRecognitionEngine(ri.Id);


                var keyword = new Choices();
                keyword.Add(new SemanticResultValue("cancel", 1));

                var gb = new GrammarBuilder { Culture = ri.Culture };
                gb.Append(keyword);
                var g = new Grammar(gb);
                speechEngine.LoadGrammar(g);

                speechEngine.SpeechRecognized += this.SpeechRecognized;
                speechEngine.SpeechRecognitionRejected += this.SpeechRejected;

                // let the convertStream know speech is going active
                this.audioStream.SpeechActive = true;

                speechEngine.SetInputToAudioStream(
                    this.audioStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));

                speechEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
            else
            {
                Debug.WriteLine("No recognizer");
            }

            stopwatch.Start();
        }

        #region Audio recognition
        private static RecognizerInfo GetKinectRecognizer()
        {
            Debug.WriteLine("In Get");
            foreach (RecognizerInfo recognizer in SpeechRecognitionEngine.InstalledRecognizers())
            {
                Debug.WriteLine("In For");
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }

            return null;
        }
        /// <summary>
        /// Handler for rejected speech events.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            
        }

        /// <summary>
        /// Handler for rejected speech events.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            
            // Speech utterance confidence below which we treat speech as if it hadn't been heard
            const double ConfidenceThreshold = 0.7;

            if (!reading && e.Result.Confidence >= ConfidenceThreshold)
            {
                Debug.WriteLine("Matched");

                textOnScreen = null;
                currentlyDisplayedSymbol = null;
                readyToDrawNewSymbol = true;
                redraw = true;
            }
        }

        private void AudioTranslationThread()
        {

            byte[] audioBuffer = new byte[rec_time];

            Debug.WriteLine("Recording");
            int readCount = audioStream.Read(audioBuffer, 0, audioBuffer.Length);
            Debug.WriteLine("Done");

            //TODO Recognition and Translation

            this.reading = false;
            
        }

        private void Reader_AudioFrameArrived(object sender, AudioBeamFrameArrivedEventArgs e)
        {
            if (!readyToDrawNewSymbol)
            {
                // Discard latest audio frames in case the player is still busy drawing earlier figures
                return;
            }

            AudioBeamFrameReference frameReference = e.FrameReference;
            AudioBeamFrameList frameList = frameReference.AcquireBeamFrames();

            if (frameList != null)
            {
                BinaryWriter bw = new BinaryWriter(audioSnippet, System.Text.Encoding.ASCII, true);
                using (frameList)
                {
                    IReadOnlyList<AudioBeamSubFrame> subFrameList = frameList[0].SubFrames;
                    foreach (AudioBeamSubFrame subFrame in subFrameList)
                    {
                        subFrame.CopyFrameDataToArray(this.audioBuffer);
                        for (int i = 0; i < this.audioBuffer.Length; i += BytesPerAudioSample)
                        {
                            float audioSample = BitConverter.ToSingle(this.audioBuffer, i);
                            bw.Write((short)(audioSample * short.MaxValue));
                        }
                        if (audioSnippet.Position >= maxAudioSamples)
                        {
                            speechToText.SendBytes(audioSnippet.GetBuffer());
                            audioSnippet.Seek(0, SeekOrigin.Begin);
                        }
                    }
                }
                bw.Close();
            }
        }

        private int update(int x, int y)
        {
            return 0;
        }


        private void SpeechToText_TextReceived(object sender, SpeechToText.RecognizedTextArgs args)
        {
            String text = args.Text;

            if (null == text)
            {
                return;
            }

            // After receiving the text, translate it into Chinese
            Console.WriteLine("Recognized text: " + text);
            String translation = null;
            if (args.Text.Contains("translate")) {
                string tobesearched = "translate";
                string code = text.Substring(text.IndexOf(tobesearched) + tobesearched.Length);
                translation = TranslateText.Translate(code);
            }
            else if (args.Text.Contains("Translate"))
            {
                string tobesearched = "Translate";
                string code = text.Substring(text.IndexOf(tobesearched) + tobesearched.Length);
                translation = TranslateText.Translate(code);
            }
            if (null != translation)
            {
                translation = translation.Replace("。", "");

                // Limit to maximum 2 chars
                int startIndex = Math.Max(0, translation.Length - 2);
                translation = translation.Substring(startIndex, Math.Min(2, translation.Length - startIndex));

                lock (textOnScreenLock)
                {
                    textOnScreen = translation;
                    readyToDrawNewSymbol = false;
                    redraw = true;
                    stopwatch.Restart();
                }
            }
        }
        #endregion

        #region Letter drawing
        private Image DrawLetter(String letter)
        {
            return DrawText(letter, System.Drawing.Color.DarkRed, System.Drawing.Color.Empty);
        }

        private Image DrawText(String text, System.Drawing.Color textColor, System.Drawing.Color backColor)
        {
            PrivateFontCollection privateFont = new PrivateFontCollection();

            privateFont.AddFontFile(System.IO.Path.Combine(Environment.CurrentDirectory, "font.ttf"));
            Font font = new Font(privateFont.Families[0], 500, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel);


            SizeF textSize;
            Graphics graphics;
            using (graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                //measure the string to see how big the image needs to be
                textSize = graphics.MeasureString(text, font);
            }

            //create a new image of the right size
            Bitmap img = new Bitmap((int)textSize.Width, (int)textSize.Height);

            graphics = Graphics.FromImage(img);

            //paint the background
            graphics.Clear(backColor);

            //create a brush for the text
            System.Drawing.Brush textBrush = new SolidBrush(textColor);

            graphics.DrawString(text, font, textBrush, 0, 0);
            graphics.Save();

            textBrush.Dispose();
            graphics.Dispose();

            img.Save(System.IO.Path.Combine(Environment.CurrentDirectory, "tmp.bmp"));
            return img;

        }
        #endregion
    }

}



