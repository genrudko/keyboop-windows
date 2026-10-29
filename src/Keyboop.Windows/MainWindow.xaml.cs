using System.Windows;
namespace Keyboop.Windows;
public partial class MainWindow : Window
{
    private KeyboardHook? _hook;
    public MainWindow(){InitializeComponent(); Loaded+=(_,_)=>Start(); Closed+=(_,_)=>_hook?.Dispose();}
    private void Start()
    {
        try{_hook=new KeyboardHook(); _hook.Start(); StatusLabel.Text="Перехват активен (только ручной режим)";ToggleButton.Content="Отключить перехват";}
        catch(Exception ex){StatusLabel.Text="Ошибка запуска: "+ex.Message; ToggleButton.Content="Повторить";}
    }
    private void ToggleClick(object sender, RoutedEventArgs e)
    {
        if(_hook is not null){_hook.Dispose(); _hook=null;StatusLabel.Text="Перехват отключён";ToggleButton.Content="Включить перехват";}else Start();
    }
}