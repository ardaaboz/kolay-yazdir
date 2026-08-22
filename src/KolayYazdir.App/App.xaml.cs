using System.Windows;
using KolayYazdir.App.Services;
using Velopack;

namespace KolayYazdir.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Velopack kurulum, güncelleme ve kaldırma kancalarını devralır.
        // Uygulamanın ilk işi olmak zorunda: kurulum sırasında çalıştırıldığında
        // pencere hiç açılmadan işini yapıp süreçten çıkar. WPF'in ürettiği Main
        // korunuyor; OnStartup, StartupUri penceresi oluşturulmadan önce çalışır.
        VelopackApp.Build().Run();

        base.OnStartup(e);

        // Güncelleme kontrolü beklenmez; yazdırma işinin önüne geçmemeli.
        _ = new UpdateService(UpdateService.RepositoryUrl).CheckInBackgroundAsync();
    }
}
