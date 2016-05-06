﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SoftwareprojektTheremin
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private PXCMSession session;
        private PXCMSenseManager senseManager;
        private Thread update;
        private PXCMBlobModule blobModule;
        private PXCMBlobConfiguration blobConfig;
        private PXCMBlobData blobData;
        private PXCMBlobData.IBlob[] blobList = new PXCMBlobData.IBlob[2];
        private int trackingDistance = 1000;

        public MainWindow()
        {
            InitializeComponent();

            // Configure RealSense session and SenseManager interface
            session = PXCMSession.CreateInstance();
            senseManager = session.CreateSenseManager();
            senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 640, 480, 30);
            senseManager.EnableBlob();
            senseManager.Init();

            //Create blobModule from SenseManager
            blobModule = senseManager.QueryBlob();
            blobConfig = blobModule.CreateActiveConfiguration();
            blobConfig.SetMaxBlobs(2);
            blobConfig.SetMaxDistance(trackingDistance);
            blobConfig.SetMaxObjectDepth(100);
            blobConfig.SetMinPixelCount(400);
            blobConfig.ApplyChanges();
            blobData = blobModule.CreateOutput();



            // Start Update thread
            update = new Thread(new ThreadStart(Update));
            update.Start();
        }

        private void Update()
        {
            // Start AcquireFrame-ReleaseFrame loop
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                PXCMCapture.Sample sample = senseManager.QuerySample();
                Bitmap colorBitmap;
                PXCMImage.ImageData colorData;
                blobData.Update();
                float Left = 0;
                float Right = 0;

                 /*while (blobData.QueryNumberOfBlobs() < 2)
                 {
                     trackingDistance += 100;
                     blobConfig.SetMaxDistance(trackingDistance);
                     blobConfig.ApplyChanges();
                     blobData.Update();

                    if(trackingDistance > 3000)
                    {
                        trackingDistance = 600;
                    }
                 }
                 */
       

                for (int i = 0; i<2; i++)
                {
                    blobData.QueryBlobByAccessOrder(i, PXCMBlobData.AccessOrderType.ACCESS_ORDER_NEAR_TO_FAR, out blobList[i]);
                }
                if (blobData.QueryNumberOfBlobs() == 2)
                {
                    if (blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x > blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x)
                    {
                        Right = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        Left = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                    }
                    else
                    {
                        Right = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        Left = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                    }
                }

                // Get color image data
                sample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out colorData);
                colorBitmap = colorData.ToBitmap(0, sample.color.info.width, sample.color.info.height);

                // Update UI
                Render(colorBitmap, Left, Right);

                // Release frame
                colorBitmap.Dispose();
                sample.color.ReleaseAccess(colorData);
                senseManager.ReleaseFrame();
            }
        }

        private void Render(Bitmap bitmap, float left, float right)
        {
            BitmapImage bitmapImage = ConvertBitmap(bitmap);

            if (bitmapImage != null)
            {
                // Update the WPF Image control
                this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                {
                    imgStream.Source = bitmapImage;
                    labelR.Content = right;
                    labelL.Content = left;
                }));
            }
        }

        private BitmapImage ConvertBitmap(Bitmap bitmap)
        {
            BitmapImage bitmapImage = null;
            System.Drawing.Pen fancyPen = new System.Drawing.Pen(System.Drawing.Color.Cyan, 5);

            if (bitmap != null)
            {
                if (blobData.QueryNumberOfBlobs() == 2)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.DrawRectangle(fancyPen, blobList[i].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x-50, blobList[i].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y, 5, 5);
                        }
                    }
                }

                MemoryStream memoryStream = new MemoryStream();
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memoryStream.Position = 0;
                bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }

            return bitmapImage;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ShutDown();
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            ShutDown();
            this.Close();
        }

        private void ShutDown()
        {
            // Stop the Update thread
            update.Abort();

            // Dispose RealSense objects
            senseManager.Dispose();
            session.Dispose();
        }
    }
}
