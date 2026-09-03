using System.Windows;
using System.Windows.Threading;

namespace KolayYazdir.App.Tests;

/// <summary>
/// Arayüz öğeleri yalnızca STA iş parçacığında ve bir Application varken
/// ölçülebiliyor. Tüm arayüz testleri tek bir kalıcı iş parçacığında koşar;
/// AppDomain başına birden fazla Application açılamadığı için ikinci bir
/// tanesini kurmaya çalışmıyoruz.
/// </summary>
internal static class WpfThread
{
    private static readonly Lazy<Dispatcher> Ui = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    public static T Run<T>(Func<T> work) => Ui.Value.Invoke(work);

    private static Dispatcher Start()
    {
        var ready = new TaskCompletionSource<Dispatcher>();

        var thread = new Thread(() =>
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/KolayYazdir;component/Theme/Dark.xaml", UriKind.Absolute)
            });

            ready.SetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return ready.Task.GetAwaiter().GetResult();
    }
}
