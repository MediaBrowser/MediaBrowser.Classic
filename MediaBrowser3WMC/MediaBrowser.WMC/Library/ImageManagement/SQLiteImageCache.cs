using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MediaBrowser.Library.Persistance;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Threading;
using System.Data.SQLite;
using MediaBrowser.Library.Logging;

namespace MediaBrowser.Library.ImageManagement
{
    class SQLiteImageCache : SQLiteRepository, IImageCache
    {
        public SQLiteImageCache(string dbPath)
        {
            if (sqliteAssembly == null)
            {
                sqliteAssembly = System.Reflection.Assembly.LoadFile(System.IO.Path.Combine(ApplicationPaths.AppConfigPath, "system.data.sqlite.dll"));
                AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(SqliteResolver);
            }

            SQLiteConnectionStringBuilder connectionstr = new SQLiteConnectionStringBuilder();
            connectionstr.PageSize = 4096;
            connectionstr.CacheSize = 4096;
            connectionstr.SyncMode = SynchronizationModes.Normal;
            connectionstr.DataSource = dbPath;
            connection = new SQLiteConnection(connectionstr.ConnectionString);
            connection.Open();

            string[] queries = {"create table if not exists images (guid, width, height, updated, stream_size, data blob)",
                                "create unique index if not exists idx_images on images(guid, width)",
                               };


            foreach (var query in queries) {
                try {

                    connection.Exec(query);
                } catch (Exception e) {
                    Logger.ReportInfo(e.ToString());
                }
            }


            alive = true; // tell writer to keep going
            Async.Queue("ImageCache Writer", DelayedWriter); 

        }

        private string ImagePath(Guid id, int width, long streamSize)
        {
            return "http://localhost:8755/" + id.ToString() + "/" + width + "/" + streamSize;
            //return "http://www.mediabrowser.tv/images/apps.png";
        }

        private MemoryStream ResizeImage(System.Drawing.Image image, int width, int height)
        {
            using (var newBmp = new System.Drawing.Bitmap(width, height))
            using (System.Drawing.Bitmap bmp = (System.Drawing.Bitmap)image)
            using (System.Drawing.Graphics graphic = System.Drawing.Graphics.FromImage(newBmp))
            {

                graphic.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphic.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphic.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphic.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                graphic.DrawImage(bmp, 0, 0, width, height);

                var ms = new MemoryStream();
                newBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms;
            }
        }

        #region IImageCache Members

        public List<ImageSize> AvailableSizes(Guid id)
        {
            throw new NotImplementedException();
        }

        public string CacheImage(Guid id, System.Drawing.Image image)
        {
            var ms = new MemoryStream();
            //translate to our cached size - just cache a single size and allow the interface to scale it
            int height = image.Height;
            Logger.ReportVerbose("Height is: " + height);
            int width = image.Width;
            Logger.ReportVerbose("Width is: " + width);
            int storedWidth = TranslateWidth(width);
            Logger.ReportVerbose("TranslatedWidth is: " + storedWidth);
            if (storedWidth != width)
            {
                height = (int)((height * (storedWidth / (float)width)));
                width = storedWidth;
                Logger.ReportVerbose("Resizing to: " + width + "x" + height);
                ms = ResizeImage(image, width, height);
            }
            else
            {
                System.Drawing.Imaging.ImageFormat format = image.Width < 1100 ? System.Drawing.Imaging.ImageFormat.Png : System.Drawing.Imaging.ImageFormat.Jpeg;
                image.Save(ms, format);
            }
            //Logger.ReportVerbose("Image memory size: " + ms.Length);
            return CacheImage(id, ms, width, height);
        }

        public string CacheImage(Guid id, MemoryStream ms, int width, int height) 
        {

            var cmd = connection.CreateCommand();
            cmd.CommandText = "replace into images(guid, width, height, updated, stream_size, data) values (@guid, @width, @height, @updated, @size, @data)";

            SQLiteParameter guidParam = new SQLiteParameter("@guid");
            SQLiteParameter widthParam = new SQLiteParameter("@width");
            SQLiteParameter heightParam = new SQLiteParameter("@height");
            SQLiteParameter updatedParam = new SQLiteParameter("@updated");
            SQLiteParameter sizeParam = new SQLiteParameter("@size");
            SQLiteParameter dataParam = new SQLiteParameter("@data");

            cmd.Parameters.Add(guidParam);
            cmd.Parameters.Add(widthParam);
            cmd.Parameters.Add(heightParam);
            cmd.Parameters.Add(updatedParam);
            cmd.Parameters.Add(sizeParam);
            cmd.Parameters.Add(dataParam);

            guidParam.Value = id.ToString();
            widthParam.Value = width;
            heightParam.Value = height;
            updatedParam.Value = DateTime.UtcNow;
            sizeParam.Value = ms.Length;
            dataParam.Value = ms.ToArray();
            Logger.ReportVerbose("Caching image("+id+") size: " + ms.ToArray().Length);

            lock (connection)
            {
                //don't use our delayed writer here cuz we need to block until this is done
                cmd.ExecuteNonQuery();
            }
            return ImagePath(id, width, ms.Length);

        }

        public DateTime GetDate(Guid id)
        {
            throw new NotImplementedException();
        }

        private int TranslateWidth(int realWidth)
        {
            if (realWidth > 1100 || realWidth < 570)
                return realWidth;
            else return 570;
        }

        public string GetImagePath(Guid id, int width, int height)
        {
            var cmd = connection.CreateCommand();
            int storedWidth = TranslateWidth(width);
            //if (width > 0)
            //{
            //    cmd.CommandText = "select stream_size from images where guid = @guid and width = @width";
            //    cmd.AddParam("@guid", id.ToString());
            //    cmd.AddParam("@width", storedWidth);
            //}
            //else
            {
                cmd.CommandText = "select stream_size from images where guid = @guid order by width desc";
                cmd.AddParam("@guid", id.ToString());
            }

            int size = 0;

            using (var reader = cmd.ExecuteReader()) {
                if (reader.Read())
                {
                    size = Convert.ToInt32(reader[0]);
                }
                else
                { //need to cache it
                    using (var ms = GetImageStream(id))
                    {
                        if (ms == null || ms.Length == 0)
                        {
                            //no image is cached
                            return null;
                        }
                        else
                        {
                            if (width > 0)
                            {
                                height = (int)((height * storedWidth) / width);
                                width = storedWidth;
                                Logger.ReportVerbose("Resizing image " + id + " to " + width + "x" + height);
                                var newImage = ResizeImage(System.Drawing.Image.FromStream(ms), width, height);
                                size = (int)newImage.Length;
                                CacheImage(id, newImage, width, height);
                            }
                        }
                    }
                }

                reader.Close();
            }
            return ImagePath(id, width, size);
        }

        public string GetImagePath(Guid id)
        {
            ImageInfo info = GetPrimaryImageInfo(id);
            if (info == null) info = new ImageInfo(null);
            return GetImagePath(id, info.Width, info.Height);
        }

        public ImageInfo GetPrimaryImageInfo(Guid id)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "select width, height, updated from images where guid = @guid order by width desc";
            cmd.AddParam("@guid", id.ToString());

            ImageInfo info = null;

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    info = new ImageInfo(null);
                    info.Width = Convert.ToInt32(reader[0]);
                    info.Height = Convert.ToInt32(reader[1]);
                    info.Date = DateTime.Parse(reader[2].ToString());
                }
                reader.Close();
            }
            return info;
        }

        public MemoryStream GetImageStream(Guid id)
        {
            return GetImageStream(id, 0);
        }

        public MemoryStream GetImageStream(Guid id, int width)
        {
            var cmd = connection.CreateCommand();
            if (width > 0)
            {
                width = TranslateWidth(width);
                cmd.CommandText = "select data from images where guid = @guid and width = @width";
                cmd.AddParam("@guid", id.ToString());
                cmd.AddParam("@width", width);
            }
            else
            {
                cmd.CommandText = "select data from images where guid = @guid order by width desc";
                cmd.AddParam("@guid", id.ToString());
            }

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    var data = reader.GetBytes(0);
                    var ms = new MemoryStream(data);
                    //Logger.ReportVerbose("Memorystream size on read from db("+id+"): " + ms.Length);
                    return ms;
                }
                else
                {
                    //not found - return an empty stream
                    return new MemoryStream();
                }
            }
        }

        public ImageSize GetSize(Guid id)
        {
            throw new NotImplementedException();
        }

        public void DeleteResizedImages()
        {
            throw new NotImplementedException();
        }

        public string Path
        {
            get { throw new NotImplementedException(); }
        }

        public void ClearCache(Guid id)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "delete from images where guid = @guid";
            cmd.AddParam("@guid", id.ToString());
            cmd.ExecuteNonQuery();
        }

        #endregion
    }
}
