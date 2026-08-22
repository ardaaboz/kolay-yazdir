using Velopack;
using Velopack.Sources;

namespace KolayYazdir.App.Services;

/// <summary>
/// Açılışta arka planda yeni sürüme bakar, varsa indirir ve uygulamadan
/// çıkıldığında uygular. Hiçbir aşamada kullanıcıya sorulmaz ve hiçbir hata
/// gösterilmez — kırtasiyede çalışan biri güncelleme penceresiyle uğraşmamalı.
/// </summary>
public sealed class UpdateService(string repositoryUrl)
{
    /// <summary>
    /// Sürümlerin yayımlandığı depo. Herkese açık olmak zorunda: Velopack özel
    /// depodan indirmek için erişim anahtarı ister ve o anahtarı dağıtılan bir
    /// exe'nin içine gömmek onu herkese vermek demektir.
    /// </summary>
    public const string RepositoryUrl = "https://github.com/ardaaboz/kolay-yazdir";

    public async Task CheckInBackgroundAsync()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(repositoryUrl, accessToken: null, prerelease: false));

            // Geliştirme sırasında uygulama kurulu olmadığı için burada durur.
            if (!manager.IsInstalled) return;

            var update = await manager.CheckForUpdatesAsync();
            if (update is null) return;

            await manager.DownloadUpdatesAsync(update);
            manager.WaitExitThenApplyUpdates(update);
        }
        catch (Exception)
        {
            // Ağ yoksa, GitHub ulaşılamıyorsa veya paket bozuksa sessizce geç.
            // Güncelleme, yazdırma işinin önüne asla geçmemeli.
        }
    }
}
