using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Editing;
using Microsoft.Graphics.Canvas;

namespace VideoExportHelper
{
    public interface IExportProvider
    {
        /// <summary>
        /// 帧率
        /// </summary>
        uint FrameRate { get; }

        /// <summary>
        /// 时长
        /// </summary>
        TimeSpan Duration { get; }

        /// <summary>
        /// 导出区域
        /// </summary>
        Rect ExportArea { get; }

        /// <summary>
        /// 导出尺寸
        /// </summary>
        Size ExportSize { get; }

        /// <summary>
        /// 分组视频导出信息
        /// </summary>
        IList<ExportPartConfig> ExportPartConfigs { get; }

        /// <summary>
        /// 音轨列表
        /// </summary>
        IList<BackgroundAudioTrack> GlobalBackgroundAudioTracks { get; }
        /// <summary>
        /// 创建水印覆盖层
        /// </summary>
        /// <returns></returns>
        MediaOverlayLayer CreateWatermarkLayer();

        /// <summary>
        /// 帧画面回调
        /// </summary>
        /// <param name="targetDrawSession"></param>
        void DrawFrame(CanvasDrawingSession targetDrawSession, TimeSpan position);
    }
}
