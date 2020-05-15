﻿using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.MediaFoundation;
using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using VL.Core;

using Xenko.Graphics;

namespace VL.MediaFoundation
{
    // Good source: https://stackoverflow.com/questions/40913196/how-to-properly-use-a-hardware-accelerated-media-foundation-source-reader-to-dec
    public partial class VideoPlayerTexture2D : IDisposable
    {
        private readonly Subject<Texture2D> videoFrames = new Subject<Texture2D>();
        private (Task, CancellationTokenSource) currentPlayback;

        public VideoPlayerTexture2D()
        {
        }

        public void Update(
            string url = "http://www.peach.themazzone.com/durian/movies/sintel-1024-surround.mp4",
            bool play = false,
            float rate = 1f,
            float seekTime = 0f,
            bool seek = false,
            float loopStartTime = 0f,
            float loopEndTime = -1f,
            bool loop = false,
            float volume = 1f)
        {
            Url = url;
            Play = play;
            Rate = rate;
            SeekTime = seekTime;
            Seek = seek;
            LoopStartTime = loopStartTime;
            LoopEndTime = loopEndTime;
            Loop = loop;
            Volume = volume;
        }

        public IObservable<Texture2D> Frames => videoFrames;

        public string Url
        {
            get => url;
            set
            {
                if (value != url)
                {
                    url = value;

                    StopCurrentPlayback();

                    var cts = new CancellationTokenSource();
                    var task = Task.Run(() => PlayUrl(url, cts.Token));
                    currentPlayback = (task, cts);
                }
            }
        }
        string url;

        public bool Play { get; set; } = true;

        public float Rate { get; set; } = 1f;

        public float SeekTime { get; set; }

        public bool Seek { get; set; }

        public float LoopStartTime { get; set; }

        public float LoopEndTime { get; set; } = float.MaxValue;

        public bool Loop { get; set; } = true;

        public float Volume { get; set; } = 1f;

        public float CurrentTime { get; private set; }

        public float Duration { get; private set; }

        async Task PlayUrl(string url, CancellationToken token)
        {
            // Reset outputs
            CurrentTime = default;
            Duration = default;

            // Initialize MediaFoundation
            MediaManagerService.Initialize();

            // Hardware acceleration
            using var d3dDevice = new SharpDX.Direct3D11.Device(DriverType.Hardware, SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport | SharpDX.Direct3D11.DeviceCreationFlags.VideoSupport);

            // Add multi thread protection on device (MF is multi-threaded)
            using var deviceMultithread = d3dDevice.QueryInterface<DeviceMultithread>();
            deviceMultithread.SetMultithreadProtected(true);

            // Reset device
            using var manager = new DXGIDeviceManager();
            manager.ResetDevice(d3dDevice);

            using var classFactory = new MediaEngineClassFactory();
            using var mediaEngineAttributes = new MediaEngineAttributes()
            {
                // To use WIC disable
                DxgiManager = manager,
                VideoOutputFormat = (int)SharpDX.DXGI.Format.B8G8R8A8_UNorm
            };
            using var engine = new MediaEngine(classFactory, mediaEngineAttributes);
            using var engineEx = engine.QueryInterface<MediaEngineEx>();

            // Wait for MediaEngine to be ready
            await engine.LoadAsync(url, token);

            engine.GetNativeVideoSize(out var width, out var height);
            Duration = (float)engine.Duration;

            //var fac = new ImagingFactory();
            var textureDesc = new Texture2DDescription()
            {
                Width = width,
                Height = height,
                MipLevels = 0,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            var renderTarget = new Texture2D(d3dDevice, textureDesc);

            textureDesc.BindFlags = BindFlags.None;
            var renderTextureOut = new Texture2D(d3dDevice, textureDesc);


            while (!token.IsCancellationRequested)
            {
                if (Loop != engine.Loop)
                    engine.Loop = Loop;

                if (Rate != engine.PlaybackRate)
                {
                    engine.PlaybackRate = Rate;
                    engine.DefaultPlaybackRate = Rate;
                }

                var volume = VLMath.Clamp(Volume, 0f, 1f);
                if (volume != engine.Volume)
                    engine.Volume = volume;

                if (Seek)
                {
                    var seekTime = VLMath.Clamp(SeekTime, 0, Duration);
                    await engine.SetCurrentTimeAsync(seekTime, token);
                }

                // Check playing state
                if (Play)
                {
                    if (engine.IsPaused)
                        await engine.PlayAsync(token);
                }
                else
                {
                    if (!engine.IsPaused)
                        await engine.PauseAsync(token);
                    else
                        await Task.Delay(10);

                    continue;
                }

                if (engine.OnVideoStreamTick(out var presentationTimeTicks))
                {
                    // Not sure why but sometimes we get a negative number here and the pipeline seems stuck as long as we don't hit play again
                    if (presentationTimeTicks < 0)
                    {
                        await engine.PlayAsync(token);
                        continue;
                    }

                    var currentTime = CurrentTime = (float)TimeSpan.FromTicks(presentationTimeTicks).TotalSeconds;

                    if (Loop || presentationTimeTicks < 0)
                    {
                        var loopStartTime = VLMath.Clamp(LoopStartTime, 0f, Duration);
                        var loopEndTime = VLMath.Clamp(LoopEndTime < 0 ? float.MaxValue : LoopEndTime, 0f, Duration);
                        if (currentTime < loopStartTime || currentTime > loopEndTime)
                        {
                            if (Rate >= 0)
                                await engine.SetCurrentTimeAsync(loopStartTime, token);
                            else
                                await engine.SetCurrentTimeAsync(loopEndTime, token);

                            continue;
                        }
                    }

                    engine.TransferVideoFrame(renderTarget, default, new SharpDX.Mathematics.Interop.RawRectangle(0, 0, width, height), default);

                    //var deviceContext = d3dDevice.ImmediateContext;
                    //deviceContext.Flush();
                    var deviceContext = d3dDevice.ImmediateContext;
                    deviceContext.CopyResource(renderTarget, renderTextureOut);

                    try
                    {
                        //Utils.Swap(ref renderTarget, ref renderTextureOut);
                        videoFrames.OnNext(renderTextureOut);
                    }
                    catch (Exception e)
                    {

                        throw new Exception(e.ToString());
                    }

                }
            }

            engine.Shutdown();
        }

        void StopCurrentPlayback()
        {
            var (currentTask, currentCts) = currentPlayback;
            if (currentTask != null)
            {
                currentCts.Cancel();
                try
                {
                    currentTask.Wait();
                }
                catch (Exception e)
                {
                    Trace.TraceError(e.ToString());
                }
                currentCts.Dispose();
            }
            currentPlayback = default;
        }

        public void Dispose()
        {
            StopCurrentPlayback();
            videoFrames.Dispose();
        }
    }
}
