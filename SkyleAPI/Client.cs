using Grpc.Core;
using Skyle_Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Skyle
{
    /// <summary>
    /// Skyle eye tracker client
    /// </summary>
    public class Client : IDisposable
    {
        private Channel channel;
        private Skyle_Server.Skyle.SkyleClient client;
        private Options currentOptions = new Options();
        private CalibControl calibControl = new CalibControl();
        private CalibPoint calibPoint = new CalibPoint();
        private CalibQuality calibQuality = new CalibQuality();
        private CalibImprove calibImprove = new CalibImprove();
        private AsyncDuplexStreamingCall<calibControlMessages, CalibMessages> _duplexStream;
        private AsyncServerStreamingCall<PositioningMessage> positioningStream;
        private AsyncServerStreamingCall<Skyle_Server.Point> gazeStream;
        private Task calibWriterTask, calibReaderTask, gazeReaderTask, positioningReaderTask, triggerReaderTask, connTask;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private BlockingCollection<calibControlMessages> blockingCollection = new BlockingCollection<calibControlMessages>(5);
        private CancellationTokenSource calibCTS = new CancellationTokenSource(), gazeCTS = new CancellationTokenSource(), posCTS = new CancellationTokenSource(), triggerCTS = new CancellationTokenSource();
        private CancellationTokenSource conCTS = new CancellationTokenSource();
        private AsyncServerStreamingCall<TriggerMessage> triggerStream;
        private readonly string host;
        private event Action<Point> _gaze;
        private event Action<Positioning> _positioning;
        private event Action<Trigger> _triggered;
        private event Action<bool> _connected;
        private AsyncServerStreamingCall<Skyle_Server.Profile> profileStream;
        private CancellationTokenSource profilesCTS = new CancellationTokenSource();
        private Task profileReaderTask;
        private bool reconn = false;

        /// <summary>
        /// Ctor with hostname (default skyle.local)
        /// </summary>
        /// <param name="host"></param>
        public Client(string host = "skyle.local")
        {
            Environment.SetEnvironmentVariable("GRPC_DNS_RESOLVER", "native");
            this.host = host;
            _connected += Client_connected;
        }

        /// <summary>
        /// Start a async connection to the skyle eye tracker
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ConnectAsync(double timeoutinseconds = 3)
        {
            try
            {
                channel = new Channel(host, 50052, ChannelCredentials.Insecure);
                client = new Skyle_Server.Skyle.SkyleClient(channel);
                createConnectionTask();
                await channel.ConnectAsync(DateTime.Now.ToUniversalTime().AddSeconds(timeoutinseconds));
                return true;
            }
            catch (TaskCanceledException)
            {
                Logger.Trace($"Connection timeout, can not connect.");
            }
            catch (Exception e)
            {
                Logger.Warn($"Connection Problem: {e.Message}");
            }
            return false;
        }

        private void createConnectionTask()
        {
            if ((connTask == null || conCTS.IsCancellationRequested) && channel != null)
            {
                conCTS = new CancellationTokenSource();
                connTask = Task.Run(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            ChannelState beforeState = channel.State;
                            await channel.TryWaitForStateChangedAsync(beforeState);
                            //if (beforeState != ChannelState.Ready && channel.State != ChannelState.Idle)
                            _connected?.Invoke(channel.State == ChannelState.Ready);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                        conCTS.Cancel();
                        return;
                    }
                }, conCTS.Token);
            }
        }

        private void Client_connected(bool con)
        {
            if (!reconn && !con)
            {
                reconn = true;
                Task.Run(async () =>
                {
                    await channel.ConnectAsync();
                    reconn = false;
                    reInitTasks();
                });
            }
        }

        /// <summary>
        /// Create calib worker tasks
        /// </summary>
        /// <param name="t"></param>
        private void createTasks(CancellationTokenSource t)
        {
            calibWriterTask = Task.Run(async () =>
            {
                try
                {
                    while (!t.IsCancellationRequested)
                    {
                        calibControlMessages mess;
                        if (blockingCollection.TryTake(out mess, -1, t.Token))
                            await _duplexStream.RequestStream.WriteAsync(mess);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Trace(ex);
                }
                finally
                {
                    t.Cancel();
                }
            }, t.Token);
            calibReaderTask = Task.Run(async () =>
            {
                try
                {
                    while (await _duplexStream.ResponseStream.MoveNext(t.Token))
                    {
                        CalibMessages current = _duplexStream.ResponseStream.Current;
                        if (current != null)
                        {
                            switch (current.MessageCase)
                            {
                                case CalibMessages.MessageOneofCase.None:
                                    break;
                                case CalibMessages.MessageOneofCase.CalibControl:
                                    calibControl = current.CalibControl;
                                    break;
                                case CalibMessages.MessageOneofCase.CalibPoint:
                                    calibPoint = current.CalibPoint;
                                    NextCalibPoint?.Invoke(new Point(current.CalibPoint));
                                    break;
                                case CalibMessages.MessageOneofCase.CalibQuality:
                                    calibQuality = current.CalibQuality;
                                    CalibrationFinished?.Invoke();
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Trace(ex);
                }
                finally
                {
                    t.Cancel();
                }
            }, t.Token);
        }

        /// <summary>
        /// Send the calib control message
        /// </summary>
        private void sendCalibControl()
        {
            if (_duplexStream == null || calibCTS.IsCancellationRequested)
            {
                calibCTS = new CancellationTokenSource();
                if (client == null) ConnectAsync().Wait();
                _duplexStream = client.Calibrate(cancellationToken: calibCTS.Token);
                createTasks(calibCTS);
            }
            calibControl.StopHID = false;
            blockingCollection.Add(new calibControlMessages() { CalibControl = calibControl });
        }

        /// <summary>
        /// Next calibration point event
        /// </summary>
        public event Action<Point> NextCalibPoint;

        /// <summary>
        /// Calibration finished event
        /// </summary>
        public event Action CalibrationFinished;

        /// <summary>
        /// IsConnected event
        /// </summary>
        public event Action<bool> IsConnected
        {
            add
            {
                createConnectionTask();
                _connected += value;
            }
            remove => _connected -= value;
        }

        /// <summary>
        /// New gaze data event
        /// </summary>
        public event Action<Point> Gaze
        {
            add
            {
                initGaze();
                _gaze += value;
            }
            remove => _gaze -= value;
        }

        /// <summary>
        /// New gaze data event
        /// </summary>
        public event Action<Trigger> OnTriggered
        {
            add
            {
                initTrigger();
                _triggered += value;
            }
            remove => _triggered -= value;
        }

        /// <summary>
        /// New positioning data event
        /// </summary>
        public event Action<Positioning> Positioning
        {
            add
            {
                initPos();
                _positioning += value;
            }
            remove => _positioning -= value;
        }

        private void reInitTasks()
        {
            Logger.Trace("Reinit called");
            if (gazeReaderTask != null && gazeCTS.IsCancellationRequested) initGaze();
            if (positioningReaderTask != null && posCTS.IsCancellationRequested) initPos();
            if (triggerReaderTask != null && triggerCTS.IsCancellationRequested) initTrigger();
            //if (connTask != null && conCTS.IsCancellationRequested) createConnectionTask();
            currentOptions = client.Configure(new OptionMessage() { Options = currentOptions });
        }

        private void initTrigger()
        {
            if (triggerReaderTask != null && !triggerCTS.IsCancellationRequested) return;
            triggerCTS = new CancellationTokenSource();
            triggerReaderTask = Task.Run(async () =>
            {
                try
                {
                    triggerStream = client.Trigger(new Google.Protobuf.WellKnownTypes.Empty());
                    while (await triggerStream.ResponseStream.MoveNext(triggerCTS.Token))
                        if (triggerStream.ResponseStream.Current != null)
                            _triggered?.Invoke(new Trigger(triggerStream.ResponseStream.Current));
                }
                catch (Exception ex)
                {
                    Logger.Trace(ex);
                }
                finally
                {
                    triggerCTS.Cancel();
                }
            }, triggerCTS.Token);
        }

        private void initGaze()
        {
            if (gazeReaderTask != null && !gazeCTS.IsCancellationRequested) return;
            gazeCTS = new CancellationTokenSource();
            gazeReaderTask = Task.Run(async () =>
            {
                try
                {
                    gazeStream = client.Gaze(new Google.Protobuf.WellKnownTypes.Empty());
                    while (await gazeStream.ResponseStream.MoveNext(gazeCTS.Token))
                        if (gazeStream.ResponseStream.Current != null)
                            _gaze?.Invoke(new Point(gazeStream.ResponseStream.Current));
                }
                catch (Exception ex)
                {
                    Logger.Trace(ex);
                }
                finally
                {
                    gazeCTS.Cancel();
                }
            }, gazeCTS.Token);
        }

        private void initPos()
        {
            if (positioningReaderTask != null && !posCTS.IsCancellationRequested) return;
            posCTS = new CancellationTokenSource();
            positioningReaderTask = Task.Run(async () =>
            {
                try
                {
                    positioningStream = client.Positioning(new Google.Protobuf.WellKnownTypes.Empty());
                    while (await positioningStream.ResponseStream.MoveNext(posCTS.Token))
                        if (positioningStream.ResponseStream.Current != null)
                            _positioning?.Invoke(new Positioning(positioningStream.ResponseStream.Current));
                }
                catch (Exception ex)
                {
                    Logger.Trace(ex);
                }
                finally
                {
                    posCTS.Cancel();
                }
            }, posCTS.Token);
        }

        /// <summary>
        /// Check if the eye tracker is available / connected
        /// </summary>
        /// <returns></returns>
        public bool Available()
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                return channel.State == ChannelState.Ready;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Start a calibration
        /// </summary>
        /// <param name="fivePoint">Set to true, if 5 point calibration instead of 9 point should be performed</param>
        public void Calibrate(int screenWidth, int screenHeight, bool fivePoint = false)
        {
            calibControl.Calibrate = true;
            calibControl.NumberOfPoints = fivePoint ? 5 : 9;
            calibControl.StopHID = true;
            calibControl.Res = new ScreenResolution()
            {
                Width = screenWidth,
                Height = screenHeight
            };
            sendCalibControl();
        }

        /// <summary>
        /// Abort a running calibration
        /// </summary>
        public void Abort()
        {
            calibControl.Abort = true;
            sendCalibControl();
        }

        /// <summary>
        /// Skyle is calibrating now
        /// </summary>
        /// <returns></returns>
        public bool Calibrating() => calibControl.Calibrate;

        /// <summary>
        /// Current calibration point
        /// </summary>
        /// <returns></returns>
        public int CurrentPoint() => calibPoint.Count;

        /// <summary>
        /// The overal quality of the last calibration
        /// </summary>
        /// <returns></returns>
        public double Quality() => calibQuality.Quality;

        /// <summary>
        /// Quality per calibration point of the last calibration
        /// </summary>
        /// <returns></returns>
        public List<double> QualityList() => new List<double>(calibQuality.Qualitys);

        /// <summary>
        /// Get current button status and configured actions
        /// </summary>
        /// <returns></returns>
        public Skyle.Button GetButton()
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                var res = client.GetButton(new Google.Protobuf.WellKnownTypes.Empty());
                Button b = new Button(res.IsPresent)
                {
                    SingleClick = res.ButtonActions.SingleClick.fromString(),
                    DoubleClick = res.ButtonActions.DoubleClick.fromString(),
                    HoldClick = res.ButtonActions.HoldClick.fromString(),
                };
                return b;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        /// <summary>
        /// Set button actions
        /// </summary>
        /// <param name="singleClick"></param>
        /// <param name="doubleClick"></param>
        /// <param name="holdClick"></param>
        /// <returns></returns>
        public bool SetButton(ButtonAction singleClick, ButtonAction doubleClick, ButtonAction holdClick)
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                ButtonActions buttonActions = new ButtonActions()
                {
                    SingleClick = singleClick.toString(),
                    DoubleClick = doubleClick.toString(),
                    HoldClick = holdClick.toString()
                };
                var res = client.SetButton(buttonActions);
                return (res.SingleClick.fromString() == singleClick && res.DoubleClick.fromString() == doubleClick && res.HoldClick.fromString() == holdClick);
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Get status of stream, disableHID and pause
        /// </summary>
        /// <returns></returns>
        public (bool stream, bool disableHID, bool pause, bool Hp) GetStatus()
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                var status = client.Configure(new OptionMessage() { Options = currentOptions });
                return (status.Stream, status.DisableMouse, status.Pause, status.Hp);
            }
            catch (Exception e)
            {
                return (false, false, false, false);
            }
        }

        /// <summary>
        /// Set ScreenResolution
        /// </summary>
        /// <param name="enable"></param>
        /// <returns></returns>
        public bool SetScreenResolution(int height, int width, int heightInMM, int widthInMM)
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                currentOptions.Res = new ScreenResolution() { Height = height, Width = width, HeightinMM = heightInMM, WidthinMM = widthInMM };
                client.Configure(new OptionMessage() { Options = currentOptions });
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Enable or disable HTTP 1.1 video stream on port 8080
        /// </summary>
        /// <param name="enable"></param>
        /// <returns></returns>
        public bool ChangeStream(bool enable)
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                currentOptions.Stream = enable;
                return client.Configure(new OptionMessage() { Options = currentOptions }).Stream;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Enable or disable pause by looking into the camera
        /// </summary>
        /// <param name="enable"></param>
        /// <returns></returns>
        public bool ChangePause(bool enable)
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                currentOptions.Pause = enable;
                return client.Configure(new OptionMessage() { Options = currentOptions }).Pause;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Disable HID (true) or enable (false)
        /// </summary>
        /// <param name="disable"></param>
        /// <returns></returns>
        public bool ChangeHID(bool disable)
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                currentOptions.DisableMouse = disable;
                return client.Configure(new OptionMessage() { Options = currentOptions }).DisableMouse;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Reset the device
        /// </summary>
        /// <param name="data"></param>
        /// <param name="services"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        public bool ResetDevice(bool data, bool services, bool device)
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                ResetMessage res = new ResetMessage()
                {
                    Data = data,
                    Services = services,
                    Device = device
                };
                return client.Reset(res).Success;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Get firmware versions and device specific data
        /// </summary>
        /// <returns></returns>
        public DeviceVersions GetVersions()
        {
            try
            {
                Skyle_Server.DeviceVersions s = client.GetVersions(new Google.Protobuf.WellKnownTypes.Empty());
                return new DeviceVersions(s);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Get all profiles
        /// </summary>
        public async Task<List<Profile>> GetProfiles()
        {
            var profiles = new List<Profile>();
            if (profileReaderTask != null && !profilesCTS.IsCancellationRequested) return profiles;
            profilesCTS = new CancellationTokenSource();

            try
            {
                profileStream = client.GetProfiles(new Google.Protobuf.WellKnownTypes.Empty());
                while (await profileStream.ResponseStream.MoveNext(profilesCTS.Token))
                    if (profileStream.ResponseStream.Current != null)
                        profiles.Add(new Profile(profileStream.ResponseStream.Current));
            }
            catch (Exception ex)
            {
                Logger.Trace(ex);
            }
            finally
            {
                profilesCTS.Cancel();
            }
            return profiles;
        }

        /// <summary>
        /// Get current profile
        /// </summary>
        public Profile GetCurrentProfile()
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                var res = client.CurrentProfile(new Google.Protobuf.WellKnownTypes.Empty());
                return new Profile(res);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        /// <summary>
        /// Set a profile
        /// </summary>
        public bool SetProfile(Profile profile)
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                var res = client.SetProfile(profile.ToProfile());
                return res.Success;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Delete a profile
        /// </summary>
        public bool DeleteProfile(Profile profile)
        {
            try
            {
                if (client == null) ConnectAsync().Wait();
                var res = client.DeleteProfile(profile.ToProfile());
                return res.Success;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            try
            {
                _connected -= Client_connected;
                calibCTS?.Cancel();
                gazeCTS?.Cancel();
                posCTS?.Cancel();
                triggerCTS?.Cancel();
                conCTS?.Cancel();
                profilesCTS?.Cancel();
                channel?.ShutdownAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine("Shutdown channel failed: " + e);
            }
        }
    }
}
