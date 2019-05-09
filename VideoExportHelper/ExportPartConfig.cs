using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Editing;

namespace VideoExportHelper
{
    public class ExportPartConfig
    {
        /// <summary>
        /// 开始时间
        /// </summary>
        public TimeSpan Start { get; set; }
        /// <summary>
        /// 视频层延迟时间
        /// </summary>
        public TimeSpan Delay { get; set; }
        /// <summary>
        /// 总时长
        /// </summary>
        public TimeSpan Duration { get; set; }
        /// <summary>
        /// 音轨列表
        /// </summary>
        public IList<BackgroundAudioTrack> BackgroundAudioTracks { get; set; }
        /// <summary>
        /// 背景视频层
        /// </summary>
        public IList<MediaClip> BackgroundVideoClips { get; set; }
    }
}
