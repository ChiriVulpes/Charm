﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Field;
using Field.General;
using Field.Models;
using Field.Statics;

namespace Charm;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public static ProgressView Progress = null;
    
    private void OnControlLoaded(object sender, RoutedEventArgs routedEventArgs)
    {
        Progress = ProgressView;
        Task.Run(InitialiseHandlers);
    }
    
    public MainWindow()
    {
        // todo give this all a progressbar
        InitializeComponent();
        
        // Hide tab by default
        MainTabControl.Visibility = Visibility.Hidden;
            
        // Check if packages path exists in config
        ConfigHandler.CheckPackagesPathIsValid();
        MainTabControl.Visibility = Visibility.Visible;
    }

    private async void InitialiseHandlers()
    {
        Progress.SetProgressStages(new List<string>
        {
            "FNV Handler",
            "Hash64",
            "Font Handler",
            "Investment",
            "Global string cache",
            "Fbx Handler",
            "Activity Names",
        });

        // Initialise FNV handler -- must be first bc my code is shit
        await Task.Run(FnvHandler.Initialise);
        Progress.CompleteStage();

        // Get all hash64 -- must be before InvestmentHandler
        await Task.Run(TagHash64Handler.Initialise);
        Progress.CompleteStage();
        
        // Load all the fonts
        await Task.Run(() =>
        {
            RegisterFonts(FontHandler.Initialise());
        });
        Progress.CompleteStage();
        
        // Initialise investment
        await Task.Run(InvestmentHandler.Initialise);
        Progress.CompleteStage();

        // Initialise global string cache
        await Task.Run(PackageHandler.GenerateGlobalStringContainerCache);
        Progress.CompleteStage();
        
        // Initialise fbx handler
        await Task.Run(FbxHandler.Initialise);
        Progress.CompleteStage();
        
        // Get all activity names
        await Task.Run(PackageHandler.GetAllActivityNames);
        Progress.CompleteStage();
    }

    private void RegisterFonts(ConcurrentDictionary<FontHandler.FontInfo, FontFamily> initialise)
    {
        foreach (var (key, value) in initialise)
        {
            Application.Current.Resources.Add($"{key.Family} {key.Subfamily}", value);
        }

        // Debug font list
        List<string> fontList = initialise.Select(pair => pair.Key.Family + " " + pair.Key.Subfamily).ToList();
        
        var a = 0;
    }

    private void OpenConfigPanel_OnClick(object sender, RoutedEventArgs e)
    {
        TabItem newTab = new TabItem();
        newTab.Header = "Configuration";
        newTab.Content = new ConfigView();
        MainTabControl.Items.Add(newTab);
        MainTabControl.SelectedItem = newTab;
    }
}