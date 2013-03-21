using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Net;
using System.IO;
using System.Xml.XPath;
using Microsoft.MediaCenter.UI;
using System.Diagnostics;
using MediaBrowser.Library.Configuration;
using MediaBrowser.Library.Logging;

namespace MediaBrowser
{
    /// <summary>
    /// This model item uses the Yahoo! Developer API to retrieve the weather.
    /// Full details can be found here: http://developer.yahoo.com/weather/
    /// </summary>
    public class YahooWeather : ModelItem
    {
        
        private static readonly string FileName = string.Format("weather_{1}_{0}.xml", Application.CurrentInstance.Config.YahooWeatherFeed, Application.CurrentInstance.Config.YahooWeatherUnit);
        private readonly string DownloadToFilePath = Path.Combine(ApplicationPaths.AppRSSPath, FileName);        
        
        private readonly string Feed = string.Format("http://xml.weather.yahoo.com/forecastrss/{0}_{1}.xml",
            Application.CurrentInstance.Config.YahooWeatherFeed,
            Application.CurrentInstance.Config.YahooWeatherUnit);
        private const int RefreshIntervalHrs = 3;
        
        private string _imageUrl = "";
        private string _code = "";
        private string _codeDescription = "";
        private string _location = "";
        private string _temp = "";
        private string _unit = "";
        private string _longTemp = "";
        private string _longPressure = "";
        private string _longWindSpeed = "";
        private string _humidity = "";
        private string _speedunit = "";
        private string _chill = "";
        private string _direction = "";
        private string _translatedirection = "";
        private string _speed = "";
        private string _sunrise = "";
        private string _sunset = "";
        private string _pressure = "";
        private string _pressureunit = "";
        ArrayListDataSet _forecast = new ArrayListDataSet();
        ArrayListDataSet _extendedForecast = new ArrayListDataSet();
        private string _visibileDistance;
        private string _distanceUnit;
        private string _longVisibility;

        public YahooWeather()
        {
        }

        public string Code
        {
            get { return _code; }
            set { _code = value; FirePropertyChanged("Code"); }
        }

        public string CodeDescription
        {
            get { return _codeDescription; }
            set { _codeDescription = value; FirePropertyChanged("CodeDescription"); }
        }

        public string ImageUrl
        {
            get { return _imageUrl; }
            set { _imageUrl = value; FirePropertyChanged("ImageUrl"); }
        }

        public string Location
        {
            get { return _location; }
            set { _location = value; FirePropertyChanged("Location"); }
        }

        public string Temp
        {
            get { return _temp; }
            set { _temp = value; FirePropertyChanged("Temp"); }
        }

        public string Unit
        {
            get { return _unit; }
            set { _unit = value; FirePropertyChanged("Unit"); }
        }
        public string LongTemp
        {
            get { return _longTemp;}
            set { _longTemp = value; FirePropertyChanged("LongTemp"); }
        }
        public string LongPressure
        {
            get { return _longPressure; }
            set { _longPressure = value; FirePropertyChanged("LongPressure"); }
        }
        public string LongWindSpeed
        {
            get { return _longWindSpeed; }
            set { _longWindSpeed = value; FirePropertyChanged("LongWindSpeed"); }
        }
        public string Humidity
        {
            get { return _humidity; }
            set
            {
                _humidity = value;
                FirePropertyChanged("Humidity");
            }
        }

        public string SpeedUnit
        {
            get { return _speedunit; }
            set
            {
                _speedunit = value;
                FirePropertyChanged("SpeedUnit");
            }
        }
        public string DistanceUnit
        {
            get { return _distanceUnit; }
            set
            {
                _distanceUnit = value;
                FirePropertyChanged("DistanceUnit");
            }
        }

        public string VisibileDistance
        {
            get { return _visibileDistance; }
            set
            {
                _visibileDistance = value;
                FirePropertyChanged("VisibileDistance");
            }
        }

        public string LongVisibility
        {
            get { return _longVisibility; }
            set
            {
                _longVisibility = value;
                FirePropertyChanged("LongVisibility");
            }
        }

        public string Chill
        {
            get { return _chill; }
            set
            {
                _chill = value;
                FirePropertyChanged("Chill");
            }
        }

        public string Direction
        {
            get
            {
                if (!string.IsNullOrEmpty(_direction))
                {
                    var heading = Int32.Parse(_direction);
                    if (heading < 12) _translatedirection = "N";
                    else if (heading < 34) _translatedirection = "NNE";
                    else if (heading < 57) _translatedirection = "NE";
                    else if (heading < 79) _translatedirection = "ENE";
                    else if (heading < 102) _translatedirection = "E";
                    else if (heading < 124) _translatedirection = "ESE";
                    else if (heading < 147) _translatedirection = "SE";
                    else if (heading < 169) _translatedirection = "SSE";
                    else if (heading < 191) _translatedirection = "S";
                    else if (heading < 214) _translatedirection = "SSW";
                    else if (heading < 237) _translatedirection = "SW";
                    else if (heading < 259) _translatedirection = "WSW";
                    else if (heading < 282) _translatedirection = "W";
                    else if (heading < 304) _translatedirection = "WNW";
                    else if (heading < 327) _translatedirection = "NW";
                    else if (heading < 349) _translatedirection = "NNW";
                    else _translatedirection = "N";
                }
                return _translatedirection;
            }
            set
            {
                _direction = value;
                FirePropertyChanged("Direction");
            }
        }

        public string Speed
        {
            get { return _speed; }
            set
            {
                _speed = value;
                FirePropertyChanged("Speed");
            }
        }
        public string Sunrise
        {
            get { return _sunrise; }
            set { _sunrise = value; FirePropertyChanged("Sunrise"); }
        }
        public string Sunset
        {
            get { return _sunset;}
            set { _sunset = value; FirePropertyChanged("Sunset"); }
        }
        public string Pressure
        {
            get { return _pressure; }
            set
            {
                _pressure = value;
                FirePropertyChanged("Pressure");
            }
        }

        public string PressureUnit
        {
            get { return _pressureunit; }
            set
            {
                _pressureunit = value;
                FirePropertyChanged("PressureUnit");
            }
        }
        public ArrayListDataSet Forecast
        {
            get { return _forecast; }
        }
        public ArrayListDataSet ExtendedForecast
        {
            get { return _extendedForecast; }
        }
        
        public void GetWeatherInfo()
        {
            WebClient client = new WebClient();
            XmlDocument xDoc = new XmlDocument();
            if (string.IsNullOrEmpty(Application.CurrentInstance.Config.YahooWeatherFeed))
            {
                ProvideNullData();
            }
            else
            {
                try
                {
                    if (IsRefreshRequired())
                    {
                        client.DownloadFile(Feed, DownloadToFilePath);
                        Stream strm = client.OpenRead(Feed);
                        StreamReader sr = new StreamReader(strm);
                        string strXml = sr.ReadToEnd();
                        xDoc.LoadXml(strXml);
                    }
                    else
                    {
                        xDoc.Load(DownloadToFilePath);
                    }

                    ParseYahooWeatherDoc(xDoc);
                }
                catch (Exception e)
                {
                    Logger.ReportException("Yahoo weather refresh failed", e);
                }
                finally
                {
                    client.Dispose();
                }
            }
        }

        private bool IsRefreshRequired()
        {
            if (File.Exists(DownloadToFilePath))
            {
                FileInfo fi = new FileInfo(DownloadToFilePath);
                if (fi.LastWriteTime < DateTime.Now.AddHours(-(RefreshIntervalHrs)))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            // If we get to this stage that means the file does not exists, and we should force a refresh
            return true;
        }

        private void ProvideNullData()
        {
            this.Temp = "nulldata";
        }

        private void ParseYahooWeatherDoc(XmlDocument xDoc)
        {
            //Setting up NSManager
            XmlNamespaceManager man = new XmlNamespaceManager(xDoc.NameTable);
            man.AddNamespace("yweather", "http://xml.weather.yahoo.com/ns/rss/1.0");

            Unit = xDoc.SelectSingleNode("rss/channel/yweather:units", man).Attributes["temperature"].Value.ToString();
            SpeedUnit = xDoc.SelectSingleNode("rss/channel/yweather:units", man).Attributes["speed"].Value.ToString();
            PressureUnit = xDoc.SelectSingleNode("rss/channel/yweather:units", man).Attributes["pressure"].Value.ToString();
            DistanceUnit = xDoc.SelectSingleNode("rss/channel/yweather:units", man).Attributes["distance"].Value.ToString();
            
            CodeDescription = xDoc.SelectSingleNode("rss/channel/item/yweather:condition", man).Attributes["text"].Value.ToString();
            Location = xDoc.SelectSingleNode("rss/channel/yweather:location", man).Attributes["city"].Value.ToString();
            Humidity = xDoc.SelectSingleNode("rss/channel/yweather:atmosphere", man).Attributes["humidity"].Value.ToString();
            VisibileDistance = xDoc.SelectSingleNode("rss/channel/yweather:atmosphere", man).Attributes["visibility"].Value.ToString();
            Chill = xDoc.SelectSingleNode("rss/channel/yweather:wind", man).Attributes["chill"].Value.ToString();
            Direction = xDoc.SelectSingleNode("rss/channel/yweather:wind", man).Attributes["direction"].Value.ToString();
            Speed = xDoc.SelectSingleNode("rss/channel/yweather:wind", man).Attributes["speed"].Value.ToString();
            Pressure = xDoc.SelectSingleNode("rss/channel/yweather:atmosphere", man).Attributes["pressure"].Value.ToString();
            Sunrise = xDoc.SelectSingleNode("rss/channel/yweather:astronomy", man).Attributes["sunrise"].Value.ToString();
            Sunset = xDoc.SelectSingleNode("rss/channel/yweather:astronomy", man).Attributes["sunset"].Value.ToString();
            Temp = xDoc.SelectSingleNode("rss/channel/item/yweather:condition", man).Attributes["temp"].Value.ToString();
            Code = xDoc.SelectSingleNode("rss/channel/item/yweather:condition", man).Attributes["code"].Value.ToString();
            ImageUrl = string.Format("resx://MediaBrowser/MediaBrowser.Resources/_{0}", this.Code);
            //this.ImageUrl = string.Format("http://l.yimg.com/a/i/us/we/52/{0}.gif", this.Code);
            LongTemp = string.Format("{0}°{1} {2}", Temp, Unit, CodeDescription);
            LongPressure = string.Format("{0} {1}", Pressure, PressureUnit);
            LongWindSpeed = string.Format("{0} {1} {2}", Speed, SpeedUnit, Direction);
            LongVisibility = string.Format("{0} {1}", VisibileDistance, DistanceUnit);

            var tempForecast = xDoc.SelectNodes("rss/channel/item/yweather:forecast", man);
            //<yweather:forecast day="Fri" date="24 Apr 2009" low="50" high="63" text="Partly Cloudy" code="30" />
            foreach (XmlNode temp in tempForecast)
            {
                var fi = new ForecastItem();
                fi.Day = temp.Attributes["day"].Value.ToString();
                fi.Date = temp.Attributes["date"].Value.ToString();
                fi.Low = temp.Attributes["low"].Value.ToString();
                fi.High = temp.Attributes["high"].Value.ToString();
                fi.Code = temp.Attributes["code"].Value.ToString();
                fi.CodeDescription = temp.Attributes["text"].Value.ToString();
                fi.ImageUrl = string.Format("resx://MediaBrowser/MediaBrowser.Resources/_{0}", fi.Code);
                if (_forecast.Count < 2)
                {
                    _forecast.Add(fi);
                }
                _extendedForecast.Add(fi);
            }

        }
    }

    public class ForecastItem : ModelItem
    {
        public ForecastItem()
        {
        }

        #region fields
        string _imageUrl, _code , _codeDescription, _day, _date, _low, _high = string.Empty;

        public string Code
        {
            get { return _code; }
            set { _code = value; FirePropertyChanged("Code"); }
        }

        public string CodeDescription
        {
            get { return _codeDescription; }
            set { _codeDescription = value; FirePropertyChanged("CodeDescription"); }
        }

        public string ImageUrl
        {
            get { return _imageUrl; }
            set { _imageUrl = value; FirePropertyChanged("ImageUrl"); }
        }


        public string Day
        {
            get { return _day; }
            set { _day = value; FirePropertyChanged("Day"); }
        }

        public string Date
        {
            get { return _date; }
            set { _date = value; FirePropertyChanged("Date"); }
        }

        public string Low
        {
            get { return _low; }
            set { _low = value; FirePropertyChanged("Low"); }
        }
        public string High
        {
            get { return _high; }
            set { _high = value; FirePropertyChanged("High"); }
        }
        #endregion

    }
}
