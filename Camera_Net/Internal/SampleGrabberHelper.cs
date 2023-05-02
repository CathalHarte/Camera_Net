#region License

/*
Camera_NET - Camera wrapper for directshow for .NET
Copyright (C) 2013
https://github.com/free5lot/Camera_Net

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 3.0 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU LesserGeneral Public 
License along with this library. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

namespace Camera_NET
{
    #region Using directives

    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Runtime.InteropServices;
    using System.Diagnostics;
    using System.Threading;
    using System.Drawing;
    using System.Drawing.Imaging;

    // Use DirectShowLib (LGPL v2.1)
    using DirectShowLib;

    // Contains common types for AVI format like FourCC
    using SharpAvi;
    // Contains types used for writing like AviWriter
    using SharpAvi.Output;
    // Contains types related to encoding like Mpeg4VcmVideoEncoder
    using SharpAvi.Codecs;
    using System.Threading.Tasks;

    #endregion

    /// <summary>
    /// Helper for SampleGrabber. Used to make screenshots (snapshots).
    /// </summary>
    /// <remarks>This class is inherited from <see cref="ISampleGrabberCB"/> class.</remarks>
    /// 
    /// <author> free5lot (free5lot@yandex.ru) </author>
    /// <version> 2013.10.17 </version>
    internal sealed class SampleGrabberHelper : ISampleGrabberCB, IDisposable
    {
        #region Public

        /// <summary>
        /// Default constructor for <see cref="SampleGrabberHelper"/> class.
        /// </summary>
        /// <param name="sampleGrabber">Pointer to COM-interface ISampleGrabber.</param>
        public SampleGrabberHelper(ISampleGrabber sampleGrabber)
        {
            m_SampleGrabber = sampleGrabber;

            // tell the callback to ignore new images
            m_PictureReady = new ManualResetEvent(false);

            m_videoStreamWriter = new VideoStreamWriter("test.avi", 1280, 720, 30);
        }

        /// <summary>
        /// Disposes object and snapshot.
        /// </summary>
        public void Dispose()
        {
            if (m_PictureReady != null)
            {
                m_PictureReady.Close();
            }

            m_SampleGrabber = null;
        }

        /// <summary>
        /// Configures mode (mediatype, format type and etc).
        /// </summary>
        public void ConfigureMode()
        {
            int hr;
            AMMediaType media = new AMMediaType();

            // Set the media type to Video/RBG32
            media.majorType = MediaType.Video;
            media.subType = MediaSubType.RGB32;
            media.formatType = FormatType.VideoInfo;
            hr = m_SampleGrabber.SetMediaType(media);
            DsError.ThrowExceptionForHR(hr);

            DsUtils.FreeAMMediaType(media);
            media = null;

            // Configure the samplegrabber

            // To save current frame via SnapshotNextFrame
            //ISampleGrabber::SetCallback method
            // Note  [Deprecated. This API may be removed from future releases of Windows.]
            // http://msdn.microsoft.com/en-us/library/windows/desktop/dd376992%28v=vs.85%29.aspx
            hr = m_SampleGrabber.SetCallback(this, 1); // 1 == WhichMethodToCallback, call the ISampleGrabberCB::BufferCB method
            DsError.ThrowExceptionForHR(hr);
        }

        /// <summary>
        /// Gets and saves mode (mediatype, format type and etc). 
        /// </summary>
        public void SaveMode()
        {
            int hr;

            // Get the media type from the SampleGrabber
            AMMediaType media = new AMMediaType();

            hr = m_SampleGrabber.GetConnectedMediaType(media);
            DsError.ThrowExceptionForHR(hr);

            if ((media.formatType != FormatType.VideoInfo) || (media.formatPtr == IntPtr.Zero))
            {
                throw new NotSupportedException("Unknown Grabber Media Format");
            }

            // Grab the size info
            VideoInfoHeader videoInfoHeader = (VideoInfoHeader)Marshal.PtrToStructure(media.formatPtr, typeof(VideoInfoHeader));
            m_videoWidth = videoInfoHeader.BmiHeader.Width;
            m_videoHeight = videoInfoHeader.BmiHeader.Height;
            m_videoBitCount = videoInfoHeader.BmiHeader.BitCount;
            m_ImageSize = videoInfoHeader.BmiHeader.ImageSize;

            DsUtils.FreeAMMediaType(media);
            media = null;
        }

        /// <summary>
        /// SampleCB callback (NOT USED). It should be implemented for ISampleGrabberCB
        /// </summary>
        int ISampleGrabberCB.SampleCB(double SampleTime, IMediaSample pSample)
        {
            Marshal.ReleaseComObject(pSample);
            return 0;
        }

        /// <summary>
        /// BufferCB callback 
        /// </summary>
        /// <remarks>COULD BE EXECUTED FROM FOREIGN THREAD.</remarks>
        int ISampleGrabberCB.BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
        {
            // Note that we depend on only being called once per call to Click.  Otherwise
            // a second call can overwrite the previous image.
            Debug.Assert(BufferLen == Math.Abs(m_videoBitCount/8*m_videoWidth) * m_videoHeight, "Incorrect buffer length");
            thing++;

            if (thing > 50)
            {
                // m_videoStreamWriter?.SaveFrameCallback(pBuffer, BufferLen, PixelFormat.Format32bppRgb);
            }

            if (thing == 650)
                m_videoStreamWriter = null;

            return 0;
        }

        private int thing;


        /// <summary>
        /// Makes a snapshot of next frame
        /// </summary>
        /// <returns>Bitmap with snapshot</returns>
        public Bitmap SnapshotNextFrame()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private

        private VideoStreamWriter m_videoStreamWriter;

        /// <summary>
        /// Flag to wait for the async job to finish.
        /// </summary>
        private volatile ManualResetEvent m_PictureReady = null;

        /// <summary>
        /// Video frame width. Calculated once in constructor for perf.
        /// </summary>
        private int m_videoWidth;

        /// <summary>
        /// Video frame height. Calculated once in constructor for perf.
        /// </summary>
        private int m_videoHeight;

        /// <summary>
        /// Video frame bits per pixel.
        /// </summary>
        private int m_videoBitCount;

        /// <summary>
        /// Size of frame in bytes.
        /// </summary>
        private int m_ImageSize;

        /// <summary>
        /// Pointer to COM-interface ISampleGrabber.
        /// </summary>
        private ISampleGrabber m_SampleGrabber = null;

        #endregion
    }


    internal sealed class VideoStreamWriter
    {
        private readonly AviWriter _writer;
        private readonly IAviVideoStream _stream;
        private readonly int _videoWidth;
        private readonly int _videoHeight;

        public VideoStreamWriter(string fileName, int videoWidth, int videoHeight, int framesPerSecond, int quality = 70, bool emitIndex1 = true)
        {
            _writer = new AviWriter(fileName)
            {
                FramesPerSecond = framesPerSecond,
                EmitIndex1 = emitIndex1
            };

            _videoWidth = videoWidth;
            _videoHeight = videoHeight;

            _stream = _writer.AddMJpegImageSharpVideoStream(videoWidth, videoHeight, quality);
        }

        public void WriteFrame(byte[] frameData)
        {
            if (_stream == null)
            {
                throw new InvalidOperationException("The video stream is not initialized.");
            }

            _stream.WriteFrame(true, frameData, 0, frameData.Length);
        }

        /// <summary>
        /// Assumes that the video dimensions match that of the initialised values
        /// </summary>
        public void SaveFrameCallback(IntPtr pBuffer, int bufferLen, PixelFormat pixelFormat)
        {
            var bitmap = new Bitmap(_videoWidth, _videoHeight, (bufferLen / _videoHeight) * _videoWidth, pixelFormat, pBuffer);

            // Assuming 'bitmap' is the Bitmap that you want to convert to a byte array
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
            var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
            var ptr = bitmapData.Scan0;

            // Calculate the size of the byte array
            var byteCount = bitmapData.Stride * bitmap.Height;
            var byteArray = new byte[byteCount];

            // Use Marshal.Copy to copy the pixel data from the bitmap to the byte array
            Marshal.Copy(ptr, byteArray, 0, byteCount);

            WriteFrame(byteArray);

            // Unlock the bitmap data and dispose of the bitmap
            bitmap.UnlockBits(bitmapData);
        }

        ~VideoStreamWriter()
        {
            _writer.Close();
        }
    }
}
