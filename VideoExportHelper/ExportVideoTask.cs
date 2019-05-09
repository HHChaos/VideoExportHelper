using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.UI;
using Microsoft.Graphics.Canvas;

namespace VideoExportHelper
{
    public class ExportVideoTask
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly SynchronizationContext _context;
        private readonly Task _exportTask;

        public ExportVideoTask(IExportProvider provider, StorageFolder destinationFolder, string fileName)
        {
            ExportProvider = provider;
            DestinationFolder = destinationFolder;
            FileName = fileName;
            _context = SynchronizationContext.Current;
            _cancellationTokenSource = new CancellationTokenSource();
            var obj = new Tuple<StorageFolder, IExportProvider, string>(DestinationFolder, provider, fileName);
            _exportTask = CreateExportTask(obj, _cancellationTokenSource.Token);
            _cancellationTokenSource.Token.Register(ClearCacheAsync);
        }

        public StorageFolder DestinationFolder { get; }
        public string FileName { get; }
        public int Progress { get; private set; }
        public IExportProvider ExportProvider { get; }
        public bool Canceled => _cancellationTokenSource.IsCancellationRequested;
        public event EventHandler<ExportProgressEventArgs> ExportProgressChanged;
        public event EventHandler<EventArgs> ExportComplated;
        public event EventHandler<EventArgs> ExportFailed;

        private Task CreateExportTask(object obj, CancellationToken cancellationToken)
        {
            return new Task(() =>
            {
                var para = obj as Tuple<StorageFolder, IExportProvider, string>;
                var folder = para?.Item1;
                var provider = para?.Item2;
                var fileName = para?.Item3;
                var mediaEncodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Wvga);
                if (provider?.ExportPartConfigs == null ||
                    folder == null ||
                    provider.ExportPartConfigs.Count < 1 ||
                    string.IsNullOrEmpty(fileName) ||
                    Math.Abs(provider.ExportSize.Width) < 1 ||
                    Math.Abs(provider.ExportSize.Height) < 1 ||
                    mediaEncodingProfile?.Video == null)
                {
                    UpdateExportStatus(false);
                    return;
                }

                var exportSize = provider.ExportSize;
                mediaEncodingProfile.Video.Width = (uint) exportSize.Width;
                mediaEncodingProfile.Video.Height = (uint) exportSize.Height;
                mediaEncodingProfile.Video.FrameRate.Numerator = 30;
                var cacheFolder = GetCacheFolder().GetAwaiter().GetResult();
                var device = CanvasDevice.GetSharedDevice();
                var mediafileList = new List<StorageFile>();
                var defaultBackgroud =
                    new CanvasRenderTarget(device, (float) exportSize.Width, (float) exportSize.Height, 96f);
                using (var session = defaultBackgroud.CreateDrawingSession())
                {
                    session.Clear(Colors.White);
                }

                var scale = exportSize.Width / provider.ExportArea.Width;
                var transform = Matrix3x2.CreateScale((float) scale);
                var offsetPoint =
                    Vector2.Transform(new Point(-provider.ExportArea.X, -provider.ExportArea.Y).ToVector2(), transform);
                transform.Translation = offsetPoint;
                var framePosition = new Rect(new Point(), exportSize);
                var timeGap = TimeSpan.FromSeconds(1) / provider.FrameRate;
                foreach (var videoPartConfig in provider.ExportPartConfigs)
                {
                    var composition = new MediaComposition();
                    var layerTmp = new MediaOverlayLayer();
                    var tmpFrameImgs = new List<CanvasBitmap>();
                    if (videoPartConfig.BackgroundVideoClips?.Count > 0)
                    {
                        if (videoPartConfig.Delay.TotalSeconds > 0)
                        {
                            var blankClip = MediaClip.CreateFromColor(Colors.White, videoPartConfig.Delay);
                            composition.Clips.Add(blankClip);
                        }

                        foreach (var videoClip in videoPartConfig.BackgroundVideoClips)
                            composition.Clips.Add(videoClip);
                        if (composition.Duration < videoPartConfig.Duration)
                        {
                            var blankClip = MediaClip.CreateFromColor(Colors.White,
                                videoPartConfig.Duration - composition.Duration);
                            composition.Clips.Add(blankClip);
                        }
                    }

                    else
                    {
                        composition.Clips.Add(MediaClip.CreateFromSurface(defaultBackgroud, videoPartConfig.Duration));
                    }

                    if (videoPartConfig.BackgroundAudioTracks?.Count > 0)
                        foreach (var track in videoPartConfig.BackgroundAudioTracks)
                        {
                            track.Delay -= videoPartConfig.Start;
                            composition.BackgroundAudioTracks.Add(track);
                        }

                    for (var currentPosition = videoPartConfig.Start;
                        currentPosition < videoPartConfig.Start + videoPartConfig.Duration;
                        currentPosition += timeGap)
                    {
                        if (Canceled) return;

                        var progress = (int) (currentPosition / provider.Duration * 100 * 0.5);
                        UpdateProgress(progress);

                        var frame = new CanvasRenderTarget(device, (float) exportSize.Width, (float) exportSize.Height,
                            96f);
                        using (var session = frame.CreateDrawingSession())
                        {
                            session.Clear(Colors.Transparent);
                            session.Transform = transform;
                            provider.DrawFrame(session, currentPosition);
                        }

                        tmpFrameImgs.Add(frame);
                        var clip = MediaClip.CreateFromSurface(frame, timeGap);
                        var tmpLayer = new MediaOverlay(clip)
                        {
                            Position = framePosition,
                            Opacity = 1f,
                            Delay = currentPosition - videoPartConfig.Start
                        };
                        layerTmp.Overlays.Add(tmpLayer);
                    }

                    composition.OverlayLayers.Add(layerTmp);
                    var mediaPartFile = cacheFolder.CreateFileAsync(
                            $"part_{mediafileList.Count}.mp4", CreationCollisionOption.ReplaceExisting).GetAwaiter()
                        .GetResult();
                    composition.RenderToFileAsync(mediaPartFile, MediaTrimmingPreference.Fast,
                        mediaEncodingProfile).GetAwaiter().GetResult();
                    mediafileList.Add(mediaPartFile);

                    foreach (var item in tmpFrameImgs) item.Dispose();

                    tmpFrameImgs.Clear();
                }

                defaultBackgroud?.Dispose();
                var mediaComposition = new MediaComposition();

                #region connect video

                foreach (var mediaPartFile in mediafileList)
                {
                    if (Canceled) return;
                    var mediaPartClip = MediaClip.CreateFromFileAsync(mediaPartFile).GetAwaiter().GetResult();
                    mediaComposition.Clips.Add(mediaPartClip);
                }

                #endregion

                #region add global BackgroundAudioTrack

                if (provider.GlobalBackgroundAudioTracks != null)
                    foreach (var bgm in provider.GlobalBackgroundAudioTracks)
                        mediaComposition.BackgroundAudioTracks.Add(bgm);

                #endregion

                #region add watermark

                var watermarkLayer = provider.CreateWatermarkLayer();
                if (watermarkLayer != null)
                    mediaComposition.OverlayLayers.Add(watermarkLayer);

                #endregion


                var exportFile = folder.CreateFileAsync($"{fileName}.mp4", CreationCollisionOption.ReplaceExisting)
                    .GetAwaiter().GetResult();
                if (Canceled) return;
                var saveOperation = mediaComposition.RenderToFileAsync(exportFile, MediaTrimmingPreference.Fast,
                    mediaEncodingProfile);
                saveOperation.Progress = (info, progress) =>
                {
                    UpdateProgress((int) (50 + progress * 0.5));
                    if (Canceled) saveOperation.Cancel();
                };

                saveOperation.Completed = (info, status) =>
                {
                    if (!Canceled)
                    {
                        var results = info.GetResults();
                        if (results != TranscodeFailureReason.None || status != AsyncStatus.Completed)
                            UpdateExportStatus(false);
                        else
                            UpdateExportStatus(true);
                    }

                    ClearCacheAsync();
                };
            }, cancellationToken);
        }

        private async Task<StorageFolder> GetCacheFolder()
        {
            return await ApplicationData.Current.TemporaryFolder.CreateFolderAsync("ExportVideoCache",
                CreationCollisionOption.OpenIfExists);
        }

        public void Start()
        {
            if (Canceled)
                return;
            _exportTask?.Start();
        }

        public void Cancel()
        {
            if (Canceled)
                return;
            if (_cancellationTokenSource.Token.CanBeCanceled) _cancellationTokenSource.Cancel(true);
        }

        private async void ClearCacheAsync()
        {
            (await GetCacheFolder())?.DeleteAsync();
        }

        private void UpdateProgress(int progress)
        {
            _context.Post(_ =>
            {
                Progress = progress;
                ExportProgressChanged?.Invoke(this, new ExportProgressEventArgs(progress));
            }, null);
        }

        private void UpdateExportStatus(bool succeed)
        {
            _context.Post(_ =>
            {
                if (succeed)
                    ExportComplated?.Invoke(this, EventArgs.Empty);
                else
                    ExportFailed?.Invoke(this, EventArgs.Empty);
            }, null);
        }
    }
}