using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using LibHac.Tools.FsSystem;
using Ryujinx.Audio.Backends.OpenAL;
using Ryujinx.Audio.Backends.SDL2;
using Ryujinx.Audio.Backends.SoundIo;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models.Input;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Common.GraphicsDriver;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Time.TimeZone;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Configuration.System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TimeZone = Ryujinx.Ava.UI.Models.TimeZone;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class SettingsViewModel : BaseModel
    {
        private readonly VirtualFileSystem _virtualFileSystem;
        private readonly ContentManager _contentManager;
        private TimeZoneContentManager _timeZoneContentManager;

        private readonly List<string> _validTzRegions;
        private AvaloniaList<TimeZone> _timeZones;
        private readonly Dictionary<string, string> _networkInterfaces;
        private ObservableCollection<string> _availableGpus;
        private readonly List<string> _gpuIds = new();
        private bool _isVulkanAvailable = true;

        // User Interface
        private bool _enableDiscordIntegration;
        private bool _checkUpdatesOnStart;
        private bool _showConfirmExit;
        private bool _rememberWindowState;
        private int _hideCursor;
        private int _baseStyleIndex;
        private AvaloniaList<string> _gameDirectories;

        // Input
        private bool _enableDockedMode;
        private bool _enableKeyboard;
        private bool _enableMouse;

        // Keyboard Hotkeys
        private HotkeyConfig _keyboardHotkey;

        // System
        private int _region;
        private int _language;
        private string _timeZone;
        private DateTimeOffset _currentDate;
        private TimeSpan _currentTime;
        private bool _enableVsync;
        private bool _enableFsIntegrityChecks;
        private bool _expandDramSize;
        private bool _ignoreMissingServices;

        // CPU
        private bool _enablePptc;
        private int _memoryMode;
        private bool _useHypervisor;

        // Graphics
        private int _graphicsBackendIndex;
        private int _preferredGpuIndex;
        private bool _enableShaderCache;
        private bool _enableTextureRecompression;
        private bool _enableMacroHLE;
        private bool _enableColorSpacePassthrough;
        private int _resolutionScale;
        private float _customResolutionScale;
        private int _antiAliasingEffect;
        private int _scalingFilter;
        private int _scalingFilterLevel;
        private int _maxAnisotropy;
        private int _aspectRatio;
        private int _graphicsBackendMultithreadingIndex;
        private string _shaderDumpPath;
        
        // Audio
        private int _audioBackend;
        private float _volume;
        private bool _isOpenAlEnabled;
        private bool _isSoundIoEnabled;
        private bool _isSDL2Enabled;

        // Network
        private int _multiplayerModeIndex;
        private bool _enableInternetAccess;
        private int _networkInterfaceIndex;

        // Logging
        private bool _enableFileLog;
        private bool _enableStub;
        private bool _enableInfo;
        private bool _enableWarn;
        private bool _enableError;
        private bool _enableTrace;
        private bool _enableGuest;
        private bool _enableDebug;
        private bool _enableFsAccessLog;
        private int _fsGlobalAccessLogMode;
        private int _openglDebugLevel;

        public event Action CloseWindow;
        public event Action SaveSettingsEvent;

        private bool _directoryChanged;

        public int ResolutionScale
        {
            get => _resolutionScale;
            set
            {
                _resolutionScale = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(CustomResolutionScale));
                OnPropertyChanged(nameof(IsCustomResolutionScaleActive));
            }
        }

        public int GraphicsBackendMultithreadingIndex
        {
            get => _graphicsBackendMultithreadingIndex;
            set
            {
                _graphicsBackendMultithreadingIndex = value;

                if (_graphicsBackendMultithreadingIndex != (int)ConfigurationState.Instance.Graphics.BackendThreading.Value)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                         ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningMessage],
                            "",
                            "",
                            LocaleManager.Instance[LocaleKeys.InputDialogOk],
                            LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningTitle])
                    );
                }

                OnPropertyChanged();
            }
        }

        public float CustomResolutionScale
        {
            get => _customResolutionScale;
            set
            {
                _customResolutionScale = MathF.Round(value, 1);

                OnPropertyChanged();
            }
        }

        public bool IsVulkanAvailable
        {
            get => _isVulkanAvailable;
            set
            {
                _isVulkanAvailable = value;

                OnPropertyChanged();
            }
        }

        public bool IsOpenGLAvailable => !OperatingSystem.IsMacOS();

        public bool IsHypervisorAvailable => OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public bool DirectoryChanged
        {
            get => _directoryChanged;
            set
            {
                _directoryChanged = value;

                OnPropertyChanged();
            }
        }

        public bool IsMacOS => OperatingSystem.IsMacOS();

        public bool EnableDiscordIntegration 
        { 
            get => _enableDiscordIntegration; 
            set
            {
                _enableDiscordIntegration = value;

                OnPropertyChanged();
            }
        }
        
        public bool CheckUpdatesOnStart 
        { 
            get => _checkUpdatesOnStart; 
            set
            {
                _checkUpdatesOnStart = value;

                OnPropertyChanged();
            }
        }

        public bool ShowConfirmExit
        {
            get => _showConfirmExit;
            set 
            {
                _showConfirmExit = value;

                OnPropertyChanged();
            }
        }

        public bool RememberWindowState
        {
            get => _rememberWindowState;
            set
            {
                _rememberWindowState = value;

                OnPropertyChanged();
            }
        }

        public int HideCursor
        {
            get => _hideCursor;
            set
            {
                _hideCursor = value;

                OnPropertyChanged();
            }
        }

        public bool EnableDockedMode
        {
            get => _enableDockedMode;
            set
            {
                _enableDockedMode = value;

                OnPropertyChanged();
            }
        }

        public bool EnableKeyboard
        {
            get => _enableKeyboard;
            set
            {
                _enableKeyboard = value;

                OnPropertyChanged();
            }
        }

        public bool EnableMouse
        {
            get => _enableMouse;
            set
            {
                _enableMouse = value;

                OnPropertyChanged();
            }
        }

        public bool EnableVsync
        {
            get => _enableVsync;
            set
            {
                _enableVsync = value;

                OnPropertyChanged();
            }
        }
        
        public bool EnablePptc
        {
            get => _enablePptc;
            set
            {
                _enablePptc = value;

                OnPropertyChanged();
            }
        }

        public bool EnableInternetAccess
        {
            get => _enableInternetAccess;
            set
            {
                _enableInternetAccess = value;

                OnPropertyChanged();
            }
        }
        
        public bool EnableFsIntegrityChecks
        {
            get => _enableFsIntegrityChecks;
            set
            {
                _enableFsIntegrityChecks = value;

                OnPropertyChanged();
            }
        }

        public bool IgnoreMissingServices
        {
            get => _ignoreMissingServices;
            set
            {
                _ignoreMissingServices = value;

                OnPropertyChanged();
            }
        }

        public bool ExpandDramSize
        {
            get => _expandDramSize;
            set
            {
                _expandDramSize = value;

                OnPropertyChanged();
            }
        }

        public bool EnableShaderCache
        {
            get => _enableShaderCache;
            set
            {
                _enableShaderCache = value;

                OnPropertyChanged();
            }
        }

        public bool EnableTextureRecompression
        {
            get => _enableTextureRecompression;
            set
            {
                _enableTextureRecompression = value;

                OnPropertyChanged();
            }
        }
        
        public bool EnableMacroHLE
        {
            get => _enableMacroHLE;
            set
            {
                _enableMacroHLE = value;

                OnPropertyChanged();
            }
        }
        
        public bool EnableColorSpacePassthrough
        {
            get => _enableColorSpacePassthrough;
            set
            {
                _enableColorSpacePassthrough = value;

                OnPropertyChanged();
            }
        }

        public bool ColorSpacePassthroughAvailable => IsMacOS;
        
        public bool EnableFileLog
        {
            get => _enableFileLog;
            set
            {
                _enableFileLog = value;

                OnPropertyChanged();
            }
        }
        
        public bool EnableStub
        {
            get => _enableStub;
            set
            {
                _enableStub = value;

                OnPropertyChanged();
            }
        }

        public bool EnableInfo
        {
            get => _enableInfo;
            set
            {
                _enableInfo = value;

                OnPropertyChanged();
            }
        }

        public bool EnableWarn
        {
            get => _enableWarn;
            set
            {
                _enableWarn = value;

                OnPropertyChanged();
            }
        }
        
        public bool EnableError
        {
            get => _enableError;
            set
            {
                _enableError = value;

                OnPropertyChanged();
            }
        }

        public bool EnableTrace
        {
            get => _enableTrace;
            set
            {
                _enableTrace = value;

                OnPropertyChanged();
            }
        }

        public bool EnableGuest
        {
            get => _enableGuest;
            set
            {
                _enableGuest = value;

                OnPropertyChanged();
            }
        }

        public bool EnableFsAccessLog
        {
            get => _enableFsAccessLog;
            set
            {
                _enableFsAccessLog = value;

                OnPropertyChanged();
            }
        }

        public bool EnableDebug
        {
            get => _enableDebug;
            set
            {
                _enableDebug = value;

                OnPropertyChanged();
            }
        }

        public bool IsOpenAlEnabled
        {
            get => _isOpenAlEnabled;
            set
            {
                _isOpenAlEnabled = value;

                OnPropertyChanged();
            }
        }

        public bool IsSoundIoEnabled
        {
            get => _isSoundIoEnabled;
            set
            {
                _isSoundIoEnabled = value;

                OnPropertyChanged();
            }
        }

        public bool IsSDL2Enabled
        {
            get => _isSDL2Enabled;
            set
            {
                _isSDL2Enabled = value;

                OnPropertyChanged();
            }
        }

        public bool IsCustomResolutionScaleActive => _resolutionScale == 4;
        public bool IsScalingFilterActive => _scalingFilter == (int)Ryujinx.Common.Configuration.ScalingFilter.Fsr;

        public bool IsVulkanSelected => GraphicsBackendIndex == 0;

        public bool UseHypervisor
        {
            get => _useHypervisor;
            set
            {
                _useHypervisor = value;

                OnPropertyChanged();
            }
        }

        public string TimeZone
        {
            get => _timeZone;
            set
            {
                _timeZone = value;

                OnPropertyChanged();
            }
        }

        public string ShaderDumpPath
        {
            get => _shaderDumpPath;
            set
            {
                _shaderDumpPath = value;

                OnPropertyChanged();
            }
        }

        public int Language
        {
            get => _language;
            set
            {
                _language = value;

                OnPropertyChanged();
            }
        }

        public int Region
        {
            get => _region;
            set
            {
                _region = value;

                OnPropertyChanged();
            }
        }

        public int FsGlobalAccessLogMode
        {
            get => _fsGlobalAccessLogMode;
            set
            {
                _fsGlobalAccessLogMode = value;

                OnPropertyChanged();
            }
        }

        public int AudioBackend
        {
            get => _audioBackend;
            set
            {
                _audioBackend = value;

                OnPropertyChanged();
            }
        }

        public int MaxAnisotropy
        {
            get => _maxAnisotropy;
            set
            {
                _maxAnisotropy = value;

                OnPropertyChanged();
            }
        }

        public int AspectRatio
        {
            get => _aspectRatio;
            set
            {
                _aspectRatio = value;

                OnPropertyChanged();
            }
        }

        public int AntiAliasingEffect
        {
            get => _antiAliasingEffect;
            set
            {
                _antiAliasingEffect = value;

                OnPropertyChanged();
            }
        }

        public string ScalingFilterLevelText => ScalingFilterLevel.ToString("0");
        public int ScalingFilterLevel
        {
            get => _scalingFilterLevel;
            set
            {
                _scalingFilterLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScalingFilterLevelText));
            }
        }

        public int OpenglDebugLevel
        {
            get => _openglDebugLevel;
            set
            {
                _openglDebugLevel = value;

                OnPropertyChanged();
            }
        }

        public int MemoryMode
        {
            get => _memoryMode;
            set
            {
                _memoryMode = value;

                OnPropertyChanged();
            }
        }

        public int BaseStyleIndex
        {
            get => _baseStyleIndex;
            set
            {
                _baseStyleIndex = value;

                OnPropertyChanged();
            }
        }

        public int GraphicsBackendIndex
        {
            get => _graphicsBackendIndex;
            set
            {
                _graphicsBackendIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsVulkanSelected));
            }
        }
        public int ScalingFilter
        {
            get => _scalingFilter;
            set
            {
                _scalingFilter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsScalingFilterActive));
            }
        }

        public int PreferredGpuIndex
        {
            get => _preferredGpuIndex;
            set
            {
                _preferredGpuIndex = value;

                OnPropertyChanged();
            }
        }

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value;

                ConfigurationState.Instance.System.AudioVolume.Value = _volume / 100;

                OnPropertyChanged();
            }
        }

        public DateTimeOffset CurrentDate
        {
            get => _currentDate;
            set
            {
                _currentDate = value;

                OnPropertyChanged();
            }
        }

        public TimeSpan CurrentTime
        {
            get => _currentTime;
            set
            {
                _currentTime = value;

                OnPropertyChanged();
            }
        }

        internal AvaloniaList<TimeZone> TimeZones
        {
            get => _timeZones;
            set
            {
                _timeZones = value;

                OnPropertyChanged();
            }
        }

        public AvaloniaList<string> GameDirectories
        {
            get => _gameDirectories;
            set
            {
                _gameDirectories = value;

                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> AvailableGpus
        {
            get => _availableGpus;
            set
            {
                _availableGpus = value;

                OnPropertyChanged();
            }
        }

        public AvaloniaList<string> NetworkInterfaceList
        {
            get => new(_networkInterfaces.Keys);
        }

        public HotkeyConfig KeyboardHotkey
        {
            get => _keyboardHotkey;
            set
            {
                _keyboardHotkey = value;

                OnPropertyChanged();
            }
        }

        public int NetworkInterfaceIndex
        {
            get => _networkInterfaceIndex;
            set
            {
                _networkInterfaceIndex = value != -1 ? value : 0;
                ConfigurationState.Instance.Multiplayer.LanInterfaceId.Value = _networkInterfaces[NetworkInterfaceList[_networkInterfaceIndex]];

                OnPropertyChanged();
            }
        }

        public int MultiplayerModeIndex
        {
            get => _multiplayerModeIndex;
            set
            {
                _multiplayerModeIndex = value;
                ConfigurationState.Instance.Multiplayer.Mode.Value = (MultiplayerMode)_multiplayerModeIndex;

                OnPropertyChanged();
            }
        }

        public SettingsViewModel(VirtualFileSystem virtualFileSystem, ContentManager contentManager) : this()
        {
            _virtualFileSystem = virtualFileSystem;
            _contentManager = contentManager;
            if (Program.PreviewerDetached)
            {
                Task.Run(LoadTimeZones);
            }
        }

        public SettingsViewModel()
        {
            GameDirectories = new AvaloniaList<string>();
            TimeZones = new AvaloniaList<TimeZone>();
            AvailableGpus = new ObservableCollection<string>();
            _validTzRegions = new List<string>();
            _networkInterfaces = new Dictionary<string, string>();

            Task.Run(CheckSoundBackends);
            Task.Run(PopulateNetworkInterfaces);

            if (Program.PreviewerDetached)
            {
                Task.Run(LoadAvailableGpus);
                LoadCurrentConfiguration();
            }
        }

        public void CheckSoundBackends()
        {
            IsOpenAlEnabled = OpenALHardwareDeviceDriver.IsSupported;
            IsSoundIoEnabled = SoundIoHardwareDeviceDriver.IsSupported;
            IsSDL2Enabled = SDL2HardwareDeviceDriver.IsSupported;
        }

        private async Task LoadAvailableGpus()
        {
            AvailableGpus.Clear();

            var devices = VulkanRenderer.GetPhysicalDevices();

            if (devices.Length == 0)
            {
                IsVulkanAvailable = false;
                GraphicsBackendIndex = 1;
            }
            else
            {
                foreach (var device in devices)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _gpuIds.Add(device.Id);

                        AvailableGpus.Add($"{device.Name} {(device.IsDiscrete ? "(dGPU)" : "")}");
                    });
                }
            }

            // GPU configuration needs to be loaded during the async method or it will always return 0.
            PreferredGpuIndex = _gpuIds.Contains(ConfigurationState.Instance.Graphics.PreferredGpu) ?
                                _gpuIds.IndexOf(ConfigurationState.Instance.Graphics.PreferredGpu) : 0;
        }

        public async Task LoadTimeZones()
        {
            _timeZoneContentManager = new TimeZoneContentManager();

            _timeZoneContentManager.InitializeInstance(_virtualFileSystem, _contentManager, IntegrityCheckLevel.None);

            foreach ((int offset, string location, string abbr) in _timeZoneContentManager.ParseTzOffsets())
            {
                int hours = Math.DivRem(offset, 3600, out int seconds);
                int minutes = Math.Abs(seconds) / 60;

                string abbr2 = abbr.StartsWith('+') || abbr.StartsWith('-') ? string.Empty : abbr;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    TimeZones.Add(new TimeZone($"UTC{hours:+0#;-0#;+00}:{minutes:D2}", location, abbr2));

                    _validTzRegions.Add(location);
                });
            }
        }

        private async Task PopulateNetworkInterfaces()
        {
            _networkInterfaces.Clear();
            _networkInterfaces.Add(LocaleManager.Instance[LocaleKeys.NetworkInterfaceDefault], "0");

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _networkInterfaces.Add(networkInterface.Name, networkInterface.Id);
                });
            }

            // Network interface index  needs to be loaded during the async method or it will always return 0.
            NetworkInterfaceIndex = _networkInterfaces.Values.ToList().IndexOf(ConfigurationState.Instance.Multiplayer.LanInterfaceId.Value);
        }

        public void ValidateAndSetTimeZone(string location)
        {
            if (_validTzRegions.Contains(location))
            {
                TimeZone = location;
            }
        }

        public void LoadCurrentConfiguration()
        {
            ConfigurationState config = ConfigurationState.Instance;

            // User Interface
            EnableDiscordIntegration = config.EnableDiscordIntegration;
            CheckUpdatesOnStart = config.CheckUpdatesOnStart;
            ShowConfirmExit = config.ShowConfirmExit;
            RememberWindowState = config.RememberWindowState;
            HideCursor = (int)config.HideCursor.Value;

            GameDirectories.Clear();
            GameDirectories.AddRange(config.UI.GameDirs.Value);

            BaseStyleIndex = config.UI.BaseStyle.Value switch
            {
                "Auto" => 0,
                "Light" => 1,
                "Dark" => 2,
                _ => 0
            };

            // Input
            EnableDockedMode = config.System.EnableDockedMode;
            EnableKeyboard = config.Hid.EnableKeyboard;
            EnableMouse = config.Hid.EnableMouse;

            // Keyboard Hotkeys
            KeyboardHotkey = new HotkeyConfig(config.Hid.Hotkeys.Value);

            // System
            Region = (int)config.System.Region.Value;
            Language = (int)config.System.Language.Value;
            TimeZone = config.System.TimeZone;

            DateTime currentHostDateTime = DateTime.Now;
            TimeSpan systemDateTimeOffset = TimeSpan.FromSeconds(config.System.SystemTimeOffset);
            DateTime currentDateTime = currentHostDateTime.Add(systemDateTimeOffset);
            CurrentDate = currentDateTime.Date;
            CurrentTime = currentDateTime.TimeOfDay;

            EnableVsync = config.Graphics.EnableVsync;
            EnableFsIntegrityChecks = config.System.EnableFsIntegrityChecks;
            ExpandDramSize = config.System.ExpandRam;
            IgnoreMissingServices = config.System.IgnoreMissingServices;

            // CPU
            EnablePptc = config.System.EnablePtc;
            MemoryMode = (int)config.System.MemoryManagerMode.Value;
            UseHypervisor = config.System.UseHypervisor;

            // Graphics
            GraphicsBackendIndex = (int)config.Graphics.GraphicsBackend.Value;
            // Physical devices are queried asynchronously hence the prefered index config value is loaded in LoadAvailableGpus().
            EnableShaderCache = config.Graphics.EnableShaderCache;
            EnableTextureRecompression = config.Graphics.EnableTextureRecompression;
            EnableMacroHLE = config.Graphics.EnableMacroHLE;
            EnableColorSpacePassthrough = config.Graphics.EnableColorSpacePassthrough;
            ResolutionScale = config.Graphics.ResScale == -1 ? 4 : config.Graphics.ResScale - 1;
            CustomResolutionScale = config.Graphics.ResScaleCustom;
            MaxAnisotropy = config.Graphics.MaxAnisotropy == -1 ? 0 : (int)(MathF.Log2(config.Graphics.MaxAnisotropy));
            AspectRatio = (int)config.Graphics.AspectRatio.Value;
            GraphicsBackendMultithreadingIndex = (int)config.Graphics.BackendThreading.Value;
            ShaderDumpPath = config.Graphics.ShadersDumpPath;
            AntiAliasingEffect = (int)config.Graphics.AntiAliasing.Value;
            ScalingFilter = (int)config.Graphics.ScalingFilter.Value;
            ScalingFilterLevel = config.Graphics.ScalingFilterLevel.Value;

            // Audio
            AudioBackend = (int)config.System.AudioBackend.Value;
            Volume = config.System.AudioVolume * 100;

            // Network
            EnableInternetAccess = config.System.EnableInternetAccess;
            // LAN interface index is loaded asynchronously in PopulateNetworkInterfaces()

            // Logging
            EnableFileLog = config.Logger.EnableFileLog;
            EnableStub = config.Logger.EnableStub;
            EnableInfo = config.Logger.EnableInfo;
            EnableWarn = config.Logger.EnableWarn;
            EnableError = config.Logger.EnableError;
            EnableTrace = config.Logger.EnableTrace;
            EnableGuest = config.Logger.EnableGuest;
            EnableDebug = config.Logger.EnableDebug;
            EnableFsAccessLog = config.Logger.EnableFsAccessLog;
            FsGlobalAccessLogMode = config.System.FsGlobalAccessLogMode;
            OpenglDebugLevel = (int)config.Logger.GraphicsDebugLevel.Value;

            MultiplayerModeIndex = (int)config.Multiplayer.Mode.Value;
        }

        public void SaveSettings()
        {
            ConfigurationState config = ConfigurationState.Instance;

            // User Interface
            config.EnableDiscordIntegration.Value = EnableDiscordIntegration;
            config.CheckUpdatesOnStart.Value = CheckUpdatesOnStart;
            config.ShowConfirmExit.Value = ShowConfirmExit;
            config.RememberWindowState.Value = RememberWindowState;
            config.HideCursor.Value = (HideCursorMode)HideCursor;

            if (_directoryChanged)
            {
                List<string> gameDirs = new(GameDirectories);
                config.UI.GameDirs.Value = gameDirs;
            }

            config.UI.BaseStyle.Value = BaseStyleIndex switch
            {
                0 => "Auto",
                1 => "Light",
                2 => "Dark",
                _ => "Auto"
            };

            // Input
            config.System.EnableDockedMode.Value = EnableDockedMode;
            config.Hid.EnableKeyboard.Value = EnableKeyboard;
            config.Hid.EnableMouse.Value = EnableMouse;

            // Keyboard Hotkeys
            config.Hid.Hotkeys.Value = KeyboardHotkey.GetConfig();

            // System
            config.System.Region.Value = (Region)Region;
            config.System.Language.Value = (Language)Language;

            if (_validTzRegions.Contains(TimeZone))
            {
                config.System.TimeZone.Value = TimeZone;
            }

            config.System.SystemTimeOffset.Value = Convert.ToInt64((CurrentDate.ToUnixTimeSeconds() + CurrentTime.TotalSeconds) - DateTimeOffset.Now.ToUnixTimeSeconds());
            config.Graphics.EnableVsync.Value = EnableVsync;
            config.System.EnableFsIntegrityChecks.Value = EnableFsIntegrityChecks;
            config.System.ExpandRam.Value = ExpandDramSize;
            config.System.IgnoreMissingServices.Value = IgnoreMissingServices;

            // CPU
            config.System.EnablePtc.Value = EnablePptc;
            config.System.MemoryManagerMode.Value = (MemoryManagerMode)MemoryMode;
            config.System.UseHypervisor.Value = UseHypervisor;

            // Graphics
            config.Graphics.GraphicsBackend.Value = (GraphicsBackend)GraphicsBackendIndex;
            config.Graphics.PreferredGpu.Value = _gpuIds.ElementAtOrDefault(PreferredGpuIndex);
            config.Graphics.EnableShaderCache.Value = EnableShaderCache;
            config.Graphics.EnableTextureRecompression.Value = EnableTextureRecompression;
            config.Graphics.EnableMacroHLE.Value = EnableMacroHLE;
            config.Graphics.EnableColorSpacePassthrough.Value = EnableColorSpacePassthrough;
            config.Graphics.ResScale.Value = ResolutionScale == 4 ? -1 : ResolutionScale + 1;
            config.Graphics.ResScaleCustom.Value = CustomResolutionScale;
            config.Graphics.MaxAnisotropy.Value = MaxAnisotropy == 0 ? -1 : MathF.Pow(2, MaxAnisotropy);
            config.Graphics.AspectRatio.Value = (AspectRatio)AspectRatio;
            config.Graphics.AntiAliasing.Value = (AntiAliasing)AntiAliasingEffect;
            config.Graphics.ScalingFilter.Value = (ScalingFilter)ScalingFilter;
            config.Graphics.ScalingFilterLevel.Value = ScalingFilterLevel;

            if (ConfigurationState.Instance.Graphics.BackendThreading != (BackendThreading)GraphicsBackendMultithreadingIndex)
            {
                DriverUtilities.ToggleOGLThreading(GraphicsBackendMultithreadingIndex == (int)BackendThreading.Off);
            }

            config.Graphics.BackendThreading.Value = (BackendThreading)GraphicsBackendMultithreadingIndex;
            config.Graphics.ShadersDumpPath.Value = ShaderDumpPath;

            // Audio
            AudioBackend audioBackend = (AudioBackend)AudioBackend;
            if (audioBackend != config.System.AudioBackend.Value)
            {
                config.System.AudioBackend.Value = audioBackend;

                Logger.Info?.Print(LogClass.Application, $"AudioBackend toggled to: {audioBackend}");
            }

            config.System.AudioVolume.Value = Volume / 100;

            // Network
            config.System.EnableInternetAccess.Value = EnableInternetAccess;

            // Logging
            config.Logger.EnableFileLog.Value = EnableFileLog;
            config.Logger.EnableStub.Value = EnableStub;
            config.Logger.EnableInfo.Value = EnableInfo;
            config.Logger.EnableWarn.Value = EnableWarn;
            config.Logger.EnableError.Value = EnableError;
            config.Logger.EnableTrace.Value = EnableTrace;
            config.Logger.EnableGuest.Value = EnableGuest;
            config.Logger.EnableDebug.Value = EnableDebug;
            config.Logger.EnableFsAccessLog.Value = EnableFsAccessLog;
            config.System.FsGlobalAccessLogMode.Value = FsGlobalAccessLogMode;
            config.Logger.GraphicsDebugLevel.Value = (GraphicsDebugLevel)OpenglDebugLevel;

            config.Multiplayer.LanInterfaceId.Value = _networkInterfaces[NetworkInterfaceList[NetworkInterfaceIndex]];
            config.Multiplayer.Mode.Value = (MultiplayerMode)MultiplayerModeIndex;

            config.ToFileFormat().SaveConfig(Program.ConfigurationPath);

            MainWindow.UpdateGraphicsConfig();

            SaveSettingsEvent?.Invoke();

            _directoryChanged = false;
        }

        private static void RevertIfNotSaved()
        {
            Program.ReloadConfig();
        }

        public void ApplyButton()
        {
            SaveSettings();
        }

        public void OkButton()
        {
            SaveSettings();
            CloseWindow?.Invoke();
        }

        public void CancelButton()
        {
            RevertIfNotSaved();
            CloseWindow?.Invoke();
        }

        public void RestoreDefaults()
        {
            ConfigurationState.Instance.LoadDefault();
            LoadCurrentConfiguration();
        }
    }
}
