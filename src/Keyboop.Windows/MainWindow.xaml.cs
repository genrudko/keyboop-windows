using System;
using System.Windows;
using Keyboop.Core;

namespace Keyboop.Windows;
public partial class MainWindow : Window
{
    private KeyboardHook? _hook;
    public MainWindow()
    {
        InitializeComponent();
        Closed+=(_,_)=>Stop();
    }
    private void PreviewClick(object sender,RoutedEventArgs e)
        => PreviewInput.Text=LayoutConverter.Convert(PreviewInput.Text);
    private void Start()
    {
        var hook=new KeyboardHook();
        hook.StatusChanged+=UpdateStatus;
        try
        {
            hook.Start();
            _hook=hook;
            ToggleButton.Content="Отключить тест перехвата";
        }
        catch(Exception ex)
        {
            hook.StatusChanged-=UpdateStatus;
            hook.Dispose();
            StatusLabel.Text="Не удалось запустить перехват: "+ex.Message;
            ToggleButton.Content="Повторить запуск";
        }
    }
    private void UpdateStatus(string status) => StatusLabel.Text=status;
    private void Stop()
    {
        if(_hook is not null)
        {
            _hook.StatusChanged-=UpdateStatus;
            _hook.Dispose();
            _hook=null;
        }
        StatusLabel.Text="Перехват выключен";
        ToggleButton.Content="Включить тест перехвата";
    }
    private void ToggleClick(object sender,RoutedEventArgs e)
    {
        if(_hook is null)Start();else Stop();
    }
}