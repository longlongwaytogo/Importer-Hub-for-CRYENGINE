﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Threading;

namespace CRYENGINE_ImportHub
{
    class TiffConvertThread
    {
        private string m_filePath;
        private string m_convertedFilePath;

        public TiffConvertThread(string filePath)
        {
            m_filePath = filePath;
            m_convertedFilePath = Path.GetDirectoryName(m_filePath) + @"\" + Path.GetFileNameWithoutExtension(m_filePath) + ".tif";

        }

        public void Convert()
        {
           try
            {
                BitmapDecoder bmpDec = BitmapDecoder.Create(new Uri(m_filePath), BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                BitmapSource srs = bmpDec.Frames[0];

                if (Path.GetExtension(m_convertedFilePath).Equals(".tif", StringComparison.CurrentCultureIgnoreCase))
                {
                    using (FileStream stream = new FileStream(m_convertedFilePath, FileMode.Create, FileAccess.Write, FileShare.Delete))
                    {
                        TiffBitmapEncoder encoder = new TiffBitmapEncoder();
                        encoder.Compression = TiffCompressOption.None;
                        encoder.Frames.Add(BitmapFrame.Create(srs));
                        encoder.Save(stream);
                    }
                }

            }
            catch (Exception ex)
            {
                Framework.ShowError("Unable to save the temporary file at: " + m_convertedFilePath, true);
            }
        }
    }


    class CTextureTiffConvert
    {
        private string m_filePath;
        private string m_fileName;
        private string m_convertedFilePath;
        private bool m_isSilentMode;

        public CTextureTiffConvert(string filePath, bool isSilentMode = false)
        {
            m_filePath = filePath;
            m_fileName = Path.GetFileName(filePath);
            m_convertedFilePath = Path.GetDirectoryName(m_filePath) + @"\" + Path.GetFileNameWithoutExtension(m_filePath) + ".tif";
            m_isSilentMode = isSilentMode;

            if (Path.GetExtension(filePath) != ".tif")
            {
                Framework.Log(m_fileName + ": Valid file, beginning conversion");

                TiffConvertThread threadHandle = new TiffConvertThread(filePath);

                Thread tiffConvertThread = new Thread(new ThreadStart(threadHandle.Convert));
                tiffConvertThread.Start();

                tiffConvertThread.Join();
                tiffConvertThread.Abort();

                CallRC();
            }
            else
            {
                //Tiff files don't need to be converted
                Framework.Log(m_fileName + ": Tiff file, unnecessary conversion: skip phase");

                CallRC();
            }

        }

        private void CallRC()
        {
            //Fix CE 3.6 /refresh command - Launch BackgroundWorker for DeleteDDS
            System.ComponentModel.BackgroundWorker bw = new System.ComponentModel.BackgroundWorker();
            bw.WorkerSupportsCancellation = true;
            bw.WorkerReportsProgress = true;
            bw.DoWork += new System.ComponentModel.DoWorkEventHandler(DeleteDDS);

            bw.RunWorkerAsync();

            Framework.CRYENGINE_RC_Call(m_convertedFilePath + " /refresh" + UserDialogCmd() + GetAdditionalCompressionPreset(), "File " + m_fileName + " succefully send to the Ressource Compiler at " + m_convertedFilePath);
        }

        private void DeleteDDS(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            var ddsFilePath = Path.GetDirectoryName(m_convertedFilePath) + @"\" + Path.GetFileNameWithoutExtension(m_convertedFilePath) + ".dds";

            if (File.Exists(ddsFilePath))
            {
                while (!Framework.IsFileLocked(ddsFilePath)) ;    //Wait until the source file is unlocked
                File.Delete(ddsFilePath);
                Framework.Log("DDS file found, deleting");
            }
        }

        private string UserDialogCmd()
        {
            if (Framework.IsCryTifDialogRequested() && !m_isSilentMode)
            {
                return " /userdialog=1";
            }
            else
            {
                return null;
            }
        }

        private string GetAdditionalCompressionPreset()
        {
            var fileName = Path.GetFileNameWithoutExtension(m_filePath);

            if (fileName.Length > 4)
            {
                if (fileName.Substring(fileName.Length - 5) == "_spec")
                    return " /preset=Specular_highQ";

                else if (fileName.Substring(fileName.Length - 5) == "_ddna")
                    return " /preset=NormalmapWithGlossInAlpha_highQ";

                else if (fileName.Substring(fileName.Length - 5) == "_diff")
                    return " /preset=Diffuse_highQ";

                else
                    return null;
            }
            else
            {
                return null;
            }
        }
    }
}
