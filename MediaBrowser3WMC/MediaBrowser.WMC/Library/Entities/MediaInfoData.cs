using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.MediaCenter.UI;
using MediaBrowser.Library.Persistance;
using MediaBrowser.LibraryManagement;
using MediaBrowser.Library.Entities.Attributes;

namespace MediaBrowser.Library.Entities
{
    public class MediaInfoData
    {
        public class MIData
        {
            [Persist]
            public int Height = 0;
            [Persist]
            public int Width = 0;
            [Persist]
            public string VideoCodec = "";
            [Persist]
            public string AudioFormat = "";
            [Persist]
            public int VideoBitRate = 0;
            [Persist]
            public int AudioBitRate = 0;
            [Persist]
            public int RunTime = 0;
            [Persist]
            public int AudioStreamCount = 0;
            [Persist]
            public string AudioChannelCount = "";
            [Persist]
            public string AudioProfile = "";
            [Persist]
            public string AudioLanguages = "";
            [Persist]
            public string Subtitles = "";
            [Persist]
            public string VideoFPS = "";
            [Persist]
            public string ScanType = "";
        }

        [Persist]
        private MIData _pluginData = new MIData();
        [Persist]
        private MIData _overrideData = new MIData();

        public MIData PluginData
        {
            get
            {
                if (_pluginData == null) _pluginData = new MIData();
                return _pluginData;
            }
            set
            {
                _pluginData = value;
            }
        }

        public MIData OverrideData
        {
            get
            {
                if (_overrideData == null) _overrideData = new MIData();
                return _overrideData;
            }
            set
            {
                _overrideData = value;
            }
        }

        public int Height
        {
            get
            {
                return OverrideData.Height > 0 ? OverrideData.Height : PluginData.Height;
            }
        }

        public int Width
        {
            get
            {
                return OverrideData.Width > 0 ? OverrideData.Width : PluginData.Width;
            }
        }

        public string VideoCodec
        {
            get
            {
                return !string.IsNullOrEmpty(OverrideData.VideoCodec) ? OverrideData.VideoCodec : PluginData.VideoCodec;
            }
        }

        public string AudioFormat
        {
            get
            {
                return !string.IsNullOrEmpty(OverrideData.AudioFormat) ? OverrideData.AudioFormat : PluginData.AudioFormat;
            }
        }

        public string AudioProfile
        {
            get
            {
                return !string.IsNullOrEmpty(OverrideData.AudioFormat) ? OverrideData.AudioProfile : PluginData.AudioProfile;
            }
        }

        public int VideoBitRate
        {
            get
            {
                return OverrideData.VideoBitRate > 0 ? OverrideData.VideoBitRate : PluginData.VideoBitRate;
            }
        }

        public int AudioBitRate
        {
            get
            {
                return OverrideData.AudioBitRate > 0 ? OverrideData.AudioBitRate : PluginData.AudioBitRate;
            }
        }

        public int RunTime
        {
            get
            {
                return OverrideData.RunTime > 0 ? OverrideData.RunTime : PluginData.RunTime;
            }
        }

        public int AudioStreamCount
        {
            get
            {
                return OverrideData.AudioStreamCount > 0 ? OverrideData.AudioStreamCount : PluginData.AudioStreamCount;
            }
        }

        public string AudioChannelCount
        {
            get
            {
                return !string.IsNullOrEmpty(OverrideData.AudioChannelCount) ? OverrideData.AudioChannelCount : PluginData.AudioChannelCount;
            }
        }

        public string AudioLanguages
        {
            get
            {
                return !string.IsNullOrEmpty(OverrideData.AudioLanguages) ? OverrideData.AudioLanguages : PluginData.AudioLanguages;
            }
        }

        public string Subtitles
        {
            get
            {
                return !string.IsNullOrEmpty(OverrideData.Subtitles) ? OverrideData.Subtitles : PluginData.Subtitles;
            }
        }

        public string VideoFPS
        {
            get
            {
                return !string.IsNullOrEmpty(OverrideData.VideoFPS) ? OverrideData.VideoFPS : PluginData.VideoFPS;
            }
        }

        public string ScanType
        {
            get
            {
                return !string.IsNullOrEmpty(OverrideData.ScanType) ? OverrideData.ScanType : PluginData.ScanType;
            }
        }

        public readonly static MediaInfoData Empty = new MediaInfoData();

        string SizeStr
        {
            get
            {
                if (Height > 0 && Width > 0)
                    return Width + "x" + Height + ", ";
                else
                    return "";
            }
        }
        string VideoRateStr
        {
            get
            {
                if (VideoBitRate >= 10000)
                    return (VideoBitRate / 1000).ToString() + " " + Kernel.Instance.StringData.GetString("KBsStr");
                else
                    if (VideoBitRate > 0)
                    return VideoBitRate.ToString() + " " + Kernel.Instance.StringData.GetString("KBsStr");
                else
                    return "";
            }
        }

        string AudioRateStr
        {
            get
            {
                if (AudioBitRate >= 10000)
                    return (AudioBitRate / 1000).ToString() + " " + Kernel.Instance.StringData.GetString("KBsStr");
                else
                    if (AudioBitRate > 0)
                        return AudioBitRate.ToString() + " " + Kernel.Instance.StringData.GetString("KBsStr");
                    else
                        return "";
            }
        }

        public string CombinedInfo
        {
            get
            {
                if (this.VideoCodecExtendedString != null && this.VideoCodecExtendedString != "")
                {
                    return string.Format("{0}{1}, {2}", this.SizeStr, this.VideoCodecExtendedString, this.AudioCodecExtendedString);
                }
                else 
                {
                    return string.Format("{0}{1}", this.SizeStr, this.AudioCodecExtendedString);
                }
            }
        }

        #region Properties Video

        protected static Dictionary<string, string> VideoImageNames = new Dictionary<string, string>()
        {
            {"divx 5","divx"},
            {"divx 4","divx"},
            {"divx 3 low","divx"},
            {"avc","H264"},
            {"v_mpeg4/iso/avc","H264"},
            {"vc-1","vc1"},
            {"wmv1","wmv"},
            {"wmv2","wmv"},
            {"wmv3","wmv"},
            {"wmv3hd","wmv_hd"},
            {"wmvhd","wmv_hd"},
            {"wmv hd","wmv_hd"},
            {"wvc1","wmv"},
            {"wvc1hd","wmv_hd"},
            {"mpeg video","mpegvideo"},
            {"mpeg-4 visual","mpeg4visual"},
            {"v_mpeg4/iso/sp","mpeg4visual"},
            {"v_mpeg4/iso/asp","mpeg4visual"},
            {"v_mpeg4/iso/ap","mpeg4visual"},
            {"v_mpeg4/ms/v3","mpeg4visual"},
            {"mpeg-1 video","mpeg1video"},
            {"mpeg-2 video","H262"},
            {"on2 vp6","on2_vp6"},
            {"sorenson h263","sorenson_H263"},
        };

        protected string VideoImageName {
            get {
                //first look for hd value if we are hd
                if (Width >= 1280 || Height >= 700) {
                    if (VideoImageNames.ContainsKey(VideoCodecString.ToLower()+"hd")) return "codec_" + VideoImageNames[VideoCodecString.ToLower()+"hd"];
                }
                //next see if there is a translation for our codec
                if (VideoImageNames.ContainsKey(VideoCodecString.ToLower())) return "codec_" + VideoImageNames[VideoCodecString.ToLower()];
                //finally, just try the codec itself
                return "codec_"+VideoCodecString.ToLower();
            }
        }

        public Image VideoCodecImage
        {
            get
            {
                return Helper.GetMediaInfoImage(VideoImageName);
            }
        }

        public string VideoResolutionString
        {
            get
            {
                if (Height > 0 && Width > 0)
                    return string.Format("{0}x{1}", this.Width, this.Height);
                else
                    return "";
            }
        }

        public string VideoResolutionExtendedString
        {
            get
            {
                if (this.VideoFrameRateString != null && this.VideoFrameRateString != "")
                {
                    return string.Format("{0} {1} {2}", this.VideoResolutionString, Kernel.Instance.StringData.GetString("AtStr"), this.VideoFrameRateString);
                }
                else
                    return this.VideoResolutionString;
            }
        }

        public string ScanTypeString
        {
            get
            {
                if (this != Empty)
                    return string.Format("{0}", this.ScanType);
                else
                    return "";
            }
        }

        public string ScanTypeChar
        {
            get
            {
                if (this != Empty)
                {
                    if (this.ScanType == "Progressive") return "p";
                    if (this.ScanType == "Interlaced") return "i";
                    if (this.ScanType == "MBAFF") return "i";
                    else return "";
                }
                else
                    return "";
            }
        }

        public string VideoCodecString
        {
            get
            {
                if (this != Empty)
                    return string.Format("{0}", this.VideoCodec);
                else
                    return "";
            }
        }

        public string AspectRatioString
        {
            get
            {
                if (this != Empty)
                {
                    Single width = (Single)this.Width;
                    Single height = (Single)this.Height;
                    Single temp = (width / height);

                    if (temp < 1.4)
                        return "4:3";
                    else if (temp >= 1.4 && temp <= 1.55)
                        return "3:2";
                    else if (temp > 1.55 && temp <= 1.8)
                        return "16:9";
                    else if (temp > 1.8 && temp <= 2)
                        return "1.85:1";
                    else if (temp > 2)
                        return "2.39:1";
                    else
                        return "";
                }
                else
                    return "";
            }
        }

        public string RuntimeString
        {
            get
            {
                if (RunTime != 0)
                {
                    return RunTime.ToString() + " " + Kernel.Instance.StringData.GetString("MinutesStr");
                }
                else return "";
            }
        }

        public string VideoFrameRateString
        {
            get
            {
                if (this.VideoFPS != null && this.VideoFPS != "" && this.VideoFPS != "0")
                {
                    return VideoFPS.ToString() + " " + Kernel.Instance.StringData.GetString("FrameRateStr");
                }   
                else return "";
            }
        }

        public string VideoCodecExtendedString
        {
            get
            {
                if (this.VideoRateStr != null && this.VideoRateStr != "")
                {
                    return string.Format("{0} {1} {2}", this.VideoCodec, Kernel.Instance.StringData.GetString("AtStr"), this.VideoRateStr);
                }
                else
                    return this.VideoCodec;
            }
        }
        #endregion

        #region Properties Audio

        protected static Dictionary<string, string> AudioImageNames = new Dictionary<string, string>()
        {
            {"aac","Aac"},
            {"ac-3","Ac3"},
            {"ac-3 dolby digital","Ac3"},     		
            {"e-ac-3","DDPlus"},
            {"ac-3 truehd","DDTrueHD"},
            {"truehd","DDTrueHD"},
            {"dts","DTS_DS"},
            {"dts 96/24","DTS_9624"},
            {"dts es","DTS_ES"},
            {"dts hra","DTS_HD_HRA"},
            {"dts ma","DTS_HD_MA"},
            {"flac","Flac"},
            {"mpeg audio","MpegAudio"},
            {"mpeg audio layer 1","MpegAudio"},
            {"mpeg audio layer 2","MpegAudio"},
            {"mpeg audio layer 3","Mp3"},       
            {"wma","Wma"},
            {"wma2","Wma"},
            {"wma3","Wma"},            
            {"vorbis","Vorbis"},
			
            //Legacy values not needed with change to seperate channel icons 
            {"ac-3 1","DD_10"},
            {"ac-3 2","DD_20"},
            {"ac-3 3","DD_30"},
            {"ac-3 6","DD_51"},
            {"ac-3 dolby digital 1","DD_10"},
            {"ac-3 dolby digital 2","DD_20"},
            {"ac-3 dolby digital 3","DD_30"},
            {"ac-3 dolby digital 6","DD_51"},
            {"e-ac-3 5","DDPlus_50"},
            {"e-ac-3 6","DDPlus_51"},
            {"e-ac-3 7","DDPlus_61"},
            {"e-ac-3 8","DDPlus_71"},
            {"truehd 5","DDTrueHD_50"},
            {"truehd 6","DDTrueHD_51"},
            {"truehd 7","DDTrueHD_61"},
            {"truehd 8","DDTrueHD_71"},
            {"ac-3 truehd 5","DDTrueHD_50"},
            {"ac-3 truehd 6","DDTrueHD_51"},
            {"ac-3 truehd 7","DDTrueHD_61"},
            {"ac-3 truehd 8","DDTrueHD_71"},            
            {"dts 1","DTS_DS_10"},
            {"dts 2","DTS_DS_20"},
            {"dts 6","DTS_DS_51"},
            {"dts 96/24 6","DTS_9624_51"},
            {"dts es 6","DTS_ES_51"},
            {"dts es 7","DTS_ES_61"},
            {"dts hra 6","DTS_HD_HRA_51"},
            {"dts hra 7","DTS_HD_HRA_61"},
            {"dts hra 8","DTS_HD_HRA_71"},
            {"dts ma 3","DTS_HD_MA_30"},
            {"dts ma 4","DTS_HD_MA_40"},
            {"dts ma 5","DTS_HD_MA_50"},
            {"dts ma 6","DTS_HD_MA_51"},
            {"dts ma 7","DTS_HD_MA_61"},
            {"dts ma 8","DTS_HD_MA_71"}

        };
        protected string AudioImageName {
            get {
                //if (AudioImageNames.ContainsKey(AudioCombinedString.ToLower())) return "codec_"+AudioImageNames[AudioCombinedString.ToLower()];
                //Removed to allow change to seperate channel icons
                if (AudioImageNames.ContainsKey(AudioProfileString.ToLower())) return "codec_"+AudioImageNames[AudioProfileString.ToLower()];
                return "codec_"+AudioProfileString.ToLower(); //not found...
            }
        }

        public Image AudioCodecImage
        {
            get
            {
                return Helper.GetMediaInfoImage(AudioImageName);
            }
        }
           
        public string AudioCodecString
        {
            get
            {
                if (this != Empty)
                    return string.Format("{0}", this.AudioFormat);
                else
                    return "";
            }
        }

        protected string ChannelImageName
        {
            get
            {
                return "channels_"+AudioChannelString.ToLower();
            }
        }

        public Image AudioChannelImage
        {
            get
            {
                return Helper.GetMediaInfoImage(ChannelImageName);
            }
        }

        public string AudioChannelString
        {
            get
            {
                if (this.AudioChannelCount != null && this.AudioChannelCount != "0")
                {				
                    return AudioChannelCount;
                }   
                else return "";				
            }
        }
		
        public string AudioStreamStr
        {
            get
            {
                if (AudioStreamCount > 0)
                    return AudioStreamCount.ToString();
                else
                    return "";
            }
        }

        public string AudioStreamString
       {
            get
            {
                if (this.AudioLanguagesString != null && this.AudioLanguagesString != "")
                {
                    return AudioLanguagesString;
                }
                else
                    return this.AudioStreamStr;
            }
        }

        public string AudioCodecExtendedString
        {
            get
            {
                if (this.AudioRateStr != null && this.AudioRateStr != "")
                {
                    return string.Format("{0} {1} {2}", this.AudioProfileString, Kernel.Instance.StringData.GetString("AtStr"), this.AudioRateStr);
                }
                else
                    return this.AudioProfileString;
            }
        }

        public string AudioProfileString
        {
            get
            {
                if (this.AudioFormat != null)
                {
                    switch (this.AudioFormat.ToLower())
                    {
                        case "ac-3":
                        case "dts":
                        case "mpeg audio":
                            {
                                if (this.AudioProfile != null && this.AudioProfile != "")
                                    return string.Format("{0} {1}", this.AudioFormat, this.AudioProfile);
                                else
                                    return this.AudioFormat;
                            }
                        default:
                            return this.AudioFormat;
                    }
                }
                else return "";
            }
        }

        public string AudioCombinedString
        {
            get
            {
                return string.Format("{0} {1}", this.AudioProfileString, this.AudioChannelString);
            }
        }
        #endregion

        #region Properties General
		
		public string AudioLanguagesString
        {
            get
            {
                if (this.AudioLanguages != null && this.AudioLanguages != " / ")
                {				
                    return AudioLanguages;
                }   
                else return "";				
            }
        }		
		
        public string SubtitleString
        {
            get
            {
                if (this.Subtitles != null && this.Subtitles != " / ")
                {				
                    return Subtitles;
                }   
                else return "";				
            }
        }		
        #endregion
    }
}
