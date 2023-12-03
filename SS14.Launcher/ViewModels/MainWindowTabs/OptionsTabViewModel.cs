using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using DynamicData;
using Marsey.Stealthsey;
using Microsoft.Toolkit.Mvvm.Input;
using Splat;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.EngineManager;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class OptionsTabViewModel : MainWindowTabViewModel, INotifyPropertyChanged
{
    private DataManager Cfg { get; }
    private readonly LoginManager _loginManager;
    private readonly DataManager _dataManager;
    private readonly IEngineManager _engineManager;
    private readonly ContentManager _contentManager;
    
    public ICommand SetUsernameCommand { get; }
    public IEnumerable<HideLevel> HideLevels { get; } = Enum.GetValues(typeof(HideLevel)).Cast<HideLevel>();

    
    public OptionsTabViewModel()
    {
        Cfg = Locator.Current.GetRequiredService<DataManager>();
        _loginManager = Locator.Current.GetRequiredService<LoginManager>();
        _dataManager = Locator.Current.GetRequiredService<DataManager>();
        _engineManager = Locator.Current.GetRequiredService<IEngineManager>();
        _contentManager = Locator.Current.GetRequiredService<ContentManager>();
        
        SetUsernameCommand = new RelayCommand(OnSetUsernameClick);
    }

#if RELEASE
        public bool HideDisableSigning => true;
#else
    public bool HideDisableSigning => false;
#endif

    public override string Name => "Options";

    public bool CompatMode
    {
        get => Cfg.GetCVar(CVars.CompatMode);
        set
        {
            Cfg.SetCVar(CVars.CompatMode, value);
            Cfg.CommitConfig();
        }
    }

    public bool DynamicPgo
    {
        get => Cfg.GetCVar(CVars.DynamicPgo);
        set
        {
            Cfg.SetCVar(CVars.DynamicPgo, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogClient
    {
        get => Cfg.GetCVar(CVars.LogClient);
        set
        {
            Cfg.SetCVar(CVars.LogClient, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogLauncher
    {
        get => Cfg.GetCVar(CVars.LogLauncher);
        set
        {
            Cfg.SetCVar(CVars.LogLauncher, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogLauncherVerbose
    {
        get => Cfg.GetCVar(CVars.LogLauncherVerbose);
        set
        {
            Cfg.SetCVar(CVars.LogLauncherVerbose, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogPatches
    {
        get => Cfg.GetCVar(CVars.LogPatches);
        set
        {
            Cfg.SetCVar(CVars.LogPatches, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogLoaderDebug
    {
        get => Cfg.GetCVar(CVars.LogLoaderDebug);
        set
        {
            Cfg.SetCVar(CVars.LogLoaderDebug, value);
            Cfg.CommitConfig();
        }
    }

    public bool ThrowPatchFail
    {
        get => Cfg.GetCVar(CVars.ThrowPatchFail);
        set
        {
            Cfg.SetCVar(CVars.ThrowPatchFail, value);
            Cfg.CommitConfig();
        }
    }
    
    public bool SeparateLogging
    {
        get => Cfg.GetCVar(CVars.SeparateLogging);
        set
        {
            Cfg.SetCVar(CVars.SeparateLogging, value);
            Cfg.CommitConfig();
        }
    }

    public HideLevel HideLevel
    {
        get => (HideLevel)Cfg.GetCVar(CVars.MarseyHide);
        set
        {
            Cfg.SetCVar(CVars.MarseyHide, (int)value);
            OnPropertyChanged(nameof(HideLevel));
            Cfg.CommitConfig();
        }
    }

    public bool DisableSigning
    {
        get => Cfg.GetCVar(CVars.DisableSigning);
        set
        {
            Cfg.SetCVar(CVars.DisableSigning, value);
            Cfg.CommitConfig();
        }
    }

    public bool OverrideAssets
    {
        get => Cfg.GetCVar(CVars.OverrideAssets);
        set
        {
            Cfg.SetCVar(CVars.OverrideAssets, value);
            Cfg.CommitConfig();
        }
    }

    public string Username
    {
        get => _loginManager.ActiveAccount?.Username!;
        set
        {
            LoginInfo LI = _loginManager.ActiveAccount!.LoginInfo;
            LI.Username = value;
        }
    }
    
    private void OnSetUsernameClick()
    {
        _dataManager.ChangeLogin(ChangeReason.Update, _loginManager.ActiveAccount?.LoginInfo!);
        _dataManager.CommitConfig();
    }

    public void ClearEngines()
    {
        _engineManager.ClearAllEngines();
    }

    public void ClearServerContent()
    {
        _contentManager.ClearAll();
    }

    public void OpenLogDirectory()
    {
        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = LauncherPaths.DirLogs
        });
    }

    public void OpenAccountSettings()
    {
        Helpers.OpenUri(ConfigConstants.AccountManagementUrl);
    }
    
    public new event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class HideLevelDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (HideLevel)(value ?? HideLevel.Normal) switch
        {
            HideLevel.Disabled => "Hidesey is disabled. Servers with engine version 183.0.0 or above crash the client.",
            HideLevel.Duplicit => "Patcher is hidden from the game programmatically. For cases when admins are more interested what patches are you using, rather than if you are using them.",
            HideLevel.Normal => "Patcher and patches are hidden from the game programmatically.",
            HideLevel.Explicit => "Patcher and patches are hidden from the game programmatically. Patcher does not log anything.",
            HideLevel.Unconditional => "Patcher and patches are hidden from the game programmatically. Patcher does not log anything. Preloads and subversions are disabled.",
            _ => "Unknown hide level."
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}

